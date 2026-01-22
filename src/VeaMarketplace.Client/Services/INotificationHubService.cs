using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Interface for real-time notification hub service
/// </summary>
public interface INotificationHubService
{
    bool IsConnected { get; }
    int UnreadCount { get; }
    ObservableCollection<NotificationDto> Notifications { get; }

    // Events
    event Action<NotificationDto>? OnNewNotification;
    event Action<NotificationDto>? OnSystemNotification;
    event Action<int>? OnUnreadCountChanged;
    event Action<string>? OnNotificationRead;
    event Action<int>? OnAllNotificationsRead;
    event Action<string>? OnNotificationDeleted;
    event Action? OnConnected;
    event Action<string>? OnError;

    // Connection
    Task ConnectAsync(string token);
    Task DisconnectAsync();

    // Operations
    Task GetNotificationsAsync(bool unreadOnly = false, int page = 1, int pageSize = 50);
    Task MarkAsReadAsync(string notificationId);
    Task MarkAllAsReadAsync();
    Task DeleteNotificationAsync(string notificationId);
}

/// <summary>
/// Real-time notification service for receiving notifications via SignalR
/// </summary>
public class NotificationHubService : INotificationHubService, IAsyncDisposable
{
    private HubConnection? _connection;
    private static readonly string HubUrl = AppConstants.Hubs.GetNotificationsUrl();
    private string? _authToken;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public int UnreadCount { get; private set; }
    public ObservableCollection<NotificationDto> Notifications { get; } = new();

    // Events
    public event Action<NotificationDto>? OnNewNotification;
    public event Action<NotificationDto>? OnSystemNotification;
    public event Action<int>? OnUnreadCountChanged;
    public event Action<string>? OnNotificationRead;
    public event Action<int>? OnAllNotificationsRead;
    public event Action<string>? OnNotificationDeleted;
    public event Action? OnConnected;
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
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // Handle reconnection
        _connection.Reconnected += async (connectionId) =>
        {
            try
            {
                Debug.WriteLine($"NotificationHubService: Reconnected with connectionId {connectionId}");
                if (_authToken != null)
                {
                    await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotificationHubService: Failed to re-authenticate after reconnection: {ex.Message}");
            }
        };

        _connection.Closed += (exception) =>
        {
            Debug.WriteLine($"NotificationHubService: Connection closed. Exception: {exception?.Message}");
            return Task.CompletedTask;
        };

        RegisterHandlers();
        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Connection established
        _connection.On("NotificationHubConnected", () =>
        {
            Debug.WriteLine("NotificationHubService: Connected");
            OnConnected?.Invoke();
        });

        // Authentication failed
        _connection.On<string>("AuthenticationFailed", error =>
        {
            Debug.WriteLine($"NotificationHubService: Authentication failed: {error}");
            OnError?.Invoke(error);
        });

        // Error handler
        _connection.On<string>("Error", error =>
        {
            Debug.WriteLine($"NotificationHubService: Error: {error}");
            OnError?.Invoke(error);
        });

        // Unread count
        _connection.On<int>("UnreadCount", count =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UnreadCount = count;
                OnUnreadCountChanged?.Invoke(count);
            });
        });

        // Unread count incremented
        _connection.On("UnreadCountIncremented", () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UnreadCount++;
                OnUnreadCountChanged?.Invoke(UnreadCount);
            });
        });

        // Notification list
        _connection.On<List<NotificationDto>>("NotificationList", notifications =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Notifications.Clear();
                foreach (var notification in notifications)
                {
                    Notifications.Add(notification);
                }
            });
        });

        // New notification
        _connection.On<NotificationDto>("NewNotification", notification =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Notifications.Insert(0, notification);
                OnNewNotification?.Invoke(notification);
            });
        });

        // System notification
        _connection.On<NotificationDto>("SystemNotification", notification =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnSystemNotification?.Invoke(notification);
            });
        });

        // Notification read
        _connection.On<string>("NotificationRead", notificationId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var notification = Notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                }
                OnNotificationRead?.Invoke(notificationId);
            });
        });

        // All notifications read
        _connection.On<int>("AllNotificationsRead", count =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                foreach (var notification in Notifications)
                {
                    notification.IsRead = true;
                }
                OnAllNotificationsRead?.Invoke(count);
            });
        });

        // Notification deleted
        _connection.On<string>("NotificationDeleted", notificationId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var notification = Notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    Notifications.Remove(notification);
                }
                OnNotificationDeleted?.Invoke(notificationId);
            });
        });
    }

    public async Task GetNotificationsAsync(bool unreadOnly = false, int page = 1, int pageSize = 50)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("GetNotifications", unreadOnly, page, pageSize).ConfigureAwait(false);
        }
    }

    public async Task MarkAsReadAsync(string notificationId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("MarkAsRead", notificationId).ConfigureAwait(false);
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("MarkAllAsRead").ConfigureAwait(false);
        }
    }

    public async Task DeleteNotificationAsync(string notificationId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("DeleteNotification", notificationId).ConfigureAwait(false);
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
