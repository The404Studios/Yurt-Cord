using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<FriendHub> _logger;
    private static readonly ConcurrentDictionary<string, string> _userConnections = new(); // userId -> connectionId
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, DateTime> _connectionTimestamps = new();

    public FriendHub(FriendService friendService, DirectMessageService dmService, AuthService authService, ILogger<FriendHub> logger)
    {
        _friendService = friendService;
        _dmService = dmService;
        _authService = authService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _connectionTimestamps[connectionId] = DateTime.UtcNow;

        _logger.LogDebug("FriendHub connection established: {ConnectionId}", connectionId);

        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = connectionId,
            ServerTime = DateTime.UtcNow,
            Hub = "FriendHub"
        });

        await base.OnConnectedAsync();
    }

    public async Task Authenticate(string token)
    {
        var connectionId = Context.ConnectionId;

        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            _logger.LogDebug("FriendHub authentication failed: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("AuthenticationFailed", new
            {
                Error = "InvalidToken",
                Message = "Invalid or expired token"
            });
            return;
        }

        _logger.LogInformation("FriendHub user authenticated: {Username}", user.Username);

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

        // Get friend info before removal for the FriendRemoved event
        var friendUser = _authService.GetUserById(friendId);

        var (success, message) = _friendService.RemoveFriend(userId, friendId);

        if (success)
        {
            // Send FriendRemoved event with friend DTO
            if (friendUser != null)
            {
                var removedFriendDto = new FriendDto
                {
                    UserId = friendUser.Id,
                    Username = friendUser.Username,
                    DisplayName = friendUser.DisplayName,
                    AvatarUrl = friendUser.AvatarUrl
                };
                await Clients.Caller.SendAsync("FriendRemoved", removedFriendDto);
            }

            // Also send updated friends list
            var myFriends = _friendService.GetFriends(userId);
            await Clients.Caller.SendAsync("FriendsList", myFriends);

            if (_userConnections.TryGetValue(friendId, out var friendConnId))
            {
                // Notify the other user they were removed
                var myUser = _authService.GetUserById(userId);
                if (myUser != null)
                {
                    var meAsRemovedDto = new FriendDto
                    {
                        UserId = myUser.Id,
                        Username = myUser.Username,
                        DisplayName = myUser.DisplayName,
                        AvatarUrl = myUser.AvatarUrl
                    };
                    await Clients.Client(friendConnId).SendAsync("FriendRemoved", meAsRemovedDto);
                }

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
    public async Task GetConversations()
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var conversations = _dmService.GetConversations(userId);
        await Clients.Caller.SendAsync("Conversations", conversations);
    }

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
            // Get username for the typing indicator
            var user = _authService.GetUserById(userId);
            var username = user?.Username ?? "Unknown";
            await Clients.Client(recipientConnId).SendAsync("UserTypingDM", userId, username);
        }
    }

    public async Task StopTypingDM(string recipientId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_userConnections.TryGetValue(recipientId, out var recipientConnId))
        {
            await Clients.Client(recipientConnId).SendAsync("UserStoppedTypingDM", userId);
        }
    }

    public async Task CancelFriendRequest(string requestId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message) = _friendService.CancelFriendRequest(userId, requestId);

        if (success)
        {
            // Update the caller's outgoing requests
            var outgoingRequests = _friendService.GetOutgoingRequests(userId);
            await Clients.Caller.SendAsync("OutgoingRequests", outgoingRequests);
            await Clients.Caller.SendAsync("FriendRequestCancelled", requestId);
        }
        else
        {
            await Clients.Caller.SendAsync("FriendError", message);
        }
    }

    public async Task BlockUser(string targetUserId, string? reason = null)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message) = _friendService.BlockUser(userId, targetUserId, reason);

        if (success)
        {
            // Remove from friends list if they were friends
            var friends = _friendService.GetFriends(userId);
            await Clients.Caller.SendAsync("FriendsList", friends);
            await Clients.Caller.SendAsync("UserBlocked", targetUserId);
            await Clients.Caller.SendAsync("Success", "User blocked successfully");

            // Notify the blocked user they've been removed (don't tell them they're blocked)
            if (_userConnections.TryGetValue(targetUserId, out var targetConnId))
            {
                var targetFriends = _friendService.GetFriends(targetUserId);
                await Clients.Client(targetConnId).SendAsync("FriendsList", targetFriends);
            }
        }
        else
        {
            await Clients.Caller.SendAsync("BlockError", message);
        }
    }

    public async Task UnblockUser(string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message) = _friendService.UnblockUser(userId, targetUserId);

        if (success)
        {
            await Clients.Caller.SendAsync("UserUnblocked", targetUserId);
            await Clients.Caller.SendAsync("Success", "User unblocked successfully");
        }
        else
        {
            await Clients.Caller.SendAsync("BlockError", message);
        }
    }

    public async Task GetBlockedUsers()
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var blockedUsers = _friendService.GetBlockedUsers(userId);
        await Clients.Caller.SendAsync("BlockedUsers", blockedUsers);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _connectionTimestamps.TryRemove(connectionId, out _);

        if (_connectionUsers.TryRemove(connectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);
            _logger.LogInformation("FriendHub user disconnected: {UserId}", userId);

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
        else
        {
            _logger.LogDebug("Unauthenticated FriendHub connection disconnected: {ConnectionId}", connectionId);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "FriendHub connection {ConnectionId} disconnected with error", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Heartbeat to keep connection alive
    /// </summary>
    public async Task Ping()
    {
        _connectionTimestamps[Context.ConnectionId] = DateTime.UtcNow;
        await Clients.Caller.SendAsync("Pong", new { ServerTime = DateTime.UtcNow });
    }

    public Task<string?> GetUserNote(string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return Task.FromResult<string?>(null);

        return Task.FromResult(_friendService.GetUserNote(userId, targetUserId));
    }

    public async Task SetUserNote(string targetUserId, string note)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        _friendService.SetUserNote(userId, targetUserId, note);
        await Clients.Caller.SendAsync("UserNoteUpdated", targetUserId);
    }
}
