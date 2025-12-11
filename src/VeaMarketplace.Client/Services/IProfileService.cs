using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
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
    private const string HubUrl = "http://162.248.94.23:5000/hubs/profile";

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

        // Own profile loaded on connect
        _connection.On<UserDto>("ProfileLoaded", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentProfile = profile;
                OnProfileLoaded?.Invoke(profile);
            });
        });

        // Own profile updated
        _connection.On<UserDto>("ProfileUpdated", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentProfile = profile;
                OnProfileUpdated?.Invoke(profile);
            });
        });

        // Another user's profile loaded (when viewing their profile)
        _connection.On<UserDto>("UserProfileLoaded", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnUserProfileLoaded?.Invoke(profile);
            });
        });

        // Any user updated their profile
        _connection.On<UserDto>("UserProfileUpdated", profile =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnUserProfileUpdated?.Invoke(profile);
            });
        });

        // User came online
        _connection.On<UserDto>("UserOnline", user =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
