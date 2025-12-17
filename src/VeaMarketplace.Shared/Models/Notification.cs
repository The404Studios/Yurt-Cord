namespace VeaMarketplace.Shared.Models;

public enum NotificationType
{
    FriendRequest = 0,
    Message = 1,
    Mention = 2,
    ProductSold = 3,
    OrderUpdate = 4,
    Review = 5,
    System = 6,
    Moderation = 7,
    Achievement = 8,
    Other = 9,
    Order = 10,
    ProductUpdate = 11
}

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔔";
    public string? IconUrl { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class NotificationSettings
{
    public string UserId { get; set; } = string.Empty;
    public bool EnableDesktopNotifications { get; set; } = true;
    public bool EnableSoundNotifications { get; set; } = true;
    public bool EnableFriendRequests { get; set; } = true;
    public bool EnableMessages { get; set; } = true;
    public bool EnableMentions { get; set; } = true;
    public bool EnableProductUpdates { get; set; } = true;
    public bool EnableSystemNotifications { get; set; } = true;
    public bool DoNotDisturb { get; set; } = false;
    public DateTime? DoNotDisturbUntil { get; set; }
    public List<string> MutedUsers { get; set; } = new();
    public List<string> MutedChannels { get; set; } = new();
    public string? CustomSoundPath { get; set; }
}
