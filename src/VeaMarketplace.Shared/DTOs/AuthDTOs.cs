using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#5865F2";
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public int Reputation { get; set; }
    public int TotalSales { get; set; }
    public int TotalPurchases { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public List<string> Badges { get; set; } = new();
    public List<CustomRoleDto> CustomRoles { get; set; } = new();

    // Social Links
    public string? DiscordUsername { get; set; }
    public string? TwitterHandle { get; set; }
    public string? TelegramUsername { get; set; }
    public string? WebsiteUrl { get; set; }

    // Helper to get display name or username
    public string GetDisplayName() => string.IsNullOrEmpty(DisplayName) ? Username : DisplayName;
}

public class UpdateProfileRequest
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? Description { get; set; }
    public string? StatusMessage { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? AccentColor { get; set; }
    public ProfileVisibility? ProfileVisibility { get; set; }

    // Social Links
    public string? DiscordUsername { get; set; }
    public string? TwitterHandle { get; set; }
    public string? TelegramUsername { get; set; }
    public string? WebsiteUrl { get; set; }
}

public class CustomRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public int Position { get; set; }
    public bool IsHoisted { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class OnlineUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public string? StatusMessage { get; set; }
    public string? Bio { get; set; }
    public string? AccentColor { get; set; }
}

public enum ActivityType
{
    None,
    Playing,
    Streaming,
    Listening,
    Watching,
    Competing,
    Custom
}

public enum UserPresenceStatus
{
    Online,
    Idle,
    DoNotDisturb,
    Invisible,
    Offline
}

public class RichPresenceDto
{
    public ActivityType ActivityType { get; set; } = ActivityType.None;
    public string? ActivityName { get; set; }
    public string? ActivityDetails { get; set; }
    public string? ActivityState { get; set; }
    public string? LargeImageUrl { get; set; }
    public string? SmallImageUrl { get; set; }
    public string? PartyId { get; set; }
    public int? PartySize { get; set; }
    public int? PartyMax { get; set; }
    public DateTime? StartTimestamp { get; set; }
    public DateTime? EndTimestamp { get; set; }
    public string? StreamUrl { get; set; }
}

public class CustomStatusDto
{
    public string? Text { get; set; }
    public string? EmojiId { get; set; }
    public string? EmojiName { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class UserPresenceDto
{
    public string UserId { get; set; } = string.Empty;
    public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Offline;
    public CustomStatusDto? CustomStatus { get; set; }
    public RichPresenceDto? RichPresence { get; set; }
    public string? ClientType { get; set; } // desktop, mobile, web
    public DateTime LastSeenAt { get; set; }
}

public class UpdatePresenceRequest
{
    public UserPresenceStatus? Status { get; set; }
    public CustomStatusDto? CustomStatus { get; set; }
    public RichPresenceDto? RichPresence { get; set; }
}

public class SetCustomStatusRequest
{
    public string? Text { get; set; }
    public string? EmojiId { get; set; }
    public string? EmojiName { get; set; }
    public int? DurationMinutes { get; set; } // null = no expiry, otherwise expires after X minutes
}
