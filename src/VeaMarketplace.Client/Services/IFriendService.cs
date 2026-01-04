using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using System.Windows.Threading;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

public interface IFriendService
{
    bool IsConnected { get; }
    string? ConnectionId { get; }
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
    event Action? OnAuthenticationSuccess;
    event Action<string>? OnFriendRequestResponded; // "accepted" or "declined"
    event Action<UserSearchResultDto?>? OnUserSearchResult;
    event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    event Action<FriendDto>? OnFriendProfileUpdated;
    event Action<FriendDto>? OnFriendRemoved;
    event Action<BlockedUserDto>? OnUserBlocked;
    event Action<string>? OnUserUnblocked;
    event Action? OnConversationsUpdated;
    event Action<string>? OnUserNoteUpdated;

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
    Task<List<FriendDto>> GetFriendsAsync();
    Task<List<UserDto>> GetMutualFriendsAsync(string userId);
    Task<FriendRelationship> GetRelationshipAsync(string userId);
    Task AcceptFriendRequestAsync(string userId);
    Task<string?> GetUserNoteAsync(string userId);
    Task SetUserNoteAsync(string userId, string note);
}

public class FriendService : IFriendService, IAsyncDisposable
{
    private HubConnection? _connection;
    private static readonly string HubUrl = AppConstants.Hubs.GetFriendsUrl();
    private DispatcherTimer? _typingTimer;
    private System.Timers.Timer? _heartbeatTimer;
    private string? _authToken;
    private bool _handshakeReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public string? ConnectionId { get; private set; }
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
    public event Action? OnAuthenticationSuccess;
    public event Action<string>? OnFriendRequestResponded;
    public event Action<UserSearchResultDto?>? OnUserSearchResult;
    public event Action<List<UserSearchResultDto>>? OnUserSearchResults;
    public event Action<FriendDto>? OnFriendProfileUpdated;
    public event Action<FriendDto>? OnFriendRemoved;
    public event Action<BlockedUserDto>? OnUserBlocked;
    public event Action<string>? OnUserUnblocked;
    public event Action? OnConversationsUpdated;
    public event Action<string>? OnUserNoteUpdated;

    public async Task ConnectAsync(string token)
    {
        _authToken = token;
        _handshakeReceived = false;
        ConnectionId = null;

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
            Debug.WriteLine($"FriendService: Reconnected with connectionId {connectionId}");
            _handshakeReceived = false;
            if (_authToken != null)
            {
                await Task.Delay(100).ConfigureAwait(false);
                await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
            }
        };

        _connection.Closed += (exception) =>
        {
            Debug.WriteLine($"FriendService: Connection closed. Exception: {exception?.Message}");
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

        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
        StartHeartbeat();
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatTimer = new System.Timers.Timer(30000); // 30 seconds
        _heartbeatTimer.Elapsed += async (s, e) => await SendHeartbeatAsync();
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
    }

