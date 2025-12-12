using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Hubs;

public class FriendHub : Hub
{
    private readonly FriendService _friendService;
    private readonly DirectMessageService _dmService;
    private readonly AuthService _authService;
    private static readonly ConcurrentDictionary<string, string> _userConnections = new(); // userId -> connectionId
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId

    public FriendHub(FriendService friendService, DirectMessageService dmService, AuthService authService)
    {
        _friendService = friendService;
        _dmService = dmService;
        _authService = authService;
    }

    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        // Track connection
        _userConnections[user.Id] = Context.ConnectionId;
        _connectionUsers[Context.ConnectionId] = user.Id;

        // Add to personal group for targeted messages
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");

        // Send initial data
        var friends = _friendService.GetFriends(user.Id);
        var pendingRequests = _friendService.GetPendingRequests(user.Id);
        var outgoingRequests = _friendService.GetOutgoingRequests(user.Id);
        var conversations = _dmService.GetConversations(user.Id);

        await Clients.Caller.SendAsync("FriendsList", friends);
        await Clients.Caller.SendAsync("PendingRequests", pendingRequests);
        await Clients.Caller.SendAsync("OutgoingRequests", outgoingRequests);
        await Clients.Caller.SendAsync("Conversations", conversations);

        // Notify friends that user is online
        foreach (var friend in friends)
        {
            if (_userConnections.TryGetValue(friend.UserId, out var friendConnId))
            {
                await Clients.Client(friendConnId).SendAsync("FriendOnline", user.Id, user.Username);
            }
        }

