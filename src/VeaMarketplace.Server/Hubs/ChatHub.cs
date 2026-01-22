using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;
using System.Collections.Concurrent;

namespace VeaMarketplace.Server.Hubs;

public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly AuthService _authService;
    private readonly ILogger<ChatHub> _logger;
    private static readonly ConcurrentDictionary<string, OnlineUserDto> _onlineUsers = new();
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new();
    private static readonly ConcurrentDictionary<string, List<string>> _userConnectionsMap = new(); // userId -> list of connectionIds
    private static readonly ConcurrentDictionary<string, DateTime> _connectionTimestamps = new(); // For connection handshake tracking

    public ChatHub(ChatService chatService, AuthService authService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a new connection is established. Sends handshake confirmation.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _connectionTimestamps[connectionId] = DateTime.UtcNow;

        _logger.LogDebug("New connection established: {ConnectionId}", connectionId);

        // Send handshake confirmation with connection ID and server timestamp
        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = connectionId,
            ServerTime = DateTime.UtcNow,
            Message = "Connection established. Please authenticate."
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Updates a user's profile across all connected clients.
    /// Called when a user changes their avatar, banner, or other profile info.
    /// </summary>
    public async Task UpdateUserProfile(UpdateProfileRequest request)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        // Get fresh user data from database
        var user = _authService.GetUserById(userId);
        if (user == null) return;

        // Update the cached online user
        if (_onlineUsers.TryGetValue(userId, out var onlineUser))
        {
            onlineUser.AvatarUrl = user.AvatarUrl ?? onlineUser.AvatarUrl;
            onlineUser.BannerUrl = user.BannerUrl;
            onlineUser.Username = user.Username;
            onlineUser.DisplayName = user.DisplayName;
            onlineUser.StatusMessage = user.StatusMessage;
            onlineUser.Bio = user.Bio;
            onlineUser.AccentColor = user.AccentColor;
            onlineUser.LastUpdated = DateTime.UtcNow;

            // Broadcast the profile update to all connected clients
            await Clients.All.SendAsync("UserProfileUpdated", onlineUser);
        }
    }

    /// <summary>
    /// Static method to broadcast profile updates from other services (e.g., ProfileHub, REST API)
    /// </summary>
    public static async Task BroadcastProfileUpdate(IHubContext<ChatHub> hubContext, User user)
    {
        // Update cached user if online, otherwise create a temporary DTO for broadcast
        OnlineUserDto userDto;

        if (_onlineUsers.TryGetValue(user.Id, out var onlineUser))
        {
            // Update existing online user
            onlineUser.AvatarUrl = user.AvatarUrl ?? onlineUser.AvatarUrl;
            onlineUser.BannerUrl = user.BannerUrl;
            onlineUser.Username = user.Username;
            onlineUser.DisplayName = user.DisplayName;
            onlineUser.StatusMessage = user.StatusMessage;
            onlineUser.Bio = user.Bio;
            onlineUser.AccentColor = user.AccentColor;
            onlineUser.LastUpdated = DateTime.UtcNow;
            userDto = onlineUser;
        }
        else
        {
            // Create a DTO for broadcasting even if user isn't in chat
            userDto = new OnlineUserDto
            {
                Id = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                BannerUrl = user.BannerUrl,
                DisplayName = user.DisplayName,
                Role = user.Role,
                Rank = user.Rank,
                StatusMessage = user.StatusMessage,
                Bio = user.Bio,
                AccentColor = user.AccentColor,
                LastUpdated = DateTime.UtcNow
            };
        }

        // Always broadcast the update to all clients
        await hubContext.Clients.All.SendAsync("UserProfileUpdated", userDto);
    }

    public async Task Authenticate(string token)
    {
        var connectionId = Context.ConnectionId;

        // Validate connection handshake was received
        if (!_connectionTimestamps.TryGetValue(connectionId, out var connectionTime))
        {
            _logger.LogWarning("Authentication attempt without handshake: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("AuthenticationFailed", new
            {
                Error = "InvalidHandshake",
                Message = "Connection handshake not completed"
            });
            return;
        }

        // Check for stale connections (> 5 minutes without auth)
        if ((DateTime.UtcNow - connectionTime).TotalMinutes > 5)
        {
            _logger.LogWarning("Stale connection authentication attempt: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("AuthenticationFailed", new
            {
                Error = "ConnectionExpired",
                Message = "Connection expired. Please reconnect."
            });
            return;
        }

        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            _logger.LogDebug("Authentication failed for connection: {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("AuthenticationFailed", new
            {
                Error = "InvalidToken",
                Message = "Invalid or expired token"
            });
            return;
        }

        _logger.LogInformation("User authenticated: {Username} ({UserId})", user.Username, user.Id);

        var onlineUser = new OnlineUserDto
        {
            Id = user.Id,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl ?? string.Empty,
            BannerUrl = user.BannerUrl,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Rank = user.Rank,
            StatusMessage = user.StatusMessage,
            Bio = user.Bio,
            AccentColor = user.AccentColor,
            LastUpdated = DateTime.UtcNow
        };

        _onlineUsers[user.Id] = onlineUser;
        _connectionUserMap[connectionId] = user.Id;

        // Update database to mark user as online
        _authService.SetUserOnlineStatus(user.Id, true);

        // Track multiple connections per user
        _userConnectionsMap.AddOrUpdate(
            user.Id,
            _ => new List<string> { connectionId },
            (_, list) => { lock (list) { if (!list.Contains(connectionId)) list.Add(connectionId); } return list; }
        );

        await Groups.AddToGroupAsync(connectionId, "general");

        // Send channel list based on user role
        var channels = _chatService.GetChannels(user.Role);
        await Clients.Caller.SendAsync("ChannelList", channels);

        // Send online users
        await Clients.All.SendAsync("UserJoined", onlineUser);
        await Clients.Caller.SendAsync("OnlineUsers", _onlineUsers.Values.ToList());

        // Send chat history
        var history = _chatService.GetChannelHistory("general");
        await Clients.Caller.SendAsync("ChatHistory", "general", history);

        // Broadcast join message
        var joinMessage = _chatService.CreateSystemMessage("general", $"{user.Username} joined the chat", MessageType.Join);
        await Clients.Group("general").SendAsync("ReceiveMessage", joinMessage);

        // Send authentication success with confirmation details
        await Clients.Caller.SendAsync("AuthenticationSuccess", new
        {
            User = _authService.MapToDto(user),
            ConnectionId = connectionId,
            AuthenticatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid().ToString() // Unique session identifier
        });
    }

    public async Task JoinChannel(string channelName)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_onlineUsers.TryGetValue(userId, out var user))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, channelName);

        var history = _chatService.GetChannelHistory(channelName);
        await Clients.Caller.SendAsync("ChatHistory", channelName, history);

        var joinMessage = _chatService.CreateSystemMessage(channelName, $"{user.Username} joined #{channelName}", MessageType.Join);
        await Clients.Group(channelName).SendAsync("ReceiveMessage", joinMessage);
    }

    public async Task LeaveChannel(string channelName)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_onlineUsers.TryGetValue(userId, out var user))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelName);

        var leaveMessage = _chatService.CreateSystemMessage(channelName, $"{user.Username} left #{channelName}", MessageType.Leave);
        await Clients.Group(channelName).SendAsync("ReceiveMessage", leaveMessage);
    }

    public async Task SendMessage(string content, string channel = "general")
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (string.IsNullOrWhiteSpace(content))
            return;

        if (string.IsNullOrWhiteSpace(channel))
            channel = "general";

        var request = new SendMessageRequest
        {
            Content = content,
            Channel = channel
        };

        var message = _chatService.SaveMessage(userId, request);
        await Clients.Group(channel).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Send a message with file attachments
    /// </summary>
    public async Task SendMessageWithAttachments(string content, string channel, List<MessageAttachmentDto> attachments)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        // Validate inputs - at least content or attachments must be present
        if (string.IsNullOrWhiteSpace(content) && (attachments == null || attachments.Count == 0))
            return;

        if (string.IsNullOrWhiteSpace(channel))
            return;

        var request = new SendMessageRequest
        {
            Content = content ?? string.Empty,
            Channel = channel
        };

        var message = _chatService.SaveMessageWithAttachments(userId, request, attachments ?? new List<MessageAttachmentDto>());
        await Clients.Group(channel).SendAsync("ReceiveMessage", message);
    }

    public async Task DeleteMessage(string messageId, string channel = "general")
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_chatService.DeleteMessage(messageId, userId))
        {
            // Only notify users in the same channel, not all connected clients
            await Clients.Group(channel).SendAsync("MessageDeleted", messageId);
        }
    }

    /// <summary>
    /// Edit a message. Only the message owner can edit within a time limit.
    /// </summary>
    public async Task EditMessage(string messageId, string newContent, string channel = "general")
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (string.IsNullOrWhiteSpace(messageId))
        {
            await Clients.Caller.SendAsync("EditError", "Message ID is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            await Clients.Caller.SendAsync("EditError", "Message content is required");
            return;
        }

        var (success, message, updatedMessage) = _chatService.EditMessage(messageId, userId, newContent);

        if (success && updatedMessage != null)
        {
            // Notify all users in the channel about the edit
            await Clients.Group(channel).SendAsync("MessageEdited", updatedMessage);
        }
        else
        {
            await Clients.Caller.SendAsync("EditError", message);
        }
    }

    public async Task SendTyping(string channel)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_onlineUsers.TryGetValue(userId, out var user))
        {
            await Clients.OthersInGroup(channel).SendAsync("UserTyping", user.Username, channel);
        }
    }

    public async Task StopTyping(string channel)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_onlineUsers.TryGetValue(userId, out var user))
        {
            await Clients.OthersInGroup(channel).SendAsync("UserStoppedTyping", user.Username, channel);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        // Clean up connection timestamp
        _connectionTimestamps.TryRemove(connectionId, out _);

        if (_connectionUserMap.TryRemove(connectionId, out var userId))
        {
            // Remove this connection from user's connection list
            if (_userConnectionsMap.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    // Only remove user from online if no more connections
                    if (connections.Count > 0)
                    {
                        _logger.LogDebug("User {UserId} disconnected from {ConnectionId}, {Count} connections remaining",
                            userId, connectionId, connections.Count);
                        return;
                    }
                }
                _userConnectionsMap.TryRemove(userId, out _);
            }

            if (_onlineUsers.TryRemove(userId, out var user))
            {
                _logger.LogInformation("User disconnected: {Username} ({UserId})", user.Username, userId);

                // Update database to mark user as offline
                _authService.SetUserOnlineStatus(userId, false);

                await Clients.All.SendAsync("UserLeft", user);

                var leaveMessage = _chatService.CreateSystemMessage("general", $"{user.Username} left the chat", MessageType.Leave);
                await Clients.Group("general").SendAsync("ReceiveMessage", leaveMessage);
            }
        }
        else
        {
            _logger.LogDebug("Unauthenticated connection disconnected: {ConnectionId}", connectionId);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "Connection {ConnectionId} disconnected with error", connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<List<OnlineUserDto>> GetOnlineUsers()
    {
        return Task.FromResult(_onlineUsers.Values.ToList());
    }

    /// <summary>
    /// Heartbeat/ping method for connection health checking.
    /// Client should call this periodically to confirm connection is alive.
    /// </summary>
    public async Task Ping()
    {
        var connectionId = Context.ConnectionId;

        // Update timestamp to keep connection fresh
        _connectionTimestamps[connectionId] = DateTime.UtcNow;

        await Clients.Caller.SendAsync("Pong", new
        {
            ServerTime = DateTime.UtcNow,
            ConnectionId = connectionId
        });
    }

    /// <summary>
    /// Confirms receipt of a message. Used for reliable message delivery.
    /// </summary>
    public async Task AcknowledgeMessage(string messageId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        _logger.LogDebug("Message {MessageId} acknowledged by user {UserId}", messageId, userId);

        // Notify sender that message was received
        await Clients.Caller.SendAsync("MessageAcknowledged", new
        {
            MessageId = messageId,
            AcknowledgedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Adds a reaction (emoji) to a message
    /// </summary>
    public async Task AddReaction(string messageId, string emoji)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, channel, reaction) = _chatService.AddReaction(userId, messageId, emoji);

        if (success && reaction != null)
        {
            _logger.LogDebug("User {UserId} added reaction {Emoji} to message {MessageId}", userId, emoji, messageId);

            // Broadcast to everyone in the channel
            await Clients.Group(channel).SendAsync("ReactionAdded", new
            {
                MessageId = messageId,
                Reaction = reaction
            });
        }
    }

    /// <summary>
    /// Removes a reaction from a message
    /// </summary>
    public async Task RemoveReaction(string messageId, string emoji)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, channel) = _chatService.RemoveReaction(userId, messageId, emoji);

        if (success)
        {
            _logger.LogDebug("User {UserId} removed reaction {Emoji} from message {MessageId}", userId, emoji, messageId);

            // Broadcast to everyone in the channel
            await Clients.Group(channel).SendAsync("ReactionRemoved", new
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji
            });
        }
    }

    /// <summary>
    /// Creates a new group chat with specified members
    /// </summary>
    public async Task<string?> CreateGroupChat(CreateGroupChatRequest request)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return null;

        if (string.IsNullOrWhiteSpace(request.Name) || request.MemberIds.Count == 0)
        {
            await Clients.Caller.SendAsync("GroupChatError", "Invalid group chat parameters");
            return null;
        }

        // Create a unique group ID
        var groupId = $"group_{Guid.NewGuid():N}";

        _logger.LogInformation("User {UserId} created group chat {GroupId}: {Name} with {MemberCount} members",
            userId, groupId, request.Name, request.MemberIds.Count);

        // Add creator to the group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupId);

        // Notify all members about the new group
        foreach (var memberId in request.MemberIds)
        {
            if (_userConnectionsMap.TryGetValue(memberId, out var connections))
            {
                foreach (var connId in connections)
                {
                    await Groups.AddToGroupAsync(connId, groupId);
                    await Clients.Client(connId).SendAsync("GroupChatCreated", new
                    {
                        GroupId = groupId,
                        Name = request.Name,
                        CreatorId = userId,
                        MemberIds = request.MemberIds,
                        IconPath = request.IconPath,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Notify the creator
        await Clients.Caller.SendAsync("GroupChatCreated", new
        {
            GroupId = groupId,
            Name = request.Name,
            CreatorId = userId,
            MemberIds = request.MemberIds,
            IconPath = request.IconPath,
            CreatedAt = DateTime.UtcNow
        });

        return groupId;
    }

    #region Static Helpers for Console/Admin

    /// <summary>
    /// Get count of online users
    /// </summary>
    public static int GetOnlineUserCount()
    {
        return _onlineUsers.Count;
    }

    /// <summary>
    /// Get list of online user IDs
    /// </summary>
    public static IEnumerable<string> GetOnlineUserIds()
    {
        return _onlineUsers.Keys.ToList();
    }

    /// <summary>
    /// Check if a specific user is online
    /// </summary>
    public static bool IsUserOnline(string userId)
    {
        return _onlineUsers.ContainsKey(userId);
    }

    /// <summary>
    /// Get connection IDs for a user
    /// </summary>
    public static IEnumerable<string> GetUserConnections(string userId)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            return connections.ToList();
        }
        return Enumerable.Empty<string>();
    }

    #endregion
}
