using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Hubs;

/// <summary>
/// Real-time hub for user notifications
/// </summary>
public class NotificationHub : Hub
{
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;

    // Track user connections
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new(); // userId -> connectionIds

    public NotificationHub(AuthService authService, NotificationService notificationService)
    {
        _authService = authService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Authenticate and initialize the notification hub connection
    /// </summary>
    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        // Track connection
        _connectionUserMap[Context.ConnectionId] = user.Id;

        if (!_userConnections.TryGetValue(user.Id, out var connections))
        {
            connections = new HashSet<string>();
            _userConnections[user.Id] = connections;
        }
        connections.Add(Context.ConnectionId);

        // Add to user's personal notification group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"notifications_{user.Id}");

        // Send initial unread count
        var unreadCount = _notificationService.GetUnreadCount(user.Id);
        await Clients.Caller.SendAsync("UnreadCount", unreadCount);

        await Clients.Caller.SendAsync("NotificationHubConnected");
    }

    /// <summary>
    /// Get notifications for the current user
    /// </summary>
    public async Task GetNotifications(bool unreadOnly = false, int page = 1, int pageSize = 50)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("Error", "Not authenticated");
            return;
        }

        var notifications = _notificationService.GetNotifications(userId, unreadOnly, page, pageSize);
        await Clients.Caller.SendAsync("NotificationList", notifications);
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    public async Task MarkAsRead(string notificationId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_notificationService.MarkAsRead(notificationId, userId))
        {
            var unreadCount = _notificationService.GetUnreadCount(userId);
            await Clients.Caller.SendAsync("NotificationRead", notificationId);
            await Clients.Caller.SendAsync("UnreadCount", unreadCount);
        }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    public async Task MarkAllAsRead()
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var count = _notificationService.MarkAllAsRead(userId);
        await Clients.Caller.SendAsync("AllNotificationsRead", count);
        await Clients.Caller.SendAsync("UnreadCount", 0);
    }

    /// <summary>
    /// Delete a notification
    /// </summary>
    public async Task DeleteNotification(string notificationId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_notificationService.DeleteNotification(notificationId, userId))
        {
            var unreadCount = _notificationService.GetUnreadCount(userId);
            await Clients.Caller.SendAsync("NotificationDeleted", notificationId);
            await Clients.Caller.SendAsync("UnreadCount", unreadCount);
        }
    }

    /// <summary>
    /// Broadcast a notification to a specific user
    /// </summary>
    public static async Task SendNotificationToUser(
        IHubContext<NotificationHub> hubContext,
        string userId,
        NotificationDto notification)
    {
        await hubContext.Clients.Group($"notifications_{userId}")
            .SendAsync("NewNotification", notification);

        // Also send updated unread count
        await hubContext.Clients.Group($"notifications_{userId}")
            .SendAsync("UnreadCountIncremented");
    }

    /// <summary>
    /// Broadcast a notification to multiple users
    /// </summary>
    public static async Task SendNotificationToUsers(
        IHubContext<NotificationHub> hubContext,
        IEnumerable<string> userIds,
        NotificationDto notification)
    {
        foreach (var userId in userIds)
        {
            await SendNotificationToUser(hubContext, userId, notification);
        }
    }

    /// <summary>
    /// Broadcast a system-wide notification
    /// </summary>
    public static async Task BroadcastSystemNotification(
        IHubContext<NotificationHub> hubContext,
        string title,
        string message,
        string? actionUrl = null)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.System,
            Title = title,
            Message = message,
            Icon = "📢",
            ActionUrl = actionUrl,
            CreatedAt = DateTime.UtcNow
        };

        await hubContext.Clients.All.SendAsync("SystemNotification", notification);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUserMap.TryRemove(Context.ConnectionId, out var userId))
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);

                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