    private void StopHeartbeat()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
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
                Debug.WriteLine($"FriendService: Heartbeat failed: {ex.Message}");
            }
        }
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Connection handshake from server
        _connection.On<JsonElement>("ConnectionHandshake", handshake =>
        {
            if (handshake.TryGetProperty(nameof(ConnectionId), out var connId))
            {
                ConnectionId = connId.GetString();
            }
            _handshakeReceived = true;
            Debug.WriteLine($"FriendService: Handshake received. ConnectionId: {ConnectionId}");
        });

        // Heartbeat response
        _connection.On<JsonElement>("Pong", pong =>
        {
            Debug.WriteLine("FriendService: Pong received");
        });

        // Handle new authentication failed format
        _connection.On<JsonElement>("AuthenticationFailed", response =>
        {
            string errorMessage;
            try
            {
                if (response.TryGetProperty("Message", out var message))
                {
                    errorMessage = message.GetString() ?? "Authentication failed";
                }
                else
                {
                    errorMessage = response.GetString() ?? "Authentication failed";
                }
            }
            catch
            {
                errorMessage = response.ToString();
            }
            Debug.WriteLine($"FriendService: Authentication failed: {errorMessage}");
            OnError?.Invoke(errorMessage);
        });

        // Authentication success
        _connection.On("AuthenticationSuccess", () =>
        {
            Debug.WriteLine("FriendService: Authentication succeeded");
            OnAuthenticationSuccess?.Invoke();
        });

        // Friend request response confirmation
        _connection.On<string>("FriendRequestResponded", response =>
        {
            Debug.WriteLine($"FriendService: Friend request responded: {response}");
            OnFriendRequestResponded?.Invoke(response);
        });

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

                // Apply message grouping - messages from the same sender within 5 minutes are grouped
                ApplyMessageGrouping(messages);

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
                    // Apply grouping for new message
                    var lastMessage = CurrentDMHistory.LastOrDefault();
                    message.IsFirstInGroup = ShouldStartNewGroup(lastMessage, message);
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

                // Auto-clear typing indicator after 3 seconds using DispatcherTimer (runs on UI thread)
                // Properly dispose old timer to avoid memory leaks
                DisposeTypingTimer();

                _typingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _typingTimer.Tick += OnTypingTimerTick;
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
        _connection.On<string>("FriendRequestSent", message => OnSuccess?.Invoke(message));

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

        // User note updated handler
        _connection.On<string>("UserNoteUpdated", userId =>
        {
            OnUserNoteUpdated?.Invoke(userId);
        });
    }

    public async Task SendFriendRequestAsync(string username)
    {
        if (_connection == null || !IsConnected)
            throw new InvalidOperationException("Not connected to friend service");
        await _connection.InvokeAsync("SendFriendRequest", username).ConfigureAwait(false);
    }

    public async Task SendFriendRequestByIdAsync(string userId)
    {
        if (_connection == null || !IsConnected)
            throw new InvalidOperationException("Not connected to friend service");
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

    public Task<List<FriendDto>> GetFriendsAsync()
    {
        // Return the current friends list - it's populated on connect via SignalR
        return Task.FromResult(Friends.ToList());
    }

    public async Task<List<UserDto>> GetMutualFriendsAsync(string userId)
    {
        if (_connection == null || !IsConnected)
            return [];

        try
        {
            var result = await _connection.InvokeAsync<List<UserDto>>("GetMutualFriends", userId).ConfigureAwait(false);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<FriendRelationship> GetRelationshipAsync(string userId)
    {
        if (_connection == null || !IsConnected)
            return FriendRelationship.None;

        try
        {
            // Check if user is a friend
            if (Friends.Any(f => f.UserId == userId))
                return FriendRelationship.Friends;

            // Check pending requests
            if (PendingRequests.Any(r => r.SenderId == userId))
                return FriendRelationship.PendingIncoming;

            if (OutgoingRequests.Any(r => r.RecipientId == userId))
                return FriendRelationship.PendingOutgoing;

            // Check blocked users
            if (BlockedUsers.Any(b => b.UserId == userId))
                return FriendRelationship.Blocked;

            return FriendRelationship.None;
        }
        catch
        {
            return FriendRelationship.None;
        }
    }

    public async Task AcceptFriendRequestAsync(string userId)
    {
        var request = PendingRequests.FirstOrDefault(r => r.SenderId == userId);
        if (request != null && _connection != null && IsConnected)
        {
            await _connection.InvokeAsync("RespondToFriendRequest", request.Id, true).ConfigureAwait(false);
        }
    }

    public async Task<string?> GetUserNoteAsync(string userId)
    {
        if (_connection == null || !IsConnected)
            return null;

        try
        {
            return await _connection.InvokeAsync<string?>("GetUserNote", userId).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetUserNoteAsync(string userId, string note)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("SetUserNote", userId, note).ConfigureAwait(false);
        }
    }

    #region Timer Management

    /// <summary>
    /// Properly disposes the typing timer to avoid memory leaks.
    /// </summary>
    private void DisposeTypingTimer()
    {
        if (_typingTimer != null)
        {
            _typingTimer.Stop();
            _typingTimer.Tick -= OnTypingTimerTick;
            _typingTimer = null;
        }
    }

    /// <summary>
    /// Handler for the typing timer tick event.
    /// </summary>
    private void OnTypingTimerTick(object? sender, EventArgs e)
    {
        var userId = TypingUserId;
        TypingUserId = null;
        TypingUsername = null;

        if (userId != null)
        {
            OnUserStoppedTypingDM?.Invoke(userId);
        }

        DisposeTypingTimer();
    }

    #endregion

    #region Message Grouping

    /// <summary>
    /// Applies message grouping to a list of messages.
    /// Messages from the same sender within 5 minutes are grouped together.
    /// </summary>
    private static void ApplyMessageGrouping(List<DirectMessageDto> messages)
    {
        if (messages == null || messages.Count == 0)
            return;

        for (int i = 0; i < messages.Count; i++)
        {
            if (i == 0)
            {
                messages[i].IsFirstInGroup = true;
            }
            else
            {
                messages[i].IsFirstInGroup = ShouldStartNewGroup(messages[i - 1], messages[i]);
            }
        }
    }

    /// <summary>
    /// Determines if a message should start a new group based on the previous message.
    /// A new group starts when:
    /// - The sender changes
    /// - More than 5 minutes have passed since the last message
    /// - The previous message is a reply
    /// </summary>
    private static bool ShouldStartNewGroup(DirectMessageDto? previous, DirectMessageDto current)
    {
        if (previous == null)
            return true;

        // Different sender = new group
        if (previous.SenderId != current.SenderId)
            return true;

        // More than 5 minutes apart = new group
        var timeDiff = current.Timestamp - previous.Timestamp;
        if (timeDiff.TotalMinutes > 5)
            return true;

        // Current message is a reply = new group
        if (current.IsReply)
            return true;

        return false;
    }

    #endregion

    public async Task DisconnectAsync()
    {
        StopHeartbeat();

        // Stop and dispose typing timer (properly unsubscribe from event)
        DisposeTypingTimer();

        ConnectionId = null;

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
