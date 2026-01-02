using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

public interface IChatService
{
    bool IsConnected { get; }
    string? ConnectionId { get; }
    string? SessionId { get; }
    event Action<ChatMessageDto>? OnMessageReceived;
    event Action<OnlineUserDto>? OnUserJoined;
    event Action<OnlineUserDto>? OnUserLeft;
    event Action<List<OnlineUserDto>>? OnOnlineUsersReceived;
    event Action<string, List<ChatMessageDto>>? OnChatHistoryReceived;
    event Action<List<ChannelDto>>? OnChannelListReceived;
    event Action<string, string>? OnUserTyping;
    event Action<string>? OnMessageDeleted;
    event Action<UserDto>? OnAuthenticated;
    event Action<string>? OnAuthenticationFailed;
    event Action<OnlineUserDto>? OnUserProfileUpdated;
    event Action? OnConnectionHandshake;
    event Action<string, string, string, int>? OnReactionAdded; // MessageId, UserId, Emoji, Count
    event Action<string, string, string>? OnReactionRemoved; // MessageId, UserId, Emoji
    event Action<GroupChatCreatedEvent>? OnGroupChatCreated;
    event Action<string>? OnGroupChatError;

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task JoinChannelAsync(string channelName);
    Task LeaveChannelAsync(string channelName);
    Task SendMessageAsync(string content, string channel = "general");
    Task SendMessageWithAttachmentsAsync(string content, string channel, List<MessageAttachmentDto> attachments);
    Task DeleteMessageAsync(string messageId, string channel = "general");
    Task SendTypingAsync(string channel);
    Task UpdateProfileAsync(string? avatarUrl = null, string? bannerUrl = null);
    Task<string?> CreateGroupChatAsync(string name, List<string> memberIds, string? iconPath = null);
    Task AddReactionAsync(string messageId, string emoji);
    Task RemoveReactionAsync(string messageId, string emoji);
    Task AcknowledgeMessageAsync(string messageId);
}

