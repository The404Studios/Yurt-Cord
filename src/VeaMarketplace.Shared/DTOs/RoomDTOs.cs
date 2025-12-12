using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class RoomDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool AllowMarketplace { get; set; }
    public bool AllowVoice { get; set; }
    public bool AllowVideo { get; set; }
    public bool AllowScreenShare { get; set; }
    public int MaxMembers { get; set; }
    public int MaxConcurrentStreams { get; set; }
    public StreamingTier StreamingTier { get; set; }
    public int TotalMembers { get; set; }
    public int OnlineMembers { get; set; }
    public List<RoomChannelDto> TextChannels { get; set; } = new();
    public List<RoomChannelDto> VoiceChannels { get; set; } = new();
    public List<RoomRoleDto> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class RoomChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomChannelType Type { get; set; }
    public int Position { get; set; }
    public string? ParentId { get; set; }
    public bool IsPrivate { get; set; }
    public int? MaxUsers { get; set; }
    public int? Bitrate { get; set; }
    public bool? VideoEnabled { get; set; }
    public bool? ScreenShareEnabled { get; set; }
    public List<RoomMemberDto> ConnectedUsers { get; set; } = new();
}

public class RoomRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#99AAB5";
    public int Position { get; set; }
    public bool IsDefault { get; set; }
    public RoomPermissions Permissions { get; set; }
}

public class RoomMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> RoleIds { get; set; } = new();
    public bool IsOnline { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsStreaming { get; set; }
    public bool IsScreenSharing { get; set; }
    public string? CurrentChannelId { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool AllowMarketplace { get; set; } = true;
    public bool AllowVoice { get; set; } = true;
    public bool AllowVideo { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
}

public class UpdateRoomRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public bool? IsPublic { get; set; }
    public bool? AllowMarketplace { get; set; }
    public bool? AllowVoice { get; set; }
    public bool? AllowVideo { get; set; }
    public bool? AllowScreenShare { get; set; }
    public int? MaxMembers { get; set; }
    public int? MaxConcurrentStreams { get; set; }
    public StreamingTier? StreamingTier { get; set; }
    public decimal? MarketplaceFeePercent { get; set; }
}

public class CreateChannelRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomChannelType Type { get; set; }
    public string? ParentId { get; set; }
    public bool IsPrivate { get; set; }
    public int? MaxUsers { get; set; }
    public int? Bitrate { get; set; }
    public bool? VideoEnabled { get; set; }
    public bool? ScreenShareEnabled { get; set; }
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#99AAB5";
    public RoomPermissions Permissions { get; set; }
}

public class StreamQualityRequestDto
{
    public string PresetName { get; set; } = "720p";
    public int? CustomWidth { get; set; }
    public int? CustomHeight { get; set; }
    public int? CustomFrameRate { get; set; }
    public int? CustomBitrate { get; set; }
}

public class StreamInfoDto
{
    public string StreamerId { get; set; } = string.Empty;
    public string StreamerUsername { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public StreamType Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameRate { get; set; }
    public int BitrateKbps { get; set; }
    public int ViewerCount { get; set; }
    public DateTime StartedAt { get; set; }
}

public enum StreamType
{
    Camera,
    ScreenShare,
    Application
}

public class RoomProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
}
