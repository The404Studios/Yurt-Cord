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
    UserSearchResultDto? LastSearchResult { get; }
    ObservableCollection<UserSearchResultDto> SearchResults { get; }

    event Action<FriendDto>? OnFriendOnline;
    event Action<string>? OnFriendOffline;
    event Action<FriendRequestDto>? OnNewFriendRequest;
    event Action<string>? OnFriendRequestAccepted;
    event Action<DirectMessageDto>? OnDirectMessageReceived;
    event Action<string>? OnUserTypingDM;
    event Action<string>? OnError;
    event Action<UserSearchResultDto?>? OnUserSearchResult;
    event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    event Action<FriendDto>? OnFriendProfileUpdated;

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task SendFriendRequestAsync(string username);
    Task SendFriendRequestByIdAsync(string userId);
    Task RespondToFriendRequestAsync(string requestId, bool accept);
    Task RemoveFriendAsync(string friendId);
    Task GetDMHistoryAsync(string partnerId);
    Task SendDirectMessageAsync(string recipientId, string content);
    Task MarkMessagesReadAsync(string partnerId);
    Task SendTypingDMAsync(string recipientId);
    Task SearchUserAsync(string query);
    Task SearchUsersAsync(string query);
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
    public ObservableCollection<UserSearchResultDto> SearchResults { get; } = new();
    public string? CurrentDMPartnerId { get; private set; }
    public UserSearchResultDto? LastSearchResult { get; private set; }

    public event Action<FriendDto>? OnFriendOnline;
    public event Action<string>? OnFriendOffline;
    public event Action<FriendRequestDto>? OnNewFriendRequest;
    public event Action<string>? OnFriendRequestAccepted;
    public event Action<DirectMessageDto>? OnDirectMessageReceived;
    public event Action<string>? OnUserTypingDM;
    public event Action<string>? OnError;
    public event Action<UserSearchResultDto?>? OnUserSearchResult;
    public event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    public event Action<FriendDto>? OnFriendProfileUpdated;

    public async Task ConnectAsync(string token)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();
        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
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

        // User search handlers
        _connection.On<UserSearchResultDto?>("UserSearchResult", result =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                LastSearchResult = result;
                OnUserSearchResult?.Invoke(result);
            });
        });

        _connection.On<List<UserSearchResultDto>>("UserSearchResults", results =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                foreach (var result in results)
                    SearchResults.Add(result);
                OnUserSearchResults?.Invoke(results);
            });
        });

        // Friend profile update handler
        _connection.On<FriendDto>("FriendProfileUpdated", friend =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = Friends.FirstOrDefault(f => f.UserId == friend.UserId);
                if (existing != null)
                {
                    var index = Friends.IndexOf(existing);
                    Friends[index] = friend;
                }
                OnFriendProfileUpdated?.Invoke(friend);
            });
        });
    }

    public async Task SendFriendRequestAsync(string username)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendFriendRequest", username).ConfigureAwait(false);
    }

    public async Task SendFriendRequestByIdAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendFriendRequestById", userId).ConfigureAwait(false);
    }

    public async Task SearchUserAsync(string query)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SearchUser", query).ConfigureAwait(false);
    }

    public async Task SearchUsersAsync(string query)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SearchUsers", query).ConfigureAwait(false);
    }

    public async Task RespondToFriendRequestAsync(string requestId, bool accept)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("RespondToFriendRequest", requestId, accept).ConfigureAwait(false);
    }

    public async Task RemoveFriendAsync(string friendId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("RemoveFriend", friendId).ConfigureAwait(false);
    }

    public async Task GetDMHistoryAsync(string partnerId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("GetDMHistory", partnerId).ConfigureAwait(false);
    }

    public async Task SendDirectMessageAsync(string recipientId, string content)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SendDirectMessage", recipientId, content).ConfigureAwait(false);
    }

    public async Task MarkMessagesReadAsync(string partnerId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("MarkMessagesRead", partnerId).ConfigureAwait(false);
    }

    public async Task SendTypingDMAsync(string recipientId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("StartTypingDM", recipientId).ConfigureAwait(false);
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

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
