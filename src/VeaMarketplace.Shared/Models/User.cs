using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#00B4D8"; // Yurt Cord teal
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public UserRole Role { get; set; } = UserRole.Member;
    public UserRank Rank { get; set; } = UserRank.Newcomer;
    public int Reputation { get; set; } = 0;
    public int TotalSales { get; set; } = 0;
    public int TotalPurchases { get; set; } = 0;
    public decimal Balance { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = false;
    public bool IsBanned { get; set; } = false;
    public string? BanReason { get; set; }
    public List<string> Badges { get; set; } = new();
    public List<string> CustomRoleIds { get; set; } = new();
    public int WarningCount { get; set; } = 0;
    public bool IsMuted { get; set; } = false;
    public DateTime? MutedUntil { get; set; }
    public int MessagesCount { get; set; } = 0;
    public int VoiceMinutes { get; set; } = 0;

    public bool IsWhitelisted { get; set; } = false;

    // Seller response tracking
    public int TotalMessagesReceived { get; set; } = 0;
    public int TotalMessagesResponded { get; set; } = 0;
    public long TotalResponseTimeMs { get; set; } = 0; // Cumulative response time in milliseconds
    public DateTime? LastMessageReceivedAt { get; set; }
    public DateTime? LastMessageRespondedAt { get; set; }

    /// <summary>
    /// Calculate response rate as a percentage (0-100)
    /// </summary>
    public int ResponseRate => TotalMessagesReceived > 0
        ? Math.Min(100, (TotalMessagesResponded * 100) / TotalMessagesReceived)
        : 100;

    /// <summary>
    /// Calculate average response time as a human-readable string
    /// </summary>
    public string ResponseTime
    {
        get
        {
            if (TotalMessagesResponded == 0) return "N/A";
            var avgMs = TotalResponseTimeMs / TotalMessagesResponded;
            var avgTime = TimeSpan.FromMilliseconds(avgMs);

            if (avgTime.TotalMinutes < 1) return "< 1 minute";
            if (avgTime.TotalMinutes < 60) return $"~{(int)avgTime.TotalMinutes} minutes";
            if (avgTime.TotalHours < 24) return $"~{(int)avgTime.TotalHours} hours";
            return $"~{(int)avgTime.TotalDays} days";
        }
    }

    // Social Links
    public string? DiscordUsername { get; set; }
    public string? TwitterHandle { get; set; }
    public string? TelegramUsername { get; set; }
    public string? WebsiteUrl { get; set; }

    // Enhanced profile features
    public string? CustomEmoji { get; set; }
    public string? ActivityStatus { get; set; }
    public DateTime? ActivityStatusExpiresAt { get; set; }
    public string? ProfileThemeId { get; set; }
    public List<string> FavoriteProductIds { get; set; } = new();
}

public enum ProfileVisibility
{
    Public,
    FriendsOnly,
    Private
}
