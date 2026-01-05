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
    private readonly ActivityService _activityService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<FriendHub> _logger;
    private static readonly ConcurrentDictionary<string, List<string>> _userConnections = new(); // userId -> connectionIds
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, DateTime> _connectionTimestamps = new();

    public FriendHub(
        FriendService friendService,
        DirectMessageService dmService,
        AuthService authService,
        ActivityService activityService,
        NotificationService notificationService,
        ILogger<FriendHub> logger)
    {
        _friendService = friendService;
        _dmService = dmService;
        _authService = authService;
        _activityService = activityService;
        _notificationService = notificationService;
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

        // Track connection (support multiple connections per user)
        _userConnections.AddOrUpdate(
            user.Id,
            _ => new List<string> { Context.ConnectionId },
            (_, list) => { lock (list) { if (!list.Contains(Context.ConnectionId)) list.Add(Context.ConnectionId); } return list; }
        );
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

        // Notify friends that user is online (send to all their connections)
        foreach (var friend in friends)
        {
            if (_userConnections.TryGetValue(friend.UserId, out var friendConnIds))
            {
                List<string> connIdsCopy;
                lock (friendConnIds) { connIdsCopy = friendConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    await Clients.Client(connId).SendAsync("FriendOnline", user.Id, user.Username);
                }
            }
        }

        await Clients.Caller.SendAsync("AuthenticationSuccess");
    }

    // Friend Requests - by username
    public async Task SendFriendRequest(string username)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (string.IsNullOrWhiteSpace(username))
        {
            await Clients.Caller.SendAsync("FriendRequestError", "Username is required");
            return;
        }

        var (success, message, friendship) = _friendService.SendFriendRequest(userId, username);
        await HandleFriendRequestResult(userId, success, message, friendship);
    }

    // Friend Requests - by user ID
    public async Task SendFriendRequestById(string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            await Clients.Caller.SendAsync("FriendRequestError", "User ID is required");
            return;
        }

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

            // Create a persistent notification for the addressee
            var requester = _authService.GetUserById(userId);
            if (requester != null)
            {
                _notificationService.CreateNotification(
                    friendship.AddresseeId,
                    "Friend Request",
                    $"{requester.Username} sent you a friend request",
                    "friend_request",
                    friendship.Id);
            }

            // Notify the addressee if online (all their connections)
            if (_userConnections.TryGetValue(friendship.AddresseeId, out var addresseeConnIds))
            {
                var pending = _friendService.GetPendingRequests(friendship.AddresseeId);
                List<string> connIdsCopy;
                lock (addresseeConnIds) { connIdsCopy = addresseeConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    await Clients.Client(connId).SendAsync("PendingRequests", pending);
                    await Clients.Client(connId).SendAsync("NewFriendRequest", pending.FirstOrDefault());
                }
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

        if (string.IsNullOrWhiteSpace(query))
        {
            await Clients.Caller.SendAsync("UserSearchResult", null);
            return;
        }

        var user = _friendService.SearchUserByIdOrUsername(query);
        // Don't show user if blocked (either direction)
        if (user != null && user.Id != userId && !_friendService.IsBlocked(userId, user.Id))
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

        if (string.IsNullOrWhiteSpace(query))
        {
            await Clients.Caller.SendAsync("UserSearchResults", new List<UserSearchResultDto>());
            return;
        }

        var users = _friendService.SearchUsers(query);
        if (users == null)
        {
            await Clients.Caller.SendAsync("UserSearchResults", new List<UserSearchResultDto>());
            return;
        }

        var results = users
            .Where(u => u.Id != userId)
            .Where(u => !_friendService.IsBlocked(userId, u.Id)) // Filter out blocked users
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
            // Log activity for both users when friend request is accepted
            if (accept)
            {
                _activityService.LogFriendAdded(userId, friendship.RequesterId);
                _activityService.LogFriendAdded(friendship.RequesterId, userId);
            }

            // Create notification for the requester about the response
            var responder = _authService.GetUserById(userId);
            if (responder != null)
            {
                _notificationService.CreateNotification(
                    friendship.RequesterId,
                    accept ? "Friend Request Accepted" : "Friend Request Declined",
                    accept
                        ? $"{responder.Username} accepted your friend request"
                        : $"{responder.Username} declined your friend request",
                    "friend_request",
                    requestId);
            }

            // Update both users' friend lists
            var myFriends = _friendService.GetFriends(userId);
            var myPending = _friendService.GetPendingRequests(userId);
            await Clients.Caller.SendAsync("FriendsList", myFriends);
            await Clients.Caller.SendAsync("PendingRequests", myPending);

            if (_userConnections.TryGetValue(friendship.RequesterId, out var requesterConnIds))
            {
                var theirFriends = _friendService.GetFriends(friendship.RequesterId);
                var theirOutgoing = _friendService.GetOutgoingRequests(friendship.RequesterId);
                List<string> connIdsCopy;
                lock (requesterConnIds) { connIdsCopy = requesterConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    await Clients.Client(connId).SendAsync("FriendsList", theirFriends);
                    await Clients.Client(connId).SendAsync("OutgoingRequests", theirOutgoing);

                    if (accept)
                    {
                        await Clients.Client(connId).SendAsync("FriendRequestAccepted", userId);
                    }
                    else
                    {
                        await Clients.Client(connId).SendAsync("FriendRequestDeclined", userId);
                    }
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

            if (_userConnections.TryGetValue(friendId, out var friendConnIds))
            {
                // Notify the other user they were removed (all connections)
                var myUser = _authService.GetUserById(userId);
                var theirFriends = _friendService.GetFriends(friendId);
                List<string> connIdsCopy;
                lock (friendConnIds) { connIdsCopy = friendConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    if (myUser != null)
                    {
                        var meAsRemovedDto = new FriendDto
                        {
                            UserId = myUser.Id,
                            Username = myUser.Username,
                            DisplayName = myUser.DisplayName,
                            AvatarUrl = myUser.AvatarUrl
                        };
                        await Clients.Client(connId).SendAsync("FriendRemoved", meAsRemovedDto);
                    }
                    await Clients.Client(connId).SendAsync("FriendsList", theirFriends);
                }
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

        if (string.IsNullOrWhiteSpace(recipientId))
        {
            await Clients.Caller.SendAsync("DMError", "Recipient is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            await Clients.Caller.SendAsync("DMError", "Message content is required");
            return;
        }

        var (success, message, dto) = _dmService.SendMessage(userId, recipientId, content);

        if (success && dto != null)
        {
            // Send to sender
            await Clients.Caller.SendAsync("DirectMessageReceived", dto);

            // Get sender info for notification
            var sender = _authService.GetUserById(userId);

            // Send to recipient if online (all connections)
            if (_userConnections.TryGetValue(recipientId, out var recipientConnIds))
            {
                var theirConversations = _dmService.GetConversations(recipientId);
                List<string> connIdsCopy;
                lock (recipientConnIds) { connIdsCopy = recipientConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    await Clients.Client(connId).SendAsync("DirectMessageReceived", dto);
                    await Clients.Client(connId).SendAsync("Conversations", theirConversations);
                }
            }
            else
            {
                // Recipient is offline - create a persistent notification
                if (sender != null)
                {
                    var truncatedContent = content.Length > 50 ? content[..50] + "..." : content;
                    _notificationService.CreateNotification(
                        recipientId,
                        "New Message",
                        $"{sender.Username}: {truncatedContent}",
                        "direct_message",
                        dto.Id);
                }
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

        if (_userConnections.TryGetValue(recipientId, out var recipientConnIds))
        {
            // Get username for the typing indicator
            var user = _authService.GetUserById(userId);
            var username = user?.Username ?? "Unknown";
            List<string> connIdsCopy;
            lock (recipientConnIds) { connIdsCopy = recipientConnIds.ToList(); }
            foreach (var connId in connIdsCopy)
            {
                await Clients.Client(connId).SendAsync("UserTypingDM", userId, username);
            }
        }
    }

    public async Task StopTypingDM(string recipientId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_userConnections.TryGetValue(recipientId, out var recipientConnIds))
        {
            List<string> connIdsCopy;
            lock (recipientConnIds) { connIdsCopy = recipientConnIds.ToList(); }
            foreach (var connId in connIdsCopy)
            {
                await Clients.Client(connId).SendAsync("UserStoppedTypingDM", userId);
            }
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
            if (_userConnections.TryGetValue(targetUserId, out var targetConnIds))
            {
                var targetFriends = _friendService.GetFriends(targetUserId);
                List<string> connIdsCopy;
                lock (targetConnIds) { connIdsCopy = targetConnIds.ToList(); }
                foreach (var connId in connIdsCopy)
                {
                    await Clients.Client(connId).SendAsync("FriendsList", targetFriends);
                }
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
            // Remove this connection from user's connection list
            bool userHasNoMoreConnections = false;
            if (_userConnections.TryGetValue(userId, out var connIds))
            {
                lock (connIds)
                {
                    connIds.Remove(connectionId);
                    if (connIds.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                        userHasNoMoreConnections = true;
                    }
                }
            }

            _logger.LogInformation("FriendHub connection disconnected for user: {UserId}, remaining connections: {HasMore}",
                userId, !userHasNoMoreConnections);

            // Only notify friends that user went offline if no more connections
            if (userHasNoMoreConnections)
            {
                var friends = _friendService.GetFriends(userId);
                foreach (var friend in friends)
                {
                    if (_userConnections.TryGetValue(friend.UserId, out var friendConnIds))
                    {
                        List<string> connIdsCopy;
                        lock (friendConnIds) { connIdsCopy = friendConnIds.ToList(); }
                        foreach (var connId in connIdsCopy)
                        {
                            await Clients.Client(connId).SendAsync("FriendOffline", userId);
                        }
                    }
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

    public Task<List<UserDto>> GetMutualFriends(string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return Task.FromResult(new List<UserDto>());

        var mutualFriends = _friendService.GetMutualFriends(userId, targetUserId);
        return Task.FromResult(mutualFriends);
    }
}
