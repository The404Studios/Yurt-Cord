namespace VeaMarketplace.Shared.Models;

public enum ModerationType
{
    Warning = 0,
    Mute = 1,
    Kick = 2,
    Ban = 3,
    Unban = 4,
    MessageDelete = 5,
    MessageEdit = 6,
    ChannelUpdate = 7,
    RoleUpdate = 8,
    Other = 9
}

public class ModerationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ModerationType Type { get; set; }
    public string ModeratorId { get; set; } = string.Empty;
    public string ModeratorUsername { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetUsername { get; set; }
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserBan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string BannedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // null = permanent
    public bool IsActive { get; set; } = true;
    public string? UnbannedBy { get; set; }
    public DateTime? UnbannedAt { get; set; }
}

public class UserMute
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string MutedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime MutedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // null = permanent
    public bool IsActive { get; set; } = true;
    public List<string> MutedChannels { get; set; } = new(); // empty = all channels
}

public class UserWarning
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public bool Acknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }
}
