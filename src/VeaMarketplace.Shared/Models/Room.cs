namespace VeaMarketplace.Shared.Models;

/// <summary>
/// Represents a room/server that users can join for voice, video, and marketplace features.
/// </summary>
public class Room
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }

    // Ownership
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;

    // Settings
    public bool IsPublic { get; set; } = true;
    public bool AllowMarketplace { get; set; } = true;
    public bool AllowVoice { get; set; } = true;
    public bool AllowVideo { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
    public int MaxMembers { get; set; } = 100;
    public int MaxConcurrentStreams { get; set; } = 10;

    // Streaming quality limits
    public StreamingTier StreamingTier { get; set; } = StreamingTier.Standard;
    public int MaxUploadBitrate { get; set; } = 8000; // kbps
    public int MaxDownloadBitrate { get; set; } = 16000; // kbps

    // Channels
    public List<RoomChannel> TextChannels { get; set; } = new();
    public List<RoomChannel> VoiceChannels { get; set; } = new();

    // Roles
    public List<RoomRole> Roles { get; set; } = new();

    // Members
    public List<RoomMember> Members { get; set; } = new();

    // Marketplace
    public List<string> FeaturedProductIds { get; set; } = new();
    public decimal MarketplaceFeePercent { get; set; } = 5.0m; // Room owner takes 5% of sales

    // Stats
    public int TotalMembers { get; set; }
    public int OnlineMembers { get; set; }
    public int TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }
}

public class RoomChannel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomChannelType Type { get; set; }
    public int Position { get; set; }
    public string? ParentId { get; set; } // For channel categories/hierarchy
    public bool IsPrivate { get; set; }
    public List<string> AllowedRoleIds { get; set; } = new();

    // Voice channel specific
    public int? MaxUsers { get; set; }
    public int? Bitrate { get; set; } // kbps
    public bool? VideoEnabled { get; set; }
    public bool? ScreenShareEnabled { get; set; }
}

public enum RoomChannelType
{
    Text,
    Voice,
    Video,
    Stage, // For presentations/broadcasts
    Category // For organizing channels
}

public class RoomRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#99AAB5";
    public int Position { get; set; }
    public bool IsDefault { get; set; }

    // Permissions
    public RoomPermissions Permissions { get; set; } = new();
}

[Flags]
public enum RoomPermissions
{
    None = 0,

    // General
    ViewChannels = 1 << 0,
    ManageChannels = 1 << 1,
    ManageRoles = 1 << 2,
    ManageRoom = 1 << 3,

    // Members
    KickMembers = 1 << 4,
    BanMembers = 1 << 5,
    InviteMembers = 1 << 6,

    // Text
    SendMessages = 1 << 7,
    ManageMessages = 1 << 8,
    EmbedLinks = 1 << 9,
    AttachFiles = 1 << 10,
    MentionEveryone = 1 << 11,

    // Voice
    Connect = 1 << 12,
    Speak = 1 << 13,
    Video = 1 << 14,
    ScreenShare = 1 << 15,
    MuteMembers = 1 << 16,
    DeafenMembers = 1 << 17,
    MoveMembers = 1 << 18,
    PrioritySpeaker = 1 << 19,

    // Marketplace
    SellProducts = 1 << 20,
    ManageProducts = 1 << 21,
    ViewSalesAnalytics = 1 << 22,

    // Streaming
    StreamHighQuality = 1 << 23,
    StreamUltraQuality = 1 << 24,

    // All permissions
    Administrator = int.MaxValue
}

public class RoomMember
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> RoleIds { get; set; } = new();
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public string? CurrentChannelId { get; set; }
}

public enum StreamingTier
{
    Basic,      // 720p30, 4Mbps
    Standard,   // 1080p30, 8Mbps
    Premium,    // 1080p60, 16Mbps
    Ultra       // 4K30 / 1440p60, 30Mbps
}

public class StreamingQualityPreset
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameRate { get; set; }
    public int BitrateKbps { get; set; }
    public StreamingTier RequiredTier { get; set; }

    public static readonly StreamingQualityPreset[] Presets = new[]
    {
        new StreamingQualityPreset { Name = "480p", Width = 854, Height = 480, FrameRate = 30, BitrateKbps = 2000, RequiredTier = StreamingTier.Basic },
        new StreamingQualityPreset { Name = "720p", Width = 1280, Height = 720, FrameRate = 30, BitrateKbps = 4000, RequiredTier = StreamingTier.Basic },
        new StreamingQualityPreset { Name = "720p60", Width = 1280, Height = 720, FrameRate = 60, BitrateKbps = 6000, RequiredTier = StreamingTier.Standard },
        new StreamingQualityPreset { Name = "1080p", Width = 1920, Height = 1080, FrameRate = 30, BitrateKbps = 8000, RequiredTier = StreamingTier.Standard },
        new StreamingQualityPreset { Name = "1080p60", Width = 1920, Height = 1080, FrameRate = 60, BitrateKbps = 16000, RequiredTier = StreamingTier.Premium },
        new StreamingQualityPreset { Name = "1440p", Width = 2560, Height = 1440, FrameRate = 30, BitrateKbps = 20000, RequiredTier = StreamingTier.Ultra },
        new StreamingQualityPreset { Name = "1440p60", Width = 2560, Height = 1440, FrameRate = 60, BitrateKbps = 30000, RequiredTier = StreamingTier.Ultra },
        new StreamingQualityPreset { Name = "4K", Width = 3840, Height = 2160, FrameRate = 30, BitrateKbps = 30000, RequiredTier = StreamingTier.Ultra }
    };
}
