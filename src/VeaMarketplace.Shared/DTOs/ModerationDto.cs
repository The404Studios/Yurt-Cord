using System.ComponentModel.DataAnnotations;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class BanUserRequest
{
    [Required(ErrorMessage = "User ID is required")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 1000 characters")]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; } // null = permanent
}

public class MuteUserRequest
{
    [Required(ErrorMessage = "User ID is required")]
    public string UserId { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
    public string? Reason { get; set; }

    [Required(ErrorMessage = "Expiration date is required")]
    public DateTime ExpiresAt { get; set; }

    public List<string> MutedChannels { get; set; } = new(); // empty = all channels
}

public class WarnUserRequest
{
    [Required(ErrorMessage = "User ID is required")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 1000 characters")]
    public string Reason { get; set; } = string.Empty;
}

public class UserBanDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string UserAvatarUrl { get; set; } = string.Empty;
    public string BannedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPermanent => ExpiresAt == null;
}

public class UserMuteDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string MutedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime MutedAt { get; set; }
    public DateTime? ExpiresAt { get; set; } // null = permanent
    public bool IsActive { get; set; } = true;
    public List<string> MutedChannels { get; set; } = new();
}

public class MessageReportDto
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterUsername { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public string ReportedUsername { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
    public string? AdditionalInfo { get; set; }
    public ReportStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Resolution { get; set; }
}

public class ReportMessageRequest
{
    [Required(ErrorMessage = "Message ID is required")]
    public string MessageId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required")]
    public ReportReason Reason { get; set; }

    [StringLength(1000, ErrorMessage = "Additional info cannot exceed 1000 characters")]
    public string? AdditionalInfo { get; set; }
}

public class ModerationLogDto
{
    public string Id { get; set; } = string.Empty;
    public ModerationType Type { get; set; }
    public string ModeratorId { get; set; } = string.Empty;
    public string ModeratorUsername { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetUsername { get; set; }
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Icon { get; set; } = "📝";
    public string ActionDescription { get; set; } = string.Empty;
}

public class ModerationDashboardDto
{
    public int ActiveBans { get; set; }
    public int ActiveMutes { get; set; }
    public int PendingReports { get; set; }
    public int AutoModActions24h { get; set; }
    public int TotalModActions { get; set; }
    public List<ModerationLogDto> RecentActions { get; set; } = new();
}

public class AutoModRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AutoModRuleType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AutoModAction Action { get; set; }
    public List<string> BannedWords { get; set; } = new();
    public List<string> AllowedDomains { get; set; } = new();
    public int? MaxMentions { get; set; }
    public int? MaxEmojis { get; set; }
    public int? MaxCapitalPercent { get; set; }
    public string? CustomRegex { get; set; }
    public List<string> ExemptRoles { get; set; } = new();
    public List<string> ExemptUsers { get; set; } = new();
    public int? MuteDuration { get; set; }
}

public class CreateAutoModRuleRequest
{
    [Required(ErrorMessage = "Rule name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Rule name must be between 1 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rule type is required")]
    public AutoModRuleType Type { get; set; }

    [Required(ErrorMessage = "Action is required")]
    public AutoModAction Action { get; set; }

    [MaxLength(1000, ErrorMessage = "Maximum 1000 banned words allowed")]
    public List<string>? BannedWords { get; set; }

    [MaxLength(100, ErrorMessage = "Maximum 100 allowed domains")]
    public List<string>? AllowedDomains { get; set; }

    [Range(1, 50, ErrorMessage = "Max mentions must be between 1 and 50")]
    public int? MaxMentions { get; set; }

    [Range(1, 100, ErrorMessage = "Max emojis must be between 1 and 100")]
    public int? MaxEmojis { get; set; }

    [Range(0, 100, ErrorMessage = "Max capital percent must be between 0 and 100")]
    public int? MaxCapitalPercent { get; set; }

    [StringLength(500, ErrorMessage = "Custom regex cannot exceed 500 characters")]
    public string? CustomRegex { get; set; }
}

public class ProductReportDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterUsername { get; set; } = string.Empty;
    public ProductReportReason Reason { get; set; }
    public string? AdditionalInfo { get; set; }
    public ReportStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Resolution { get; set; }
}

public class ReportProductRequest
{
    [Required(ErrorMessage = "Product ID is required")]
    public string ProductId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required")]
    public ProductReportReason Reason { get; set; }

    [StringLength(1000, ErrorMessage = "Additional info cannot exceed 1000 characters")]
    public string? AdditionalInfo { get; set; }
}
