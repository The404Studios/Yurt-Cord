using System.Collections.Concurrent;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Screen sharing quality presets - supports up to 4K with 30MB/s upload
/// </summary>
public enum ScreenShareQuality
{
    Source,   // Dynamic - match source resolution
    Low,      // 480p, 30fps - ~2 Mbps
    Medium,   // 720p, 30fps - ~4 Mbps
    High,     // 720p, 60fps - ~6 Mbps
    HD,       // 1080p, 30fps - ~8 Mbps
    FullHD,   // 1080p, 60fps - ~16 Mbps
    QHD,      // 1440p, 30fps - ~20 Mbps
    QHD60,    // 1440p, 60fps - ~30 Mbps
    UHD       // 4K, 30fps - ~30 Mbps
}

/// <summary>
/// Configuration for screen sharing session - supports up to 30MB/s upload
/// </summary>
public class ScreenShareSettings
{
    public int TargetFps { get; set; } = 30; // Default to 30fps for balanced CPU/quality
    public int TargetWidth { get; set; } = 1280;
    public int TargetHeight { get; set; } = 720;
    public int JpegQuality { get; set; } = 75; // Industry standard quality for good visual fidelity
    public int MaxFrameSizeKb { get; set; } = 80;
    public int BitrateKbps { get; set; } = 4000; // Target bitrate in kbps
    public bool AdaptiveQuality { get; set; } = true;

    /// <summary>
    /// Enable system audio (desktop audio) capture during screen sharing.
    /// NOTE: WASAPI loopback implementation is NOT YET COMPLETE.
    /// See SYSTEM_AUDIO_IMPLEMENTATION_GUIDE.md for full implementation details.
    /// When implemented, this will capture all desktop audio (games, music, apps)
    /// and stream it alongside video to viewers.
    /// </summary>
    public bool ShareAudio { get; set; } = true; // Ready for system audio when implemented

    public bool AllowDownscaling { get; set; } = true; // Allow viewers to request lower quality

    /// <summary>
    /// Creates settings from a quality preset. Supports bandwidth up to 30MB/s upload.
    /// </summary>
    public static ScreenShareSettings FromQuality(ScreenShareQuality quality) => quality switch
    {
        ScreenShareQuality.Source => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 0, // 0 = match source
            TargetHeight = 0,
            JpegQuality = 70,
            MaxFrameSizeKb = 1000,
            AdaptiveQuality = true
        },
        ScreenShareQuality.Low => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 854,
            TargetHeight = 480,
            JpegQuality = 60, // Improved: readable text even on low preset
            MaxFrameSizeKb = 80, // ~2 Mbps
            BitrateKbps = 2000
        },
        ScreenShareQuality.Medium => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 1280,
            TargetHeight = 720,
            JpegQuality = 70, // Improved: good balance of quality/bandwidth
            MaxFrameSizeKb = 160, // ~4 Mbps
            BitrateKbps = 4000
        },
        ScreenShareQuality.High => new ScreenShareSettings
        {
            TargetFps = 60,
            TargetWidth = 1280,
            TargetHeight = 720,
            JpegQuality = 80, // Improved: smooth 60 FPS with great quality
            MaxFrameSizeKb = 125, // ~6 Mbps at 60fps
            BitrateKbps = 6000
        },
        ScreenShareQuality.HD => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 1920,
            TargetHeight = 1080,
            JpegQuality = 80, // Improved: 1080p deserves high quality
            MaxFrameSizeKb = 330, // ~8 Mbps
            BitrateKbps = 8000
        },
        ScreenShareQuality.FullHD => new ScreenShareSettings
        {
            TargetFps = 60,
            TargetWidth = 1920,
            TargetHeight = 1080,
            JpegQuality = 85, // Improved: premium quality for 1080p60
            MaxFrameSizeKb = 330, // ~16 Mbps at 60fps
            BitrateKbps = 16000
        },
        ScreenShareQuality.QHD => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 2560,
            TargetHeight = 1440,
            JpegQuality = 85, // Improved: high quality for 1440p
            MaxFrameSizeKb = 830, // ~20 Mbps
            BitrateKbps = 20000
        },
        ScreenShareQuality.QHD60 => new ScreenShareSettings
        {
            TargetFps = 60,
            TargetWidth = 2560,
            TargetHeight = 1440,
            JpegQuality = 90, // Improved: near-lossless for 1440p60
            MaxFrameSizeKb = 625, // ~30 Mbps at 60fps
            BitrateKbps = 30000
        },
        ScreenShareQuality.UHD => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 3840,
            TargetHeight = 2160,
            JpegQuality = 90, // Improved: near-lossless for 4K
            MaxFrameSizeKb = 1250, // ~30 Mbps
            BitrateKbps = 30000
        },
        _ => new ScreenShareSettings()
    };
}