public class ChatService : IChatService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly INotificationService _notificationService;
    private static readonly string HubUrl = AppConstants.Hubs.GetChatUrl();
    private string? _authToken;
    private System.Timers.Timer? _heartbeatTimer;
    private bool _handshakeReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public string? ConnectionId { get; private set; }
    public string? SessionId { get; private set; }

    public ChatService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public event Action<ChatMessageDto>? OnMessageReceived;
    public event Action<OnlineUserDto>? OnUserJoined;
    public event Action<OnlineUserDto>? OnUserLeft;
    public event Action<List<OnlineUserDto>>? OnOnlineUsersReceived;
    public event Action<string, List<ChatMessageDto>>? OnChatHistoryReceived;
    public event Action<List<ChannelDto>>? OnChannelListReceived;
    public event Action<string, string>? OnUserTyping;
    public event Action<string>? OnMessageDeleted;
    public event Action<UserDto>? OnAuthenticated;
    public event Action<string>? OnAuthenticationFailed;
    public event Action<OnlineUserDto>? OnUserProfileUpdated;
    public event Action? OnConnectionHandshake;
    public event Action<string, string, string, int>? OnReactionAdded;
    public event Action<string, string, string>? OnReactionRemoved;
    public event Action<GroupChatCreatedEvent>? OnGroupChatCreated;
    public event Action<string>? OnGroupChatError;

    public async Task ConnectAsync(string token)
    {
        _authToken = token;
        _handshakeReceived = false;
        ConnectionId = null;
        SessionId = null;

        // Dispose existing connection and timer if any
        StopHeartbeat();
        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                // Match server's JSON serialization for proper enum handling
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // Handle reconnection - re-authenticate when reconnected
        _connection.Reconnected += async (connectionId) =>
        {
            Debug.WriteLine($"ChatService: Reconnected with connectionId {connectionId}");
            _handshakeReceived = false;
            if (_authToken != null)
            {
                // Wait for handshake before authenticating
                await Task.Delay(100).ConfigureAwait(false);
                await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
            }
        };

        _connection.Closed += (exception) =>
        {
            Debug.WriteLine($"ChatService: Connection closed. Exception: {exception?.Message}");
            StopHeartbeat();
            return Task.CompletedTask;
        };

        RegisterHandlers();

        await _connection.StartAsync().ConfigureAwait(false);

        // Wait for handshake before authenticating (with timeout)
        var handshakeTimeout = DateTime.UtcNow.AddSeconds(5);
        while (!_handshakeReceived && DateTime.UtcNow < handshakeTimeout)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        if (!_handshakeReceived)
        {
            Debug.WriteLine("ChatService: Handshake timeout, proceeding with authentication anyway");
        }

        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatTimer = new System.Timers.Timer(30000); // 30 seconds
        _heartbeatTimer.Elapsed += async (s, e) => await SendHeartbeatAsync();
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
        Debug.WriteLine("ChatService: Heartbeat started");
    }

    private void StopHeartbeat()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
            Debug.WriteLine("ChatService: Heartbeat stopped");
        }
    }

    private async Task SendHeartbeatAsync()
    {
        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.InvokeAsync("Ping").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChatService: Heartbeat failed: {ex.Message}");
            }
        }
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Connection handshake from server
        _connection.On<JsonElement>("ConnectionHandshake", handshake =>
        {
            if (handshake.TryGetProperty("ConnectionId", out var connId))
            {
                ConnectionId = connId.GetString();
            }
            _handshakeReceived = true;
            Debug.WriteLine($"ChatService: Handshake received. ConnectionId: {ConnectionId}");
            OnConnectionHandshake?.Invoke();
        });

        // Heartbeat response
        _connection.On<JsonElement>("Pong", pong =>
        {
            Debug.WriteLine("ChatService: Pong received");
        });

        // Message acknowledgment
        _connection.On<JsonElement>("MessageAcknowledged", ack =>
        {
            if (ack.TryGetProperty("MessageId", out var msgId))
            {
                Debug.WriteLine($"ChatService: Message {msgId.GetString()} acknowledged");
            }
        });

        _connection.On<ChatMessageDto>("ReceiveMessage", message =>
            OnMessageReceived?.Invoke(message));

        _connection.On<OnlineUserDto>("UserJoined", user =>
        {
            _notificationService.PlayUserJoinSound();
            OnUserJoined?.Invoke(user);
        });

        _connection.On<OnlineUserDto>("UserLeft", user =>
        {
            _notificationService.PlayUserLeaveSound();
            OnUserLeft?.Invoke(user);
        });

        _connection.On<List<OnlineUserDto>>("OnlineUsers", users =>
            OnOnlineUsersReceived?.Invoke(users));

        _connection.On<string, List<ChatMessageDto>>("ChatHistory", (channel, messages) =>
            OnChatHistoryReceived?.Invoke(channel, messages));

        _connection.On<List<ChannelDto>>("ChannelList", channels =>
            OnChannelListReceived?.Invoke(channels));

        _connection.On<string, string>("UserTyping", (username, channel) =>
            OnUserTyping?.Invoke(username, channel));

        _connection.On<string>("MessageDeleted", messageId =>
            OnMessageDeleted?.Invoke(messageId));

        // Handle new authentication success format with session info
        _connection.On<JsonElement>("AuthenticationSuccess", response =>
        {
            try
            {
                // New format: { User, ConnectionId, AuthenticatedAt, SessionId }
                if (response.TryGetProperty("User", out var userElement))
                {
                    var user = JsonSerializer.Deserialize<UserDto>(userElement.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    });

                    if (response.TryGetProperty("SessionId", out var sessionId))
                    {
                        SessionId = sessionId.GetString();
                    }

                    Debug.WriteLine($"ChatService: Authenticated. SessionId: {SessionId}");
                    StartHeartbeat();

                    if (user != null)
                    {
                        OnAuthenticated?.Invoke(user);
                    }
                }
                else
                {
                    // Fallback: Old format where response is directly a UserDto
                    var user = JsonSerializer.Deserialize<UserDto>(response.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter() }
                    });

                    StartHeartbeat();

                    if (user != null)
                    {
                        OnAuthenticated?.Invoke(user);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChatService: Error parsing AuthenticationSuccess: {ex.Message}");
            }
        });

        // Handle new authentication failed format
        _connection.On<JsonElement>("AuthenticationFailed", response =>
        {
            string errorMessage;
            try
            {
                // New format: { Error, Message }
                if (response.TryGetProperty("Message", out var message))
                {
                    errorMessage = message.GetString() ?? "Authentication failed";
                }
                else
                {
                    // Fallback: Old format where response is just a string
                    errorMessage = response.GetString() ?? "Authentication failed";
                }
            }
            catch
            {
                errorMessage = response.ToString();
            }

            Debug.WriteLine($"ChatService: Authentication failed: {errorMessage}");
            OnAuthenticationFailed?.Invoke(errorMessage);
        });

        _connection.On<OnlineUserDto>("UserProfileUpdated", user =>
            OnUserProfileUpdated?.Invoke(user));

        // Reaction handlers
        _connection.On<JsonElement>("ReactionAdded", data =>
        {
            if (data.TryGetProperty("MessageId", out var msgId) &&
                data.TryGetProperty("Reaction", out var reaction))
            {
                var messageId = msgId.GetString() ?? "";
                var userId = reaction.TryGetProperty("UserId", out var uid) ? uid.GetString() ?? "" : "";
                var emoji = reaction.TryGetProperty("Emoji", out var em) ? em.GetString() ?? "" : "";
                var count = reaction.TryGetProperty("Count", out var cnt) ? cnt.GetInt32() : 1;
                OnReactionAdded?.Invoke(messageId, userId, emoji, count);
            }
        });

        _connection.On<JsonElement>("ReactionRemoved", data =>
        {
            if (data.TryGetProperty("MessageId", out var msgId) &&
                data.TryGetProperty("UserId", out var userId) &&
                data.TryGetProperty("Emoji", out var emoji))
            {
                OnReactionRemoved?.Invoke(
                    msgId.GetString() ?? "",
                    userId.GetString() ?? "",
                    emoji.GetString() ?? "");
            }
        });

        // Group chat handlers
        _connection.On<JsonElement>("GroupChatCreated", data =>
        {
            try
            {
                var evt = new GroupChatCreatedEvent
                {
                    GroupId = data.TryGetProperty("GroupId", out var gid) ? gid.GetString() ?? "" : "",
                    Name = data.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                    CreatorId = data.TryGetProperty("CreatorId", out var cid) ? cid.GetString() ?? "" : "",
                    IconPath = data.TryGetProperty("IconPath", out var icon) ? icon.GetString() : null,
                    CreatedAt = data.TryGetProperty("CreatedAt", out var cat) ? cat.GetDateTime() : DateTime.UtcNow
                };
                if (data.TryGetProperty("MemberIds", out var members))
                {
                    evt.MemberIds = JsonSerializer.Deserialize<List<string>>(members.GetRawText()) ?? [];
                }
                OnGroupChatCreated?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChatService: Error parsing GroupChatCreated: {ex.Message}");
            }
        });

        _connection.On<string>("GroupChatError", error =>
            OnGroupChatError?.Invoke(error));
    }

    public async Task DisconnectAsync()
    {
        StopHeartbeat();
        ConnectionId = null;
        SessionId = null;

        if (_connection != null)
        {
            await _connection.StopAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    public async Task JoinChannelAsync(string channelName)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("JoinChannel", channelName).ConfigureAwait(false);
    }

    public async Task LeaveChannelAsync(string channelName)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("LeaveChannel", channelName).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(string content, string channel = "general")
    {
        if (_connection == null) return;

        // Wait briefly for connection if reconnecting
        if (_connection.State == HubConnectionState.Reconnecting)
        {
            await Task.Delay(500).ConfigureAwait(false);
        }

        if (IsConnected)
        {
            await _connection.InvokeAsync("SendMessage", content, channel).ConfigureAwait(false);
        }
    }

    public async Task DeleteMessageAsync(string messageId, string channel = "general")
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("DeleteMessage", messageId, channel).ConfigureAwait(false);
    }

    public async Task SendTypingAsync(string channel)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendTyping", channel).ConfigureAwait(false);
    }

    public async Task SendMessageWithAttachmentsAsync(string content, string channel, List<MessageAttachmentDto> attachments)
    {
        if (_connection == null) return;

        // Wait briefly for connection if reconnecting
        if (_connection.State == HubConnectionState.Reconnecting)
        {
            await Task.Delay(500).ConfigureAwait(false);
        }

        if (IsConnected)
        {
            await _connection.InvokeAsync("SendMessageWithAttachments", content, channel, attachments).ConfigureAwait(false);
        }
    }

    public async Task UpdateProfileAsync(string? avatarUrl = null, string? bannerUrl = null)
    {
        if (_connection != null && IsConnected)
        {
            var request = new UpdateProfileRequest
            {
                AvatarUrl = avatarUrl,
                BannerUrl = bannerUrl
            };
            await _connection.InvokeAsync("UpdateUserProfile", request).ConfigureAwait(false);
        }
    }

    public async Task<string?> CreateGroupChatAsync(string name, List<string> memberIds, string? iconPath = null)
    {
        if (_connection == null || !IsConnected)
            return null;

        try
        {
            var request = new CreateGroupChatRequest
            {
                Name = name,
                MemberIds = memberIds,
                IconPath = iconPath
            };
            return await _connection.InvokeAsync<string>("CreateGroupChat", request).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task AddReactionAsync(string messageId, string emoji)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("AddReaction", messageId, emoji).ConfigureAwait(false);
        }
    }

    public async Task RemoveReactionAsync(string messageId, string emoji)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("RemoveReaction", messageId, emoji).ConfigureAwait(false);
        }
    }

    public async Task AcknowledgeMessageAsync(string messageId)
    {
        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.InvokeAsync("AcknowledgeMessage", messageId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChatService: Failed to acknowledge message {messageId}: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopHeartbeat();
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event data for when a group chat is created
/// </summary>
public class GroupChatCreatedEvent
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public string? IconPath { get; set; }
    public DateTime CreatedAt { get; set; }
}
