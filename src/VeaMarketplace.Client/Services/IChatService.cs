using Microsoft.AspNetCore.SignalR.Client;
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

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task JoinChannelAsync(string channelName);
    Task LeaveChannelAsync(string channelName);
    Task SendMessageAsync(string content, string channel = "general");
    Task DeleteMessageAsync(string messageId);
    Task SendTypingAsync(string channel);
}

public class ChatService : IChatService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly INotificationService _notificationService;
    private const string HubUrl = "http://162.248.94.23:5000/hubs/chat";

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

    public async Task ConnectAsync(string token)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();

        await _connection.StartAsync();
        await _connection.InvokeAsync("Authenticate", token);
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
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async Task JoinChannelAsync(string channelName)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("JoinChannel", channelName);
    }

    public async Task LeaveChannelAsync(string channelName)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("LeaveChannel", channelName);
    }

    public async Task SendMessageAsync(string content, string channel = "general")
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendMessage", content, channel);
    }

    public async Task DeleteMessageAsync(string messageId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("DeleteMessage", messageId);
    }

    public async Task SendTypingAsync(string channel)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendTyping", channel);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