        await Clients.Caller.SendAsync("AuthenticationSuccess");
    }

    // Friend Requests - by username
    public async Task SendFriendRequest(string username)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message, friendship) = _friendService.SendFriendRequest(userId, username);
        await HandleFriendRequestResult(userId, success, message, friendship);
    }

    // Friend Requests - by user ID
    public async Task SendFriendRequestById(string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message, friendship) = _friendService.SendFriendRequestById(userId, targetUserId);
        await HandleFriendRequestResult(userId, success, message, friendship);
    }

    private async Task HandleFriendRequestResult(string userId, bool success, string message, Friendship? friendship)
    {
        if (success && friendship != null)
        {
            // Notify the requester
            await Clients.Caller.SendAsync("FriendRequestSent", message);

            // Update requester's outgoing requests
            var outgoing = _friendService.GetOutgoingRequests(userId);
            await Clients.Caller.SendAsync("OutgoingRequests", outgoing);

            // Notify the addressee if online
            if (_userConnections.TryGetValue(friendship.AddresseeId, out var addresseeConnId))
            {
                var pending = _friendService.GetPendingRequests(friendship.AddresseeId);
                await Clients.Client(addresseeConnId).SendAsync("PendingRequests", pending);
                await Clients.Client(addresseeConnId).SendAsync("NewFriendRequest", pending.FirstOrDefault());
            }
        }
        else
        {
            await Clients.Caller.SendAsync("FriendRequestError", message);
        }
    }

    // Search users by ID or username
    public async Task SearchUser(string query)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var user = _friendService.SearchUserByIdOrUsername(query);
        if (user != null && user.Id != userId)
        {
            // Check if already friends or pending
            var isFriend = _friendService.AreFriends(userId, user.Id);
            var result = new UserSearchResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Bio = user.Bio,
                StatusMessage = user.StatusMessage,
                Role = user.Role,
                Rank = user.Rank,
                IsOnline = user.IsOnline,
                IsFriend = isFriend
            };
            await Clients.Caller.SendAsync("UserSearchResult", result);
        }
        else
        {
            await Clients.Caller.SendAsync("UserSearchResult", null);
        }
    }

    // Search multiple users
    public async Task SearchUsers(string query)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var users = _friendService.SearchUsers(query);
        var results = users
            .Where(u => u.Id != userId)
            .Select(u => new UserSearchResultDto
            {
                UserId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Bio = u.Bio,
                StatusMessage = u.StatusMessage,
                Role = u.Role,
                Rank = u.Rank,
                IsOnline = u.IsOnline,
                IsFriend = _friendService.AreFriends(userId, u.Id)
            })
            .ToList();

        await Clients.Caller.SendAsync("UserSearchResults", results);
    }

    public async Task RespondToFriendRequest(string requestId, bool accept)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var friendship = _friendService.GetPendingRequests(userId)
            .FirstOrDefault(r => r.Id == requestId);

        if (friendship == null)
        {
            await Clients.Caller.SendAsync("FriendRequestError", "Request not found");
            return;
        }

        var (success, message) = _friendService.RespondToFriendRequest(userId, requestId, accept);

        if (success)
        {
            // Update both users' friend lists
            var myFriends = _friendService.GetFriends(userId);
            var myPending = _friendService.GetPendingRequests(userId);
            await Clients.Caller.SendAsync("FriendsList", myFriends);
            await Clients.Caller.SendAsync("PendingRequests", myPending);

            if (_userConnections.TryGetValue(friendship.RequesterId, out var requesterConnId))
            {
                var theirFriends = _friendService.GetFriends(friendship.RequesterId);
                var theirOutgoing = _friendService.GetOutgoingRequests(friendship.RequesterId);
                await Clients.Client(requesterConnId).SendAsync("FriendsList", theirFriends);
                await Clients.Client(requesterConnId).SendAsync("OutgoingRequests", theirOutgoing);

                if (accept)
                {
                    await Clients.Client(requesterConnId).SendAsync("FriendRequestAccepted", userId);
                }
                else
                {
                    // Notify requester that their request was declined
                    await Clients.Client(requesterConnId).SendAsync("FriendRequestDeclined", userId);
                }
            }

            await Clients.Caller.SendAsync("FriendRequestResponded", accept ? "accepted" : "declined");
        }
        else
        {
            await Clients.Caller.SendAsync("FriendRequestError", message);
        }
    }

    public async Task RemoveFriend(string friendId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message) = _friendService.RemoveFriend(userId, friendId);

        if (success)
        {
            var myFriends = _friendService.GetFriends(userId);
            await Clients.Caller.SendAsync("FriendsList", myFriends);

            if (_userConnections.TryGetValue(friendId, out var friendConnId))
            {
                var theirFriends = _friendService.GetFriends(friendId);
                await Clients.Client(friendConnId).SendAsync("FriendsList", theirFriends);
            }
        }
        else
        {
            await Clients.Caller.SendAsync("FriendError", message);
        }
    }

    // Direct Messages
    public async Task GetDMHistory(string partnerId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var messages = _dmService.GetMessages(userId, partnerId);
        _dmService.MarkAsRead(userId, partnerId);

        await Clients.Caller.SendAsync("DMHistory", partnerId, messages);

        // Update conversation list with new unread count
        var conversations = _dmService.GetConversations(userId);
        await Clients.Caller.SendAsync("Conversations", conversations);
    }

    public async Task SendDirectMessage(string recipientId, string content)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message, dto) = _dmService.SendMessage(userId, recipientId, content);

        if (success && dto != null)
        {
            // Send to sender
            await Clients.Caller.SendAsync("DirectMessageReceived", dto);

            // Send to recipient if online
            if (_userConnections.TryGetValue(recipientId, out var recipientConnId))
            {
                await Clients.Client(recipientConnId).SendAsync("DirectMessageReceived", dto);

                // Update their conversations
                var theirConversations = _dmService.GetConversations(recipientId);
                await Clients.Client(recipientConnId).SendAsync("Conversations", theirConversations);
            }

            // Update sender's conversations
            var myConversations = _dmService.GetConversations(userId);
            await Clients.Caller.SendAsync("Conversations", myConversations);
        }
        else
        {
            await Clients.Caller.SendAsync("DMError", message);
        }
    }

    public async Task MarkMessagesRead(string partnerId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        _dmService.MarkAsRead(userId, partnerId);

        var conversations = _dmService.GetConversations(userId);
        await Clients.Caller.SendAsync("Conversations", conversations);
    }

    public async Task StartTypingDM(string recipientId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_userConnections.TryGetValue(recipientId, out var recipientConnId))
        {
            await Clients.Client(recipientConnId).SendAsync("UserTypingDM", userId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUsers.TryRemove(Context.ConnectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);

            // Notify friends that user went offline
            var friends = _friendService.GetFriends(userId);
            foreach (var friend in friends)
            {
                if (_userConnections.TryGetValue(friend.UserId, out var friendConnId))
                {
                    await Clients.Client(friendConnId).SendAsync("FriendOffline", userId);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
