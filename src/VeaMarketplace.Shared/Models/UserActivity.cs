namespace VeaMarketplace.Shared.Models;

public enum ActivityType
{
    MessageSent = 0,
    VoiceJoined = 1,
    VoiceLeft = 2,
    ProductListed = 3,
    ProductPurchased = 4,
    FriendAdded = 5,
    ProfileUpdated = 6,
    StatusChanged = 7,
    ScreenShareStarted = 8,
    ScreenShareStopped = 9,
    Other = 10
}

public class UserActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsPublic { get; set; } = true;
}

public class UserBadge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string BadgeId { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public bool IsDisplayed { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}

public class ProfileTheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#5865F2";
    public string SecondaryColor { get; set; } = "#202225";
    public string AccentColor { get; set; } = "#EB459E";
    public string BackgroundUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CustomStatus
{
    public string UserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