/// <summary>
/// Statistics for active screen share session
/// </summary>
public class ScreenShareStats
{
    public int CurrentFps { get; set; }
    public int TargetFps { get; set; }
    public int FramesSent { get; set; }
    public int FramesDropped { get; set; }
    public long BytesSent { get; set; }
    public int CurrentWidth { get; set; }
    public int CurrentHeight { get; set; }
    public int CurrentQuality { get; set; }
    public int ViewerCount { get; set; }
    public double AverageBitrateMbps { get; set; }
    public double CaptureTimeMs { get; set; }
    public double EncodeTimeMs { get; set; }
    public double SendTimeMs { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public int AverageFrameSize { get; set; }
}

/// <summary>
/// Information about a remote screen share
/// </summary>
public class RemoteScreenShare
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Fps { get; set; }
    public DateTime StartedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Frame data received from remote screen share
/// </summary>
public class ScreenFrame
{
    public string SenderConnectionId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameNumber { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Interface for managing screen sharing functionality
/// </summary>
public interface IScreenSharingManager : IAsyncDisposable
{
    // State
    bool IsSharing { get; }
    bool IsConnected { get; }
    DisplayInfo? CurrentDisplay { get; }
    ScreenShareSettings CurrentSettings { get; }
    ScreenShareStats CurrentStats { get; }

    // Active shares (remote users sharing their screens)
    IReadOnlyDictionary<string, RemoteScreenShare> ActiveShares { get; }

    // Events for broadcasting
    event Action<string, byte[], int, int>? OnFrameReady;

    // Event for local preview (fires when we're sharing)
    event Action<byte[], int, int>? OnLocalFrameReady;

    // Events for receiving
    event Action<ScreenFrame>? OnFrameReceived;
    event Action<RemoteScreenShare>? OnScreenShareStarted;
    event Action<string>? OnScreenShareStopped;
    event Action<int>? OnViewerCountChanged;
    event Action<ScreenShareStats>? OnStatsUpdated;

    // Connection management
    Task ConnectAsync(Func<byte[], int, int, Task> sendFrameFunc, Func<Task> notifyStartFunc, Func<Task> notifyStopFunc);
    void Disconnect();

    // Sharing controls
    Task StartSharingAsync(DisplayInfo display, ScreenShareSettings? settings = null);
    Task StartSharingAsync(DisplayInfo display, ScreenShareQuality quality);
    Task StopSharingAsync();

    // Settings
    void UpdateSettings(ScreenShareSettings settings);
    void SetQuality(ScreenShareQuality quality);

    // Voice priority - yield to audio when voice is active to prevent choppy audio
    void SetVoiceActive(bool active);

    // Receiving (called by VoiceService when frames arrive)
    void HandleFrameReceived(string senderConnectionId, byte[] frameData, int width, int height);
    void HandleScreenShareStarted(string connectionId, string username);
    void HandleScreenShareStopped(string connectionId);
    void HandleViewerCountUpdate(int count);

    // Display enumeration
    List<DisplayInfo> GetAvailableDisplays();
}
