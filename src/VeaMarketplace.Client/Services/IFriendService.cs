using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

public interface IFriendService
{
    bool IsConnected { get; }
    ObservableCollection<FriendDto> Friends { get; }
    ObservableCollection<FriendRequestDto> PendingRequests { get; }
    ObservableCollection<FriendRequestDto> OutgoingRequests { get; }
    ObservableCollection<ConversationDto> Conversations { get; }
    ObservableCollection<DirectMessageDto> CurrentDMHistory { get; }
    string? CurrentDMPartnerId { get; }

    event Action<FriendDto>? OnFriendOnline;
    event Action<string>? OnFriendOffline;
    event Action<FriendRequestDto>? OnNewFriendRequest;
    event Action<string>? OnFriendRequestAccepted;
    event Action<DirectMessageDto>? OnDirectMessageReceived;
    event Action<string>? OnUserTypingDM;
    event Action<string>? OnError;

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task SendFriendRequestAsync(string username);
    Task RespondToFriendRequestAsync(string requestId, bool accept);
    Task RemoveFriendAsync(string friendId);
    Task GetDMHistoryAsync(string partnerId);
    Task SendDirectMessageAsync(string recipientId, string content);
    Task MarkMessagesReadAsync(string partnerId);
    Task SendTypingDMAsync(string recipientId);
}

public class FriendService : IFriendService, IAsyncDisposable
{
    private HubConnection? _connection;
    private const string HubUrl = "http://162.248.94.23:5000/hubs/friends";

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public ObservableCollection<FriendDto> Friends { get; } = new();
    public ObservableCollection<FriendRequestDto> PendingRequests { get; } = new();
    public ObservableCollection<FriendRequestDto> OutgoingRequests { get; } = new();
    public ObservableCollection<ConversationDto> Conversations { get; } = new();
    public ObservableCollection<DirectMessageDto> CurrentDMHistory { get; } = new();
    public string? CurrentDMPartnerId { get; private set; }

    public event Action<FriendDto>? OnFriendOnline;
    public event Action<string>? OnFriendOffline;
    public event Action<FriendRequestDto>? OnNewFriendRequest;
    public event Action<string>? OnFriendRequestAccepted;
    public event Action<DirectMessageDto>? OnDirectMessageReceived;
    public event Action<string>? OnUserTypingDM;
    public event Action<string>? OnError;

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

        _connection.On<List<FriendDto>>("FriendsList", friends =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Friends.Clear();
                foreach (var friend in friends)
                    Friends.Add(friend);
            });
        });

        _connection.On<List<FriendRequestDto>>("PendingRequests", requests =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                PendingRequests.Clear();
                foreach (var request in requests)
                    PendingRequests.Add(request);
            });
        });

        _connection.On<List<FriendRequestDto>>("OutgoingRequests", requests =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OutgoingRequests.Clear();
                foreach (var request in requests)
                    OutgoingRequests.Add(request);
            });
        });

        _connection.On<List<ConversationDto>>("Conversations", conversations =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Conversations.Clear();
                foreach (var conv in conversations)
                    Conversations.Add(conv);
            });
        });

        _connection.On<string, List<DirectMessageDto>>("DMHistory", (partnerId, messages) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentDMPartnerId = partnerId;
                CurrentDMHistory.Clear();
                foreach (var msg in messages)
                    CurrentDMHistory.Add(msg);
            });
        });

        _connection.On<DirectMessageDto>("DirectMessageReceived", message =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // If we're in a conversation with this person, add the message
                if (CurrentDMPartnerId == message.SenderId || CurrentDMPartnerId == message.RecipientId)
                {
                    CurrentDMHistory.Add(message);
                }
                OnDirectMessageReceived?.Invoke(message);
            });
        });

        _connection.On<string, string>("FriendOnline", (userId, username) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var friend = Friends.FirstOrDefault(f => f.UserId == userId);
                if (friend != null)
                {
                    friend.IsOnline = true;
                    OnFriendOnline?.Invoke(friend);
                }
            });
        });

        _connection.On<string>("FriendOffline", userId =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var friend = Friends.FirstOrDefault(f => f.UserId == userId);
                if (friend != null)
                {
                    friend.IsOnline = false;
                    OnFriendOffline?.Invoke(userId);
                }
            });
        });

        _connection.On<FriendRequestDto>("NewFriendRequest", request =>
        {
            OnNewFriendRequest?.Invoke(request);
        });

        _connection.On<string>("FriendRequestAccepted", userId =>
        {
            OnFriendRequestAccepted?.Invoke(userId);
        });

        _connection.On<string>("UserTypingDM", userId =>
        {
            OnUserTypingDM?.Invoke(userId);
        });

        _connection.On<string>("FriendRequestError", error => OnError?.Invoke(error));
        _connection.On<string>("FriendError", error => OnError?.Invoke(error));
        _connection.On<string>("DMError", error => OnError?.Invoke(error));
    }

    public async Task SendFriendRequestAsync(string username)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendFriendRequest", username);
    }

    public async Task RespondToFriendRequestAsync(string requestId, bool accept)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("RespondToFriendRequest", requestId, accept);
    }

    public async Task RemoveFriendAsync(string friendId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("RemoveFriend", friendId);
    }

    public async Task GetDMHistoryAsync(string partnerId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("GetDMHistory", partnerId);
    }

    public async Task SendDirectMessageAsync(string recipientId, string content)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendDirectMessage", recipientId, content);
    }

    public async Task MarkMessagesReadAsync(string partnerId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("MarkMessagesRead", partnerId);
    }

    public async Task SendTypingDMAsync(string recipientId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("StartTypingDM", recipientId);
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

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
