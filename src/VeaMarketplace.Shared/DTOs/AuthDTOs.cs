using System.ComponentModel.DataAnnotations;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 32 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 32 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public string? ClientSalt { get; set; }  // Unique salt for client-side encryption
    /// <summary>
    /// The current authentication mode the server is running in.
    /// </summary>
    public AuthenticationMode? AuthMode { get; set; }
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
    public string AccentColor { get; set; } = "#00B4D8"; // Yurt Cord teal
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public int Reputation { get; set; }
    public int TotalSales { get; set; }
    public int TotalPurchases { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime LastActive => LastSeenAt; // Alias for compatibility
    public bool IsOnline { get; set; }
    public bool IsFollowedByCurrentUser { get; set; }
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
    [StringLength(32, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 32 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public string? Username { get; set; }

    [StringLength(64, ErrorMessage = "Display name cannot exceed 64 characters")]
    public string? DisplayName { get; set; }

    [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters")]
    public string? Bio { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [StringLength(128, ErrorMessage = "Status message cannot exceed 128 characters")]
    public string? StatusMessage { get; set; }

    [Url(ErrorMessage = "Invalid avatar URL")]
    public string? AvatarUrl { get; set; }

    [Url(ErrorMessage = "Invalid banner URL")]
    public string? BannerUrl { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Accent color must be a valid hex color (e.g., #00B4D8)")]
    public string? AccentColor { get; set; }

    public ProfileVisibility? ProfileVisibility { get; set; }

    // Social Links
    [StringLength(32, ErrorMessage = "Discord username cannot exceed 32 characters")]
    public string? DiscordUsername { get; set; }

    [StringLength(15, ErrorMessage = "Twitter handle cannot exceed 15 characters")]
    [RegularExpression(@"^@?[a-zA-Z0-9_]+$", ErrorMessage = "Invalid Twitter handle format")]
    public string? TwitterHandle { get; set; }

    [StringLength(32, ErrorMessage = "Telegram username cannot exceed 32 characters")]
    public string? TelegramUsername { get; set; }

    [Url(ErrorMessage = "Invalid website URL")]
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
    public string? BannerUrl { get; set; }
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public string? StatusMessage { get; set; }
    public string? Bio { get; set; }
    public string? AccentColor { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Activity type for rich presence (not to be confused with Models.ActivityType for user activities)
/// </summary>
public enum PresenceActivityType
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
    public PresenceActivityType ActivityType { get; set; } = PresenceActivityType.None;
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
