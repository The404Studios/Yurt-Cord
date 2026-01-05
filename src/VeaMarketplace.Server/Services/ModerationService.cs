using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ModerationService
{
    private readonly DatabaseService _db;

    public ModerationService(DatabaseService db)
    {
        _db = db;
    }

    public ModerationDashboardDto GetDashboard()
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);

        var activeBans = _db.UserBans.Count(b => b.IsActive && (!b.ExpiresAt.HasValue || b.ExpiresAt > now));
        var activeMutes = _db.UserMutes.Count(m => m.IsActive && m.ExpiresAt > now);
        var pendingReports = _db.MessageReports.Count(r => r.Status == ReportStatus.Pending);
        var autoModActions24h = _db.ModerationLogs.Count(l => l.CreatedAt >= yesterday);
        var totalModActions = _db.ModerationLogs.Count();

        var recentActions = _db.ModerationLogs.Query()
            .OrderByDescending(l => l.CreatedAt)
            .Limit(20)
            .ToList()
            .Select(MapLogToDto)
            .ToList();

        return new ModerationDashboardDto
        {
            ActiveBans = activeBans,
            ActiveMutes = activeMutes,
            PendingReports = pendingReports,
            AutoModActions24h = autoModActions24h,
            TotalModActions = totalModActions,
            RecentActions = recentActions
        };
    }

    public List<UserBanDto> GetBannedUsers()
    {
        var now = DateTime.UtcNow;
        return _db.UserBans.Find(b => b.IsActive && (!b.ExpiresAt.HasValue || b.ExpiresAt > now))
            .OrderByDescending(b => b.BannedAt)
            .Select(MapBanToDto)
            .ToList();
    }

    public List<MessageReportDto> GetPendingReports()
    {
        return _db.MessageReports.Find(r => r.Status == ReportStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(MapReportToDto)
            .ToList();
    }

    public UserBanDto? BanUser(string moderatorId, BanUserRequest request)
    {
        var moderator = _db.Users.FindById(moderatorId);
        var targetUser = _db.Users.FindById(request.UserId);

        if (moderator == null || targetUser == null) return null;
        if (moderator.Role < UserRole.Moderator) return null;
        if (targetUser.Role >= moderator.Role) return null; // Can't ban equal or higher roles

        // Check if already banned
        var existingBan = _db.UserBans.FindOne(b => b.UserId == request.UserId && b.IsActive);
        if (existingBan != null)
        {
            existingBan.ExpiresAt = request.ExpiresAt;
            existingBan.Reason = request.Reason;
            _db.UserBans.Update(existingBan);
            return MapBanToDto(existingBan);
        }

        var ban = new UserBan
        {
            UserId = request.UserId,
            Username = targetUser.Username,
            BannedBy = moderator.Username,
            Reason = request.Reason,
            BannedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _db.UserBans.Insert(ban);

        // Update user
        targetUser.IsBanned = true;
        targetUser.BanReason = request.Reason;
        _db.Users.Update(targetUser);

        // Log action
        LogModerationAction(moderatorId, moderator.Username, ModerationType.Ban,
            request.UserId, targetUser.Username, request.Reason);

        return MapBanToDto(ban);
    }

    public bool UnbanUser(string moderatorId, string userId)
    {
        var moderator = _db.Users.FindById(moderatorId);
        var targetUser = _db.Users.FindById(userId);

        if (moderator == null || moderator.Role < UserRole.Moderator) return false;
        if (targetUser == null) return false;

        var ban = _db.UserBans.FindOne(b => b.UserId == userId && b.IsActive);
        if (ban == null) return false;

        ban.IsActive = false;
        ban.UnbannedBy = moderator.Username;
        ban.UnbannedAt = DateTime.UtcNow;
        _db.UserBans.Update(ban);

        targetUser.IsBanned = false;
        targetUser.BanReason = null;
        _db.Users.Update(targetUser);

        LogModerationAction(moderatorId, moderator.Username, ModerationType.Unban,
            userId, targetUser.Username, "Unbanned by moderator");

        return true;
    }

    public UserMuteDto? MuteUser(string moderatorId, MuteUserRequest request)
    {
        var moderator = _db.Users.FindById(moderatorId);
        var targetUser = _db.Users.FindById(request.UserId);

        if (moderator == null || targetUser == null) return null;
        if (moderator.Role < UserRole.Moderator) return null;
        if (targetUser.Role >= moderator.Role) return null;

        // Remove existing mute if any
        var existingMute = _db.UserMutes.FindOne(m => m.UserId == request.UserId && m.IsActive);
        if (existingMute != null)
        {
            existingMute.IsActive = false;
            _db.UserMutes.Update(existingMute);
        }

        var mute = new UserMute
        {
            UserId = request.UserId,
            Username = targetUser.Username,
            MutedBy = moderator.Username,
            Reason = request.Reason,
            MutedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
            MutedChannels = request.MutedChannels ?? new()
        };

        _db.UserMutes.Insert(mute);

        LogModerationAction(moderatorId, moderator.Username, ModerationType.Mute,
            request.UserId, targetUser.Username, request.Reason);

        return MapMuteToDto(mute);
    }

    public bool UnmuteUser(string moderatorId, string userId)
    {
        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        var mute = _db.UserMutes.FindOne(m => m.UserId == userId && m.IsActive);
        if (mute == null) return false;

        mute.IsActive = false;
        _db.UserMutes.Update(mute);

        var targetUser = _db.Users.FindById(userId);
        LogModerationAction(moderatorId, moderator.Username, ModerationType.Mute,
            userId, targetUser?.Username, "Unmuted by moderator");

        return true;
    }

    public bool WarnUser(string moderatorId, WarnUserRequest request)
    {
        var moderator = _db.Users.FindById(moderatorId);
        var targetUser = _db.Users.FindById(request.UserId);

        if (moderator == null || targetUser == null) return false;
        if (moderator.Role < UserRole.Moderator) return false;

        var warning = new UserWarning
        {
            UserId = request.UserId,
            Username = targetUser.Username,
            IssuedBy = moderator.Username,
            Reason = request.Reason,
            IssuedAt = DateTime.UtcNow
        };

        _db.UserWarnings.Insert(warning);

        LogModerationAction(moderatorId, moderator.Username, ModerationType.Warning,
            request.UserId, targetUser.Username, request.Reason);

        return true;
    }

    public List<UserWarning> GetUserWarnings(string userId)
    {
        return _db.UserWarnings.Find(w => w.UserId == userId)
            .OrderByDescending(w => w.IssuedAt)
            .ToList();
    }

    public bool DeleteMessage(string moderatorId, string messageId, string reason)
    {
        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        var message = _db.Messages.FindById(messageId);
        if (message == null) return false;

        _db.Messages.Delete(messageId);

        LogModerationAction(moderatorId, moderator.Username, ModerationType.MessageDelete,
            message.SenderId, message.SenderUsername, reason, $"Message content: {message.Content}");

        return true;
    }

    public bool ResolveReport(string moderatorId, string reportId, string resolution)
    {
        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        var report = _db.MessageReports.FindById(reportId);
        if (report == null) return false;

        report.Status = ReportStatus.Resolved;
        report.ReviewedBy = moderator.Username;
        report.ReviewedAt = DateTime.UtcNow;
        report.Resolution = resolution;
        _db.MessageReports.Update(report);

        return true;
    }

    public bool DismissReport(string moderatorId, string reportId)
    {
        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        var report = _db.MessageReports.FindById(reportId);
        if (report == null) return false;

        report.Status = ReportStatus.Dismissed;
        report.ReviewedBy = moderator.Username;
        report.ReviewedAt = DateTime.UtcNow;
        _db.MessageReports.Update(report);

        return true;
    }

    public bool IsUserBanned(string userId)
    {
        var now = DateTime.UtcNow;
        return _db.UserBans.Exists(b =>
            b.UserId == userId &&
            b.IsActive &&
            (!b.ExpiresAt.HasValue || b.ExpiresAt > now));
    }

    public bool IsUserMuted(string userId, string? channelId = null)
    {
        var now = DateTime.UtcNow;
        var mute = _db.UserMutes.FindOne(m =>
            m.UserId == userId &&
            m.IsActive &&
            m.ExpiresAt > now);

        if (mute == null) return false;

        // If channel specified, check if muted in that channel
        if (channelId != null && mute.MutedChannels.Count > 0)
        {
            return mute.MutedChannels.Contains(channelId);
        }

        return true;
    }

    private void LogModerationAction(
        string moderatorId, string moderatorUsername,
        ModerationType type, string? targetUserId, string? targetUsername,
        string? reason, string? details = null)
    {
        var log = new ModerationLog
        {
            ModeratorId = moderatorId,
            ModeratorUsername = moderatorUsername,
            Type = type,
            TargetUserId = targetUserId,
            TargetUsername = targetUsername,
            Reason = reason,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _db.ModerationLogs.Insert(log);
    }

    private static UserBanDto MapBanToDto(UserBan ban)
    {
        return new UserBanDto
        {
            Id = ban.Id,
            UserId = ban.UserId,
            Username = ban.Username,
            BannedBy = ban.BannedBy,
            Reason = ban.Reason,
            BannedAt = ban.BannedAt,
            ExpiresAt = ban.ExpiresAt,
            IsActive = ban.IsActive
        };
    }

    private static UserMuteDto MapMuteToDto(UserMute mute)
    {
        return new UserMuteDto
        {
            Id = mute.Id,
            UserId = mute.UserId,
            Username = mute.Username,
            MutedBy = mute.MutedBy,
            Reason = mute.Reason,
            MutedAt = mute.MutedAt,
            ExpiresAt = mute.ExpiresAt,
            IsActive = mute.IsActive,
            MutedChannels = mute.MutedChannels
        };
    }

    private static ModerationLogDto MapLogToDto(ModerationLog log)
    {
        return new ModerationLogDto
        {
            Id = log.Id,
            Type = log.Type,
            ModeratorId = log.ModeratorId,
            ModeratorUsername = log.ModeratorUsername,
            TargetUserId = log.TargetUserId,
            TargetUsername = log.TargetUsername,
            Reason = log.Reason,
            Details = log.Details,
            CreatedAt = log.CreatedAt,
            Icon = GetModIcon(log.Type),
            ActionDescription = GetActionDescription(log.Type, log.TargetUsername)
        };
    }

    private static MessageReportDto MapReportToDto(MessageReport report)
    {
        return new MessageReportDto
        {
            Id = report.Id,
            MessageId = report.MessageId,
            MessageContent = report.MessageContent ?? "",
            ReporterId = report.ReporterId,
            ReporterUsername = "",
            ReportedUserId = report.ReportedUserId,
            ReportedUsername = "",
            Reason = report.Reason,
            AdditionalInfo = report.AdditionalInfo,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ReviewedBy = report.ReviewedBy,
            ReviewedAt = report.ReviewedAt,
            Resolution = report.Resolution
        };
    }

    private static string GetModIcon(ModerationType type)
    {
        return type switch
        {
            ModerationType.Warning => "⚠️",
            ModerationType.Mute => "🔇",
            ModerationType.Kick => "👢",
            ModerationType.Ban => "🔨",
            ModerationType.Unban => "✅",
            ModerationType.MessageDelete => "🗑️",
            ModerationType.MessageEdit => "✏️",
            ModerationType.ChannelUpdate => "📝",
            ModerationType.RoleUpdate => "👑",
            _ => "📋"
        };
    }

    private static string GetActionDescription(ModerationType type, string? targetUsername)
    {
        var target = targetUsername ?? "Unknown";
        return type switch
        {
            ModerationType.Warning => $"Warned {target}",
            ModerationType.Mute => $"Muted {target}",
            ModerationType.Kick => $"Kicked {target}",
            ModerationType.Ban => $"Banned {target}",
            ModerationType.Unban => $"Unbanned {target}",
            ModerationType.MessageDelete => $"Deleted message from {target}",
            ModerationType.MessageEdit => $"Edited message from {target}",
            ModerationType.ChannelUpdate => "Updated channel",
            ModerationType.RoleUpdate => "Updated roles",
            _ => "Moderation action"
        };
    }

    #region Product Reports

    /// <summary>
    /// Submit a report for a product
    /// </summary>
    public ProductReportDto? ReportProduct(string reporterId, ReportProductRequest request)
    {
        var reporter = _db.Users.FindById(reporterId);
        if (reporter == null) return null;

        var product = _db.Products.FindById(request.ProductId);
        if (product == null) return null;

        var seller = _db.Users.FindById(product.SellerId);

        // Check if user has already reported this product
        var existingReport = _db.ProductReports.FindOne(r =>
            r.ProductId == request.ProductId &&
            r.ReporterId == reporterId &&
            r.Status == ReportStatus.Pending);

        if (existingReport != null)
        {
            // Update existing report
            existingReport.Reason = request.Reason;
            existingReport.AdditionalInfo = request.AdditionalInfo;
            _db.ProductReports.Update(existingReport);
            return MapProductReportToDto(existingReport);
        }

        var report = new ProductReport
        {
            ProductId = request.ProductId,
            ProductTitle = product.Title,
            SellerId = product.SellerId,
            SellerUsername = seller?.Username ?? "Unknown",
            ReporterId = reporterId,
            ReporterUsername = reporter.Username,
            Reason = request.Reason,
            AdditionalInfo = request.AdditionalInfo
        };

        _db.ProductReports.Insert(report);
        return MapProductReportToDto(report);
    }

    /// <summary>
    /// Get all pending product reports
    /// </summary>
    public List<ProductReportDto> GetPendingProductReports()
    {
        return _db.ProductReports.Find(r => r.Status == ReportStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(MapProductReportToDto)
            .ToList();
    }

    /// <summary>
    /// Get all product reports with optional status filter
    /// </summary>
    public List<ProductReportDto> GetProductReports(ReportStatus? status = null)
    {
        var query = status.HasValue
            ? _db.ProductReports.Find(r => r.Status == status.Value)
            : _db.ProductReports.FindAll();

        return query
            .OrderByDescending(r => r.CreatedAt)
            .Select(MapProductReportToDto)
            .ToList();
    }

    /// <summary>
    /// Resolve a product report
    /// </summary>
    public bool ResolveProductReport(string reportId, string moderatorId, string resolution, bool takeAction = false)
    {
        var report = _db.ProductReports.FindById(reportId);
        if (report == null) return false;

        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        report.Status = ReportStatus.Resolved;
        report.ReviewedBy = moderator.Username;
        report.ReviewedAt = DateTime.UtcNow;
        report.Resolution = resolution;
        _db.ProductReports.Update(report);

        // If taking action, delete the product
        if (takeAction)
        {
            var product = _db.Products.FindById(report.ProductId);
            if (product != null)
            {
                product.IsDeleted = true;
                _db.Products.Update(product);

                // Log the moderation action
                LogModerationAction(moderatorId, ModerationType.MessageDelete, report.SellerId, report.SellerUsername,
                    $"Removed product '{report.ProductTitle}' due to report: {resolution}");
            }
        }

        return true;
    }

    /// <summary>
    /// Dismiss a product report
    /// </summary>
    public bool DismissProductReport(string reportId, string moderatorId)
    {
        var report = _db.ProductReports.FindById(reportId);
        if (report == null) return false;

        var moderator = _db.Users.FindById(moderatorId);
        if (moderator == null || moderator.Role < UserRole.Moderator) return false;

        report.Status = ReportStatus.Dismissed;
        report.ReviewedBy = moderator.Username;
        report.ReviewedAt = DateTime.UtcNow;
        _db.ProductReports.Update(report);

        return true;
    }

    private static ProductReportDto MapProductReportToDto(ProductReport report)
    {
        return new ProductReportDto
        {
            Id = report.Id,
            ProductId = report.ProductId,
            ProductTitle = report.ProductTitle,
            SellerId = report.SellerId,
            SellerUsername = report.SellerUsername,
            ReporterId = report.ReporterId,
            ReporterUsername = report.ReporterUsername,
            Reason = report.Reason,
            AdditionalInfo = report.AdditionalInfo,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ReviewedBy = report.ReviewedBy,
            ReviewedAt = report.ReviewedAt,
            Resolution = report.Resolution
        };
    }

    #endregion
}
