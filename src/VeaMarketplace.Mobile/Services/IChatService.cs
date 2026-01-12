using Microsoft.AspNetCore.SignalR.Client;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.Services;

public interface IChatService
{
    bool IsConnected { get; }
    event Action<MessageDto>? OnMessageReceived;
    event Action<string>? OnUserTyping;
    event Action? OnConnectionChanged;

    Task ConnectAsync();
    Task DisconnectAsync();
    Task JoinChannelAsync(string channelId);
    Task LeaveChannelAsync(string channelId);
    Task SendTypingIndicatorAsync(string channelId);
}

public class ChatService : IChatService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly IApiService _apiService;
    private string? _currentChannelId;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<MessageDto>? OnMessageReceived;
    public event Action<string>? OnUserTyping;
    public event Action? OnConnectionChanged;

    public ChatService(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task ConnectAsync()
    {
        if (_hubConnection != null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl("https://api.overseer.app/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_apiService.AuthToken);
            })
            .WithAutomaticReconnect()
            .Build();

        // Register handlers
        _hubConnection.On<MessageDto>("ReceiveMessage", message =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnMessageReceived?.Invoke(message));
        });

        _hubConnection.On<string, string>("UserTyping", (channelId, username) =>
        {
            if (channelId == _currentChannelId)
            {
                MainThread.BeginInvokeOnMainThread(() => OnUserTyping?.Invoke(username));
            }
        });

        _hubConnection.Reconnected += _ =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnConnectionChanged?.Invoke());
            return Task.CompletedTask;
        };

        _hubConnection.Closed += _ =>
        {
            MainThread.BeginInvokeOnMainThread(() => OnConnectionChanged?.Invoke());
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            OnConnectionChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignalR connection error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            OnConnectionChanged?.Invoke();
        }
    }

    public async Task JoinChannelAsync(string channelId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            if (_currentChannelId != null)
            {
                await _hubConnection.InvokeAsync("LeaveChannel", _currentChannelId);
            }
            await _hubConnection.InvokeAsync("JoinChannel", channelId);
            _currentChannelId = channelId;
        }
    }

    public async Task LeaveChannelAsync(string channelId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("LeaveChannel", channelId);
            if (_currentChannelId == channelId)
            {
                _currentChannelId = null;
            }
        }
    }

    public async Task SendTypingIndicatorAsync(string channelId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendTyping", channelId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
