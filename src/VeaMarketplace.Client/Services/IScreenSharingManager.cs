using System.Collections.Concurrent;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Screen sharing quality presets
/// </summary>
public enum ScreenShareQuality
{
    Low,      // 480p, 30fps, high compression
    Medium,   // 720p, 30fps, medium compression
    High,     // 720p, 60fps, low compression
    Ultra     // 1080p, 60fps, low compression
}

/// <summary>
/// Configuration for screen sharing session
/// </summary>
public class ScreenShareSettings
{
    public int TargetFps { get; set; } = 60;
    public int TargetWidth { get; set; } = 1280;
    public int TargetHeight { get; set; } = 720;
    public int JpegQuality { get; set; } = 50;
    public int MaxFrameSizeKb { get; set; } = 100;
    public bool AdaptiveQuality { get; set; } = true;
    public bool ShareAudio { get; set; } = false;

    public static ScreenShareSettings FromQuality(ScreenShareQuality quality) => quality switch
    {
        ScreenShareQuality.Low => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 854,
            TargetHeight = 480,
            JpegQuality = 40,
            MaxFrameSizeKb = 50
        },
        ScreenShareQuality.Medium => new ScreenShareSettings
        {
            TargetFps = 30,
            TargetWidth = 1280,
            TargetHeight = 720,
            JpegQuality = 50,
            MaxFrameSizeKb = 75
        },
        ScreenShareQuality.High => new ScreenShareSettings
        {
            TargetFps = 60,
            TargetWidth = 1280,
            TargetHeight = 720,
            JpegQuality = 55,
            MaxFrameSizeKb = 100
        },
        ScreenShareQuality.Ultra => new ScreenShareSettings
        {
            TargetFps = 60,
            TargetWidth = 1920,
            TargetHeight = 1080,
            JpegQuality = 60,
            MaxFrameSizeKb = 150
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

    // Receiving (called by VoiceService when frames arrive)
    void HandleFrameReceived(string senderConnectionId, byte[] frameData, int width, int height);
    void HandleScreenShareStarted(string connectionId, string username);
    void HandleScreenShareStopped(string connectionId);
    void HandleViewerCountUpdate(int count);

    // Display enumeration
    List<DisplayInfo> GetAvailableDisplays();
}
