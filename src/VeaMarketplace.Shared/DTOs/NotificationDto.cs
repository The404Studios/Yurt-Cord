using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔔";
    public string? IconUrl { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class NotificationSettingsDto
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

public class MarkNotificationReadRequest
{
    public string NotificationId { get; set; } = string.Empty;
}
