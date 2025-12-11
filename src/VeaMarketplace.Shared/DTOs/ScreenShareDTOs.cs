namespace VeaMarketplace.Shared.DTOs;

/// <summary>
/// DTO for screen share state that can be replicated across the network
/// </summary>
public class ScreenShareDto
{
    public string SharerConnectionId { get; set; } = string.Empty;
    public string SharerUserId { get; set; } = string.Empty;
    public string SharerUsername { get; set; } = string.Empty;
    public string SharerAvatarUrl { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Fps { get; set; }
    public int ViewerCount { get; set; }
    public DateTime StartedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for screen frame data
/// </summary>
public class ScreenFrameDto
{
    public string SharerConnectionId { get; set; } = string.Empty;
    public byte[] FrameData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameNumber { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// DTO for active screen shares list
/// </summary>
public class ActiveScreenSharesDto
{
    public List<ScreenShareDto> ScreenShares { get; set; } = new();
}

/// <summary>
/// Request to start screen share
/// </summary>
public class StartScreenShareRequest
{
    public string ChannelId { get; set; } = string.Empty;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int TargetFps { get; set; } = 30;
}

/// <summary>
/// Response when screen share started
/// </summary>
public class ScreenShareStartedResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ScreenShareDto? ScreenShare { get; set; }
}
