using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class BanUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; } // null = permanent
}

public class MuteUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<string> MutedChannels { get; set; } = new(); // empty = all channels
}

public class WarnUserRequest
{
    public string UserId { get; set; } = string.Empty;
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
    public string MessageId { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
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
    public string Name { get; set; } = string.Empty;
    public AutoModRuleType Type { get; set; }
    public AutoModAction Action { get; set; }
    public List<string>? BannedWords { get; set; }
    public List<string>? AllowedDomains { get; set; }
    public int? MaxMentions { get; set; }
    public int? MaxEmojis { get; set; }
    public int? MaxCapitalPercent { get; set; }
    public string? CustomRegex { get; set; }
}
