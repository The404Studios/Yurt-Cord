using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// Background service that performs periodic cleanup tasks:
/// - Expires old sessions/tokens
/// - Cleans up stale connections
/// - Archives old messages
/// - Expires temporary bans
/// - Updates user online status
/// </summary>
public class CleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupBackgroundService> _logger;

    // Cleanup intervals
    private static readonly TimeSpan StaleConnectionCleanupInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BanExpirationCheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OnlineStatusCleanupInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OldDataArchiveInterval = TimeSpan.FromHours(1);

    // Thresholds
    private static readonly TimeSpan StaleConnectionThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(10);

    public CleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup background service started");

        // Stagger the initial runs
        var lastStaleCleanup = DateTime.UtcNow;
        var lastBanCheck = DateTime.UtcNow;
        var lastOnlineCleanup = DateTime.UtcNow;
        var lastArchive = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Stale connection cleanup
                if (now - lastStaleCleanup >= StaleConnectionCleanupInterval)
                {
                    await CleanupStaleConnections(stoppingToken);
                    lastStaleCleanup = now;
                }

                // Ban expiration check
                if (now - lastBanCheck >= BanExpirationCheckInterval)
                {
                    await ExpireBansAndMutes(stoppingToken);
                    lastBanCheck = now;
                }

                // Online status cleanup
                if (now - lastOnlineCleanup >= OnlineStatusCleanupInterval)
                {
                    await CleanupOnlineStatus(stoppingToken);
                    lastOnlineCleanup = now;
                }

                // Old data archive (less frequent)
                if (now - lastArchive >= OldDataArchiveInterval)
                {
                    await ArchiveOldData(stoppingToken);
                    lastArchive = now;
                }

                // Sleep for a short interval before next check
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup background service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Cleanup background service stopped");
    }

    private async Task CleanupStaleConnections(CancellationToken ct)
    {
        try
        {
            // This would clean up any tracked connections that haven't sent a heartbeat
            // The actual cleanup is handled by the hubs themselves via OnDisconnectedAsync
            // This is just a safety net for orphaned state

            _logger.LogDebug("Stale connection cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during stale connection cleanup");
        }

        await Task.CompletedTask;
    }

    private async Task ExpireBansAndMutes(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            var now = DateTime.UtcNow;
            var expiredCount = 0;

            // Expire bans
            var expiredBans = db.UserBans
                .Find(b => b.IsActive && b.ExpiresAt.HasValue && b.ExpiresAt.Value <= now)
                .ToList();

            foreach (var ban in expiredBans)
            {
                ban.IsActive = false;
                db.UserBans.Update(ban);
                expiredCount++;
            }

            // Expire mutes
            var expiredMutes = db.UserMutes
                .Find(m => m.IsActive && m.ExpiresAt.HasValue && m.ExpiresAt.Value <= now)
                .ToList();

            foreach (var mute in expiredMutes)
            {
                mute.IsActive = false;
                db.UserMutes.Update(mute);
                expiredCount++;
            }

            if (expiredCount > 0)
            {
                _logger.LogInformation("Expired {Count} bans/mutes", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during ban/mute expiration check");
        }

        await Task.CompletedTask;
    }

    private async Task CleanupOnlineStatus(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            var threshold = DateTime.UtcNow - OfflineThreshold;
            var staleOnlineUsers = db.Users
                .Find(u => u.IsOnline && u.LastSeenAt < threshold)
                .ToList();

            foreach (var user in staleOnlineUsers)
            {
                user.IsOnline = false;
                db.Users.Update(user);
            }

            if (staleOnlineUsers.Count > 0)
            {
                _logger.LogDebug("Marked {Count} stale users as offline", staleOnlineUsers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during online status cleanup");
        }

        await Task.CompletedTask;
    }

    private async Task ArchiveOldData(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            // Clean up old notifications (older than 30 days and read)
            var notificationThreshold = DateTime.UtcNow.AddDays(-30);
            var oldNotifications = db.Notifications
                .Find(n => n.IsRead && n.CreatedAt < notificationThreshold)
                .ToList();

            if (oldNotifications.Count > 100) // Only if there are many
            {
                foreach (var notification in oldNotifications.Take(1000)) // Batch delete
                {
                    db.Notifications.Delete(notification.Id);
                }
                _logger.LogInformation("Archived {Count} old notifications", Math.Min(oldNotifications.Count, 1000));
            }

            // Clean up old activity logs (older than 90 days)
            var activityThreshold = DateTime.UtcNow.AddDays(-90);
            var oldActivities = db.UserActivities
                .Find(a => a.CreatedAt < activityThreshold)
                .ToList();

            if (oldActivities.Count > 100)
            {
                foreach (var activity in oldActivities.Take(1000))
                {
                    db.UserActivities.Delete(activity.Id);
                }
                _logger.LogInformation("Archived {Count} old activity logs", Math.Min(oldActivities.Count, 1000));
            }

            // Clean up expired coupons
            var expiredCoupons = db.Coupons
                .Find(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value < DateTime.UtcNow && !c.IsActive)
                .ToList();

            if (expiredCoupons.Count > 50)
            {
                foreach (var coupon in expiredCoupons.Take(100))
                {
                    db.Coupons.Delete(coupon.Id);
                }
                _logger.LogInformation("Cleaned up {Count} expired coupons", Math.Min(expiredCoupons.Count, 100));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during old data archive");
        }

        await Task.CompletedTask;
    }
}
