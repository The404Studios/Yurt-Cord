using System.Diagnostics;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// Centralized configuration for scalability settings optimized for 1000+ concurrent users.
/// Provides tuning parameters for various subsystems.
/// </summary>
public class ScalabilityConfigurationService
{
    // Connection Limits
    public int MaxConcurrentConnections { get; set; } = 10000;
    public int MaxConnectionsPerUser { get; set; } = 5; // Prevent abuse
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan InactivityDisconnectThreshold { get; set; } = TimeSpan.FromMinutes(30);

    // Message Batching
    public bool EnableMessageBatching { get; set; } = true;
    public TimeSpan MessageBatchWindow { get; set; } = TimeSpan.FromMilliseconds(50);
    public int MaxMessagesPerBatch { get; set; } = 100;

    // Presence Management
    public bool EnablePresenceOptimization { get; set; } = true;
    public TimeSpan PresenceBroadcastInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxPresenceBroadcastBatchSize { get; set; } = 50; // Broadcast to 50 users at a time

    // Chat Message Throttling
    public bool EnableMessageThrottling { get; set; } = true;
    public int MaxMessagesPerMinute { get; set; } = 60;
    public int MaxMessagesPerBurst { get; set; } = 10;
    public TimeSpan ThrottleWindow { get; set; } = TimeSpan.FromSeconds(10);

    // Voice/Video Optimization
    public bool EnableVoiceOptimization { get; set; } = true;
    public int MaxConcurrentCalls { get; set; } = 200; // 200 concurrent calls = 200+ users
    public int MaxParticipantsPerGroupCall { get; set; } = 10;
    public int VoiceDataBatchSize { get; set; } = 5; // Batch 5 voice packets together

    // Database Query Optimization
    public bool EnableQueryCaching { get; set; } = true;
    public TimeSpan QueryCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxQueryResultsPerPage { get; set; } = 100;

    // Memory Management
    public long MaxMemoryCacheSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB
    public TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromMinutes(10);
    public double MemoryPressureThreshold { get; set; } = 0.8; // 80% memory usage triggers cleanup

    // User List Optimization
    public bool EnableLazyUserLoading { get; set; } = true;
    public int UserListPageSize { get; set; } = 50; // Load 50 users at a time
    public int MaxCachedOnlineUsers { get; set; } = 1000;

    // Friend List Optimization
    public int MaxFriendsPerUser { get; set; } = 1000;
    public bool EnableFriendListPagination { get; set; } = true;
    public int FriendListPageSize { get; set; } = 50;

    // Room/Channel Optimization
    public int MaxUsersPerRoom { get; set; } = 1000;
    public int MaxRoomsPerUser { get; set; } = 50;
    public bool EnableRoomMessageHistory { get; set; } = true;
    public int MaxRoomMessageHistory { get; set; } = 500;

    // Performance Monitoring
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public TimeSpan MetricsReportingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnableDetailedMetrics { get; set; } = false; // Disable in production for performance

    // Rate Limiting (per user)
    public int MaxApiRequestsPerMinute { get; set; } = 500;
    public int MaxSignalRInvocationsPerMinute { get; set; } = 1000;
    public int MaxFileUploadsPerHour { get; set; } = 50;

    // Horizontal Scaling (for multi-server deployment)
    public bool EnableRedisBackplane { get; set; } = false; // Enable for multi-server
    public string? RedisConnectionString { get; set; } = null;
    public bool EnableStickySessionsAffinity { get; set; } = false;

    // Load Balancing Hints
    public int RecommendedMinServers { get; set; } = 1;
    public int RecommendedMaxServers { get; set; } = 5;
    public int UsersPerServerTarget { get; set; } = 200; // Target 200 users per server

    public ScalabilityConfigurationService()
    {
        // Log configuration on startup
        LogConfiguration();
    }

