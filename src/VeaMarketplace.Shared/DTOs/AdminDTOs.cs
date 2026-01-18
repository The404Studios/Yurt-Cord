namespace VeaMarketplace.Shared.DTOs;

/// <summary>
/// Server statistics for admin dashboard
/// </summary>
public class AdminServerStatsDto
{
    public int TotalUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalMessages { get; set; }
    public int MessagesToday { get; set; }
    public int TotalOrders { get; set; }
    public int OrdersToday { get; set; }
    public int ActiveBans { get; set; }
    public int ActiveMutes { get; set; }
    public int PendingReports { get; set; }
    public int TotalRooms { get; set; }
    public int NewUsersToday { get; set; }
    public int NewUsersThisWeek { get; set; }
    public long ServerUptime { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public bool MaintenanceMode { get; set; }
}

/// <summary>
/// Online user information for admin view
/// </summary>
public class AdminOnlineUserInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int ConnectionCount { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public List<string> ActiveHubs { get; set; } = new();
}
