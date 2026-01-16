using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

public interface IProfileService
{
    bool IsConnected { get; }
    UserDto? CurrentProfile { get; }
    ObservableCollection<UserDto> OnlineUsers { get; }

    event Action<UserDto>? OnProfileLoaded;
    event Action<UserDto>? OnProfileUpdated;
    event Action<UserDto>? OnUserOnline;
    event Action<string, string>? OnUserOffline;
    event Action<UserDto>? OnUserProfileLoaded;
    event Action<UserDto>? OnUserProfileUpdated;
    event Action<string>? OnError;

    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task GetUserProfileAsync(string userId);
    Task UpdateProfileAsync(UpdateProfileRequest request);
    Task GetOnlineUsersAsync();
    Task SubscribeToProfileAsync(string userId);
    Task UnsubscribeFromProfileAsync(string userId);
}

public class ProfileService : IProfileService, IAsyncDisposable
{
    private HubConnection? _connection;
    private static readonly string HubUrl = AppConstants.Hubs.GetProfileUrl();
    private string? _authToken;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public UserDto? CurrentProfile { get; private set; }
    public ObservableCollection<UserDto> OnlineUsers { get; } = new();

    public event Action<UserDto>? OnProfileLoaded;
    public event Action<UserDto>? OnProfileUpdated;
    public event Action<UserDto>? OnUserOnline;
    public event Action<string, string>? OnUserOffline;
    public event Action<UserDto>? OnUserProfileLoaded;
    public event Action<UserDto>? OnUserProfileUpdated;
    public event Action<string>? OnError;

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

        // Handle connection closed
        _connection.Closed += (exception) =>
        {
            System.Diagnostics.Debug.WriteLine($"ProfileService: Connection closed. Exception: {exception?.Message}");
            return Task.CompletedTask;
        };

        RegisterHandlers();
        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Own profile loaded on connect
        _connection.On<UserDto>("ProfileLoaded", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentProfile = profile;
                OnProfileLoaded?.Invoke(profile);
            });
        });

        // Own profile updated
        _connection.On<UserDto>("ProfileUpdated", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentProfile = profile;
                OnProfileUpdated?.Invoke(profile);
            });
        });

        // Another user's profile loaded (when viewing their profile)
        _connection.On<UserDto>("UserProfileLoaded", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnUserProfileLoaded?.Invoke(profile);
            });
        });

        // Any user updated their profile
        _connection.On<UserDto>("UserProfileUpdated", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Update in online users list
                var existing = OnlineUsers.FirstOrDefault(u => u.Id == profile.Id);
                if (existing != null)
                {
                    var index = OnlineUsers.IndexOf(existing);
                    OnlineUsers[index] = profile;
                }
                OnUserProfileUpdated?.Invoke(profile);
            });
        });

        // Friend updated their profile
        _connection.On<UserDto>("FriendProfileUpdated", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnUserProfileUpdated?.Invoke(profile);
            });
        });

        // User came online
        _connection.On<UserDto>("UserOnline", user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (!OnlineUsers.Any(u => u.Id == user.Id))
                {
                    OnlineUsers.Add(user);
                }
                OnUserOnline?.Invoke(user);
            });
        });

        // User went offline
        _connection.On<string, string>("UserOffline", (userId, username) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var user = OnlineUsers.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    OnlineUsers.Remove(user);
                }
                OnUserOffline?.Invoke(userId, username);
            });
        });

        // Online users list
        _connection.On<List<UserDto>>("OnlineUsersList", users =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnlineUsers.Clear();
                foreach (var user in users)
                    OnlineUsers.Add(user);
            });
        });

        // Error handling
        _connection.On<string>("ProfileError", error => OnError?.Invoke(error));
        _connection.On<string>("AuthenticationFailed", error => OnError?.Invoke(error));
    }

    public async Task GetUserProfileAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("GetUserProfile", userId).ConfigureAwait(false);
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UpdateProfile", request).ConfigureAwait(false);
    }

    public async Task GetOnlineUsersAsync()
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("GetOnlineUsers").ConfigureAwait(false);
    }

    public async Task SubscribeToProfileAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SubscribeToProfile", userId).ConfigureAwait(false);
    }

    public async Task UnsubscribeFromProfileAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UnsubscribeFromProfile", userId).ConfigureAwait(false);
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
