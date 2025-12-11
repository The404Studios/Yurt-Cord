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
    ObservableCollection<BlockedUserDto> BlockedUsers { get; }
    string? CurrentDMPartnerId { get; }
    UserSearchResultDto? LastSearchResult { get; }
    ObservableCollection<UserSearchResultDto> SearchResults { get; }
    string? TypingUserId { get; }
    string? TypingUsername { get; }

    event Action<FriendDto>? OnFriendOnline;
    event Action<string>? OnFriendOffline;
    event Action<FriendRequestDto>? OnNewFriendRequest;
    event Action<string>? OnFriendRequestAccepted;
    event Action<string>? OnFriendRequestDeclined;
    event Action<string>? OnFriendRequestCancelled;
    event Action<DirectMessageDto>? OnDirectMessageReceived;
    event Action<string, string>? OnUserTypingDM;
    event Action<string>? OnUserStoppedTypingDM;
    event Action<string>? OnError;
    event Action<string>? OnSuccess;
    event Action<UserSearchResultDto?>? OnUserSearchResult;
    event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    event Action<FriendDto>? OnFriendProfileUpdated;
    event Action<FriendDto>? OnFriendRemoved;
    event Action<BlockedUserDto>? OnUserBlocked;
    event Action<string>? OnUserUnblocked;
    event Action? OnConversationsUpdated;

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task SendFriendRequestAsync(string username);
    Task SendFriendRequestByIdAsync(string userId);
    Task RespondToFriendRequestAsync(string requestId, bool accept);
    Task CancelFriendRequestAsync(string requestId);
    Task RemoveFriendAsync(string friendId);
    Task BlockUserAsync(string userId, string? reason = null);
    Task UnblockUserAsync(string userId);
    Task GetBlockedUsersAsync();
    Task GetDMHistoryAsync(string partnerId);
    Task SendDirectMessageAsync(string recipientId, string content);
    Task MarkMessagesReadAsync(string partnerId);
    Task SendTypingDMAsync(string recipientId);
    Task StopTypingDMAsync(string recipientId);
    Task SearchUserAsync(string query);
    Task SearchUsersAsync(string query);
    Task RefreshConversationsAsync();
}

public class FriendService : IFriendService, IAsyncDisposable
{
    private HubConnection? _connection;
    private const string HubUrl = "http://162.248.94.23:5000/hubs/friends";
    private System.Timers.Timer? _typingTimer;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public ObservableCollection<FriendDto> Friends { get; } = new();
    public ObservableCollection<FriendRequestDto> PendingRequests { get; } = new();
    public ObservableCollection<FriendRequestDto> OutgoingRequests { get; } = new();
    public ObservableCollection<ConversationDto> Conversations { get; } = new();
    public ObservableCollection<DirectMessageDto> CurrentDMHistory { get; } = new();
    public ObservableCollection<UserSearchResultDto> SearchResults { get; } = new();
    public ObservableCollection<BlockedUserDto> BlockedUsers { get; } = new();
    public string? CurrentDMPartnerId { get; private set; }
    public UserSearchResultDto? LastSearchResult { get; private set; }
    public string? TypingUserId { get; private set; }
    public string? TypingUsername { get; private set; }

