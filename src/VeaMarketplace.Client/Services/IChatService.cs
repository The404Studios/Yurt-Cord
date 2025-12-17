using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

public interface IChatService
{
    bool IsConnected { get; }
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
}

public class ChatService : IChatService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly INotificationService _notificationService;
    private const string HubUrl = "http://localhost:5000/hubs/chat";
    private string? _authToken;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

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

    public async Task ConnectAsync(string token)
    {
        _authToken = token;

        // Dispose existing connection if any
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
            if (_authToken != null)
            {
                await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
            }
        };

        RegisterHandlers();

        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

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

        _connection.On<UserDto>("AuthenticationSuccess", user =>
            OnAuthenticated?.Invoke(user));

        _connection.On<string>("AuthenticationFailed", error =>
            OnAuthenticationFailed?.Invoke(error));

        _connection.On<OnlineUserDto>("UserProfileUpdated", user =>
            OnUserProfileUpdated?.Invoke(user));
    }

    public async Task DisconnectAsync()
    {
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

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