    /// <summary>
    /// Get optimized configuration for current server load
    /// </summary>
    public void OptimizeForLoad(int currentUserCount)
    {
        // Adjust batching window based on load
        if (currentUserCount > 500)
        {
            // Under heavy load, increase batching to reduce network overhead
            MessageBatchWindow = TimeSpan.FromMilliseconds(100);
            MaxMessagesPerBatch = 150;
            PresenceBroadcastInterval = TimeSpan.FromSeconds(60);
        }
        else if (currentUserCount > 250)
        {
            MessageBatchWindow = TimeSpan.FromMilliseconds(75);
            MaxMessagesPerBatch = 125;
            PresenceBroadcastInterval = TimeSpan.FromSeconds(45);
        }
        else
        {
            // Light load - optimize for latency
            MessageBatchWindow = TimeSpan.FromMilliseconds(50);
            MaxMessagesPerBatch = 100;
            PresenceBroadcastInterval = TimeSpan.FromSeconds(30);
        }

        Debug.WriteLine($"[Scalability] Optimized for {currentUserCount} users: " +
                       $"BatchWindow={MessageBatchWindow.TotalMilliseconds}ms, " +
                       $"PresenceInterval={PresenceBroadcastInterval.TotalSeconds}s");
    }

    /// <summary>
    /// Calculate recommended server count based on current load
    /// </summary>
    public int CalculateRecommendedServers(int currentUserCount)
    {
        var recommendedServers = (int)Math.Ceiling((double)currentUserCount / UsersPerServerTarget);
        return Math.Max(RecommendedMinServers, Math.Min(recommendedServers, RecommendedMaxServers));
    }

    /// <summary>
    /// Get health status based on current metrics
    /// </summary>
    public ServerHealthStatus GetHealthStatus(int currentUsers, int currentConnections, long memoryUsageBytes)
    {
        var status = new ServerHealthStatus
        {
            CurrentUsers = currentUsers,
            CurrentConnections = currentConnections,
            MemoryUsageBytes = memoryUsageBytes,
            MaxUsers = UsersPerServerTarget,
            MaxConnections = MaxConcurrentConnections,
            MaxMemoryBytes = MaxMemoryCacheSizeBytes
        };

        // Calculate load percentage
        status.UserLoadPercent = ((double)currentUsers / UsersPerServerTarget) * 100;
        status.ConnectionLoadPercent = ((double)currentConnections / MaxConcurrentConnections) * 100;
        status.MemoryLoadPercent = ((double)memoryUsageBytes / MaxMemoryCacheSizeBytes) * 100;

        // Determine overall health
        var maxLoad = Math.Max(status.UserLoadPercent, Math.Max(status.ConnectionLoadPercent, status.MemoryLoadPercent));

        if (maxLoad < 60)
            status.Health = HealthLevel.Good;
        else if (maxLoad < 80)
            status.Health = HealthLevel.Warning;
        else if (maxLoad < 95)
            status.Health = HealthLevel.Critical;
        else
            status.Health = HealthLevel.Overloaded;

        status.RecommendedServers = CalculateRecommendedServers(currentUsers);

        return status;
    }

    private void LogConfiguration()
    {
        Debug.WriteLine($"[Scalability] Configuration loaded:");
        Debug.WriteLine($"  Max Concurrent Connections: {MaxConcurrentConnections}");
        Debug.WriteLine($"  Max Users Per Server Target: {UsersPerServerTarget}");
        Debug.WriteLine($"  Message Batching: {(EnableMessageBatching ? "Enabled" : "Disabled")}");
        Debug.WriteLine($"  Batch Window: {MessageBatchWindow.TotalMilliseconds}ms");
        Debug.WriteLine($"  Presence Optimization: {(EnablePresenceOptimization ? "Enabled" : "Disabled")}");
        Debug.WriteLine($"  Message Throttling: {(EnableMessageThrottling ? "Enabled" : "Disabled")}");
        Debug.WriteLine($"  Redis Backplane: {(EnableRedisBackplane ? "Enabled" : "Disabled")}");
    }
}

public class ServerHealthStatus
{
    public int CurrentUsers { get; set; }
    public int CurrentConnections { get; set; }
    public long MemoryUsageBytes { get; set; }
    public int MaxUsers { get; set; }
    public int MaxConnections { get; set; }
    public long MaxMemoryBytes { get; set; }
    public double UserLoadPercent { get; set; }
    public double ConnectionLoadPercent { get; set; }
    public double MemoryLoadPercent { get; set; }
    public HealthLevel Health { get; set; }
    public int RecommendedServers { get; set; }
}

public enum HealthLevel
{
    Good,
    Warning,
    Critical,
    Overloaded
}
