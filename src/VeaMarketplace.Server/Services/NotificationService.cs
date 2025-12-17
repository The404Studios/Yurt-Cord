using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class NotificationService
{
    private readonly DatabaseService _db;

    public NotificationService(DatabaseService db)
    {
        _db = db;
    }

    public List<NotificationDto> GetNotifications(string userId, bool? unreadOnly = null, int page = 1, int pageSize = 50)
    {
        var query = _db.Notifications.Query()
            .Where(n => n.UserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        return query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    public int GetUnreadCount(string userId)
    {
        return _db.Notifications.Count(n => n.UserId == userId && !n.IsRead);
    }

    public NotificationDto? CreateNotification(
        string userId,
        NotificationType type,
        string title,
        string message,
        string? icon = null,
        string? iconUrl = null,
        string? actionUrl = null,
        Dictionary<string, string>? data = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Icon = icon ?? GetDefaultIcon(type),
            IconUrl = iconUrl,
            ActionUrl = actionUrl,
            Data = data ?? new(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Insert(notification);
        return MapToDto(notification);
    }

    public bool MarkAsRead(string notificationId, string userId)
    {
        var notification = _db.Notifications.FindById(notificationId);
        if (notification == null || notification.UserId != userId) return false;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        _db.Notifications.Update(notification);
        return true;
    }

    public int MarkAllAsRead(string userId)
    {
        var unread = _db.Notifications.Find(n => n.UserId == userId && !n.IsRead).ToList();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            _db.Notifications.Update(notification);
        }
        return unread.Count;
    }

    public bool DeleteNotification(string notificationId, string userId)
    {
        var notification = _db.Notifications.FindById(notificationId);
        if (notification == null || notification.UserId != userId) return false;

        _db.Notifications.Delete(notificationId);
        return true;
    }

    public int ClearAllNotifications(string userId)
    {
        var notifications = _db.Notifications.Find(n => n.UserId == userId).ToList();
        foreach (var notification in notifications)
        {
            _db.Notifications.Delete(notification.Id);
        }
        return notifications.Count;
    }

    // Helper methods to create specific notification types
    public void NotifyFriendRequest(string userId, string fromUsername, string fromUserId)
    {
        CreateNotification(
            userId,
            NotificationType.FriendRequest,
            "Friend Request",
            $"{fromUsername} sent you a friend request",
            actionUrl: $"vea://friends/requests",
            data: new Dictionary<string, string> { { "fromUserId", fromUserId } }
        );
    }

    public void NotifyNewMessage(string userId, string fromUsername, string channelId, string preview)
    {
        CreateNotification(
            userId,
            NotificationType.Message,
            $"Message from {fromUsername}",
            preview.Length > 100 ? preview[..97] + "..." : preview,
            actionUrl: $"vea://chat/{channelId}"
        );
    }

    public void NotifyMention(string userId, string fromUsername, string channelId, string context)
    {
        CreateNotification(
            userId,
            NotificationType.Mention,
            $"{fromUsername} mentioned you",
            context.Length > 100 ? context[..97] + "..." : context,
            actionUrl: $"vea://chat/{channelId}"
        );
    }

    public void NotifyProductSold(string sellerId, string productTitle, string buyerUsername, decimal amount)
    {
        CreateNotification(
            sellerId,
            NotificationType.ProductSold,
            "Product Sold!",
            $"{buyerUsername} purchased \"{productTitle}\" for ${amount:F2}",
            icon: "💰"
        );
    }

    public void NotifyOrderUpdate(string userId, string orderId, string status, string productTitle)
    {
        CreateNotification(
            userId,
            NotificationType.OrderUpdate,
            "Order Update",
            $"Your order for \"{productTitle}\" is now {status}",
            actionUrl: $"vea://orders/{orderId}"
        );
    }

    public void NotifyNewReview(string sellerId, string productTitle, int rating, string reviewerUsername)
    {
        CreateNotification(
            sellerId,
            NotificationType.Review,
            "New Review",
            $"{reviewerUsername} left a {rating}-star review on \"{productTitle}\"",
            icon: "⭐"
        );
    }

    public void NotifyModerationAction(string userId, string action, string? reason)
    {
        CreateNotification(
            userId,
            NotificationType.Moderation,
            "Moderation Notice",
            $"You have been {action}" + (reason != null ? $": {reason}" : ""),
            icon: "⚠️"
        );
    }

    public void NotifyPriceDrop(string userId, string productTitle, decimal oldPrice, decimal newPrice)
    {
        var percentOff = Math.Round((1 - newPrice / oldPrice) * 100);
        CreateNotification(
            userId,
            NotificationType.ProductUpdate,
            "Price Drop Alert",
            $"\"{productTitle}\" dropped from ${oldPrice:F2} to ${newPrice:F2} ({percentOff}% off)!",
            icon: "📉"
        );
    }

    private static string GetDefaultIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.FriendRequest => "👋",
            NotificationType.Message => "💬",
            NotificationType.Mention => "📢",
            NotificationType.ProductSold => "💰",
            NotificationType.OrderUpdate => "📦",
            NotificationType.Review => "⭐",
            NotificationType.System => "🔔",
            NotificationType.Moderation => "⚠️",
            NotificationType.Achievement => "🏆",
            NotificationType.ProductUpdate => "📉",
            _ => "🔔"
        };
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Icon = notification.Icon,
            IconUrl = notification.IconUrl,
            ActionUrl = notification.ActionUrl,
            Data = notification.Data,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }
}
