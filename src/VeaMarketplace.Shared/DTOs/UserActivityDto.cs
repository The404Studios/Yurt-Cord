namespace VeaMarketplace.Shared.DTOs;

public class UserActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public VeaMarketplace.Shared.Models.ActivityType Type { get; set; }
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime Timestamp => CreatedAt; // Alias for backward compatibility
    public bool IsPublic { get; set; } = true;
    public string Icon { get; set; } = "📝";
    public string ActionText { get; set; } = string.Empty;
}

public class UserBadgeDto
{
    public string Id { get; set; } = string.Empty;
    public string BadgeId { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime EarnedAt { get; set; }
    public bool IsDisplayed { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}

public class ProfileThemeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#FF6B00"; // Plugin orange
    public string SecondaryColor { get; set; } = "#202225";
    public string AccentColor { get; set; } = "#EB459E";
    public string BackgroundUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
}

public class UpdateProfileThemeRequest
{
    public string? ThemeId { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? BackgroundUrl { get; set; }
}

public class FollowStatusDto
{
    public bool IsFollowing { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
}
