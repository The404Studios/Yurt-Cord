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

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = Context.ConnectionId,
            ServerTime = DateTime.UtcNow,
            Hub = "NotificationHub"
        });

        await base.OnConnectedAsync();
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
            Icon = "System",
            ActionUrl = actionUrl,
            CreatedAt = DateTime.UtcNow
        };

        await hubContext.Clients.All.SendAsync("SystemNotification", notification);
    }

    /// <summary>
    /// Notify user of a new order
    /// </summary>
    public static async Task NotifyNewOrder(
        IHubContext<NotificationHub> hubContext,
        string sellerId,
        string buyerUsername,
        string productTitle,
        decimal amount,
        string orderId)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Order,
            Title = "New Order Received",
            Message = $"{buyerUsername} purchased '{productTitle}' for ${amount:F2}",
            Icon = "Order",
            ActionUrl = $"/orders/{orderId}",
            CreatedAt = DateTime.UtcNow
        };

        await SendNotificationToUser(hubContext, sellerId, notification);
    }

    /// <summary>
    /// Notify user of order status change
    /// </summary>
    public static async Task NotifyOrderStatusChange(
        IHubContext<NotificationHub> hubContext,
        string userId,
        string orderId,
        string productTitle,
        string newStatus)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Order,
            Title = "Order Update",
            Message = $"Order for '{productTitle}' is now {newStatus}",
            Icon = "Order",
            ActionUrl = $"/orders/{orderId}",
            CreatedAt = DateTime.UtcNow
        };

        await SendNotificationToUser(hubContext, userId, notification);
    }

    /// <summary>
    /// Notify user of a new review on their product
    /// </summary>
    public static async Task NotifyNewReview(
        IHubContext<NotificationHub> hubContext,
        string sellerId,
        string reviewerUsername,
        string productTitle,
        int rating,
        string productId)
    {
        var stars = new string('*', rating);
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Review,
            Title = "New Review",
            Message = $"{reviewerUsername} left a {rating}-star review on '{productTitle}'",
            Icon = "Review",
            ActionUrl = $"/products/{productId}",
            CreatedAt = DateTime.UtcNow
        };

        await SendNotificationToUser(hubContext, sellerId, notification);
    }

    /// <summary>
    /// Notify user of a new message
    /// </summary>
    public static async Task NotifyNewMessage(
        IHubContext<NotificationHub> hubContext,
        string userId,
        string senderUsername,
        string preview,
        string channelId)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.Message,
            Title = $"Message from {senderUsername}",
            Message = preview.Length > 100 ? preview[..97] + "..." : preview,
            Icon = "Message",
            ActionUrl = $"/chat/{channelId}",
            CreatedAt = DateTime.UtcNow
        };

        await SendNotificationToUser(hubContext, userId, notification);
    }

    /// <summary>
    /// Notify user of a friend request
    /// </summary>
    public static async Task NotifyFriendRequest(
        IHubContext<NotificationHub> hubContext,
        string userId,
        string requesterId,
        string requesterUsername)
    {
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid().ToString(),
            Type = NotificationType.FriendRequest,
            Title = "Friend Request",
            Message = $"{requesterUsername} sent you a friend request",
            Icon = "FriendRequest",
            ActionUrl = $"/friends",
            CreatedAt = DateTime.UtcNow
        };

        await SendNotificationToUser(hubContext, userId, notification);
    }

    /// <summary>
    /// Check if user is currently connected
    /// </summary>
    public static bool IsUserConnected(string userId)
    {
        return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
    }

    /// <summary>
    /// Get count of connected users
    /// </summary>
    public static int GetConnectedUserCount()
    {
        return _userConnections.Count;
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

    /// <summary>
    /// Heartbeat ping from client
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new
        {
            ServerTime = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        });
    }
}