    public event Action<FriendDto>? OnFriendOnline;
    public event Action<string>? OnFriendOffline;
    public event Action<FriendRequestDto>? OnNewFriendRequest;
    public event Action<string>? OnFriendRequestAccepted;
    public event Action<string>? OnFriendRequestDeclined;
    public event Action<string>? OnFriendRequestCancelled;
    public event Action<DirectMessageDto>? OnDirectMessageReceived;
    public event Action<string, string>? OnUserTypingDM;
    public event Action<string>? OnUserStoppedTypingDM;
    public event Action<string>? OnError;
    public event Action<string>? OnSuccess;
    public event Action<UserSearchResultDto?>? OnUserSearchResult;
    public event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    public event Action<FriendDto>? OnFriendProfileUpdated;
    public event Action<FriendDto>? OnFriendRemoved;
    public event Action<BlockedUserDto>? OnUserBlocked;
    public event Action<string>? OnUserUnblocked;
    public event Action? OnConversationsUpdated;

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
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Friends.Clear();
                foreach (var friend in friends)
                    Friends.Add(friend);
            });
        });

        _connection.On<List<FriendRequestDto>>("PendingRequests", requests =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                PendingRequests.Clear();
                foreach (var request in requests)
                    PendingRequests.Add(request);
            });
        });

        _connection.On<List<FriendRequestDto>>("OutgoingRequests", requests =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OutgoingRequests.Clear();
                foreach (var request in requests)
                    OutgoingRequests.Add(request);
            });
        });

        _connection.On<List<ConversationDto>>("Conversations", conversations =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Conversations.Clear();
                foreach (var conv in conversations)
                    Conversations.Add(conv);
            });
        });

        _connection.On<string, List<DirectMessageDto>>("DMHistory", (partnerId, messages) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentDMPartnerId = partnerId;
                CurrentDMHistory.Clear();
                foreach (var msg in messages)
                    CurrentDMHistory.Add(msg);
            });
        });

        _connection.On<DirectMessageDto>("DirectMessageReceived", message =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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

        _connection.On<string, string>("UserTypingDM", (userId, username) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                TypingUserId = userId;
                TypingUsername = username;
                OnUserTypingDM?.Invoke(userId, username);

                // Auto-clear typing indicator after 3 seconds
                _typingTimer?.Stop();
                _typingTimer?.Dispose();
                _typingTimer = new System.Timers.Timer(3000);
                _typingTimer.Elapsed += (s, e) =>
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        TypingUserId = null;
                        TypingUsername = null;
                        OnUserStoppedTypingDM?.Invoke(userId);
                    });
                    _typingTimer?.Stop();
                };
                _typingTimer.AutoReset = false;
                _typingTimer.Start();
            });
        });

        _connection.On<string>("UserStoppedTypingDM", userId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (TypingUserId == userId)
                {
                    TypingUserId = null;
                    TypingUsername = null;
                }
                OnUserStoppedTypingDM?.Invoke(userId);
            });
        });

        _connection.On<string>("FriendRequestDeclined", userId =>
        {
            OnFriendRequestDeclined?.Invoke(userId);
        });

        _connection.On<string>("FriendRequestCancelled", requestId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var request = PendingRequests.FirstOrDefault(r => r.Id == requestId);
                if (request != null)
                    PendingRequests.Remove(request);
            });
            OnFriendRequestCancelled?.Invoke(requestId);
        });

        _connection.On<FriendDto>("FriendRemoved", friend =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var existing = Friends.FirstOrDefault(f => f.UserId == friend.UserId);
                if (existing != null)
                    Friends.Remove(existing);
            });
            OnFriendRemoved?.Invoke(friend);
        });

        _connection.On<List<BlockedUserDto>>("BlockedUsers", users =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                BlockedUsers.Clear();
                foreach (var user in users)
                    BlockedUsers.Add(user);
            });
        });

        _connection.On<BlockedUserDto>("UserBlocked", user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                BlockedUsers.Add(user);
                // Remove from friends if blocked
                var friend = Friends.FirstOrDefault(f => f.UserId == user.UserId);
                if (friend != null)
                    Friends.Remove(friend);
            });
            OnUserBlocked?.Invoke(user);
        });

        _connection.On<string>("UserUnblocked", userId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var user = BlockedUsers.FirstOrDefault(u => u.UserId == userId);
                if (user != null)
                    BlockedUsers.Remove(user);
            });
            OnUserUnblocked?.Invoke(userId);
        });

        _connection.On<string>("FriendRequestError", error => OnError?.Invoke(error));
        _connection.On<string>("FriendError", error => OnError?.Invoke(error));
        _connection.On<string>("DMError", error => OnError?.Invoke(error));
        _connection.On<string>("BlockError", error => OnError?.Invoke(error));
        _connection.On<string>("Success", message => OnSuccess?.Invoke(message));

        // User search handlers
        _connection.On<UserSearchResultDto?>("UserSearchResult", result =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                LastSearchResult = result;
                OnUserSearchResult?.Invoke(result);
            });
        });

        _connection.On<List<UserSearchResultDto>>("UserSearchResults", results =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
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

    public async Task CancelFriendRequestAsync(string requestId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("CancelFriendRequest", requestId).ConfigureAwait(false);
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var request = OutgoingRequests.FirstOrDefault(r => r.Id == requestId);
                if (request != null)
                    OutgoingRequests.Remove(request);
            });
        }
    }

    public async Task RemoveFriendAsync(string friendId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("RemoveFriend", friendId).ConfigureAwait(false);
    }

    public async Task BlockUserAsync(string userId, string? reason = null)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("BlockUser", userId, reason).ConfigureAwait(false);
    }

    public async Task UnblockUserAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UnblockUser", userId).ConfigureAwait(false);
    }

    public async Task GetBlockedUsersAsync()
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("GetBlockedUsers").ConfigureAwait(false);
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

    public async Task StopTypingDMAsync(string recipientId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("StopTypingDM", recipientId).ConfigureAwait(false);
    }

    public async Task RefreshConversationsAsync()
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("GetConversations").ConfigureAwait(false);
            OnConversationsUpdated?.Invoke();
        }
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
