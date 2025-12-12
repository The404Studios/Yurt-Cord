using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for viewing screen shares from other users and self
/// </summary>
public interface IScreenShareViewerService
{
    /// <summary>
    /// Currently active screen shares in the channel
    /// </summary>
    ObservableCollection<ScreenShareDto> ActiveScreenShares { get; }

    /// <summary>
    /// The screen share we're currently viewing (if any)
    /// </summary>
    ScreenShareDto? CurrentlyViewing { get; }

    /// <summary>
    /// Our own screen share (if sharing)
    /// </summary>
    ScreenShareDto? OwnScreenShare { get; }

    /// <summary>
    /// Event when a new frame is received for any screen share
    /// </summary>
    event Action<string, ImageSource>? OnFrameReceived;

    /// <summary>
    /// Event when a screen share starts
    /// </summary>
    event Action<ScreenShareDto>? OnScreenShareStarted;

    /// <summary>
    /// Event when a screen share stops
    /// </summary>
    event Action<string>? OnScreenShareStopped;

    /// <summary>
    /// Start viewing a specific screen share
    /// </summary>
    Task StartViewingAsync(string sharerConnectionId);

    /// <summary>
    /// Stop viewing the current screen share
    /// </summary>
    Task StopViewingAsync();

    /// <summary>
    /// Process a received frame (called by VoiceService)
    /// </summary>
    void HandleFrame(string sharerConnectionId, byte[] frameData, int width, int height);

    /// <summary>
    /// Handle screen share started notification
    /// </summary>
    void HandleScreenShareStarted(string connectionId, string username, string channelId);

    /// <summary>
    /// Handle screen share stopped notification
    /// </summary>
    void HandleScreenShareStopped(string connectionId);

    /// <summary>
    /// Get the latest frame as ImageSource for a specific sharer
    /// </summary>
    ImageSource? GetLatestFrame(string sharerConnectionId);

    /// <summary>
    /// Clear all state (for disconnect)
    /// </summary>
    void Clear();
}

public class ScreenShareViewerService : IScreenShareViewerService
{
    private readonly ConcurrentDictionary<string, BitmapImage> _latestFrames = new();
    private readonly ConcurrentDictionary<string, ScreenShareDto> _screenShares = new();
    private string? _viewingConnectionId;
    private string? _ownConnectionId;

    // Jitter buffer for smooth playback - holds frames and releases at steady rate
    private readonly ConcurrentDictionary<string, ConcurrentQueue<BufferedFrame>> _frameBuffers = new();
    private readonly ConcurrentDictionary<string, int> _targetFps = new();
    private DispatcherTimer? _playbackTimer;
    private readonly object _timerLock = new();

    // Buffer settings - tuned for smooth playback with memory trade-off
    private const int TargetBufferSize = 3; // Buffer 3 frames before starting playback
    private const int MaxBufferSize = 15;   // ~50MB at 720p (3.5MB per decoded frame)
    private const int PlaybackIntervalMs = 16; // ~60fps playback rate

    public ObservableCollection<ScreenShareDto> ActiveScreenShares { get; } = new();
    public ScreenShareDto? CurrentlyViewing => _viewingConnectionId != null && _screenShares.TryGetValue(_viewingConnectionId, out var share) ? share : null;
    public ScreenShareDto? OwnScreenShare => _ownConnectionId != null && _screenShares.TryGetValue(_ownConnectionId, out var share) ? share : null;

    public event Action<string, ImageSource>? OnFrameReceived;
    public event Action<ScreenShareDto>? OnScreenShareStarted;
    public event Action<string>? OnScreenShareStopped;

    public void SetOwnConnectionId(string connectionId)
    {
        _ownConnectionId = connectionId;
    }

    public Task StartViewingAsync(string sharerConnectionId)
    {
        _viewingConnectionId = sharerConnectionId;
        return Task.CompletedTask;
    }

    public Task StopViewingAsync()
    {
        _viewingConnectionId = null;
        return Task.CompletedTask;
    }

    public void HandleFrame(string sharerConnectionId, byte[] frameData, int width, int height)
    {
        if (frameData == null || frameData.Length == 0) return;

        try
        {
            // Get or create buffer for this sharer
            var buffer = _frameBuffers.GetOrAdd(sharerConnectionId, _ => new ConcurrentQueue<BufferedFrame>());

            // Drop oldest frames if buffer is full to prevent memory growth
            // Each decoded 720p frame is ~3.5MB, so 15 frames = ~50MB per stream
            while (buffer.Count >= MaxBufferSize)
            {
                buffer.TryDequeue(out _);
            }

            // Pre-decode frame NOW to trade memory for CPU
            // Decoding happens on UI thread via InvokeAsync (non-blocking)
            // Decoded bitmap is cached in memory for instant playback (no CPU during display)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(frameData);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Thread-safe, keeps decoded pixels in memory

                    buffer.Enqueue(new BufferedFrame
                    {
                        DecodedBitmap = bitmap, // ~3.5MB per 720p frame in memory
                        Width = width,
                        Height = height,
                        ReceivedAt = Stopwatch.GetTimestamp()
                    });

                    // Update screen share info
                    if (_screenShares.TryGetValue(sharerConnectionId, out var share))
                    {
                        share.Width = width;
                        share.Height = height;
                    }

                    // Ensure playback timer is running
                    EnsurePlaybackTimerRunning();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error decoding frame: {ex.Message}");
                }
            }, DispatcherPriority.Background); // Background priority to not block UI
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling frame: {ex.Message}");
        }
    }

    private void EnsurePlaybackTimerRunning()
    {
        if (_playbackTimer != null) return;

        lock (_timerLock)
        {
            if (_playbackTimer != null) return;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (_playbackTimer != null) return;

                _playbackTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(PlaybackIntervalMs)
                };
                _playbackTimer.Tick += PlaybackTimer_Tick;
                _playbackTimer.Start();
                Debug.WriteLine("Started screen share playback timer");
            });
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        // Process one frame from each active buffer at steady rate
        foreach (var kvp in _frameBuffers)
        {
            var sharerConnectionId = kvp.Key;
            var buffer = kvp.Value;

            // Wait until we have enough frames buffered for smooth playback
            // This prevents playing back immediately and allows jitter absorption
            if (buffer.Count < TargetBufferSize && buffer.Count > 0)
            {
                continue;
            }

            if (buffer.TryDequeue(out var frame))
            {
                // Frame is already decoded - just display it (zero CPU, uses ~3.5MB memory per frame)
                if (frame.DecodedBitmap != null)
                {
                    _latestFrames[sharerConnectionId] = frame.DecodedBitmap;

                    // Track FPS
                    if (_screenShares.TryGetValue(sharerConnectionId, out var share))
                    {
                        share.Fps++;
                    }

                    OnFrameReceived?.Invoke(sharerConnectionId, frame.DecodedBitmap);
                }
            }
        }

        // Stop timer if no active buffers
        if (_frameBuffers.IsEmpty || _frameBuffers.All(kvp => kvp.Value.IsEmpty))
        {
            StopPlaybackTimer();
        }
    }

    private void StopPlaybackTimer()
    {
        lock (_timerLock)
        {
            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
                _playbackTimer.Tick -= PlaybackTimer_Tick;
                _playbackTimer = null;
                Debug.WriteLine("Stopped screen share playback timer");
            }
        }
    }

    public void HandleScreenShareStarted(string connectionId, string username, string channelId)
    {
        var share = new ScreenShareDto
        {
            SharerConnectionId = connectionId,
            SharerUsername = username,
            ChannelId = channelId,
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };

        _screenShares[connectionId] = share;

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var existing = ActiveScreenShares.FirstOrDefault(s => s.SharerConnectionId == connectionId);
            if (existing != null)
            {
                var index = ActiveScreenShares.IndexOf(existing);
                ActiveScreenShares[index] = share;
            }
            else
            {
                ActiveScreenShares.Add(share);
            }

            OnScreenShareStarted?.Invoke(share);
        });
    }

    public void HandleScreenShareStopped(string connectionId)
    {
        _screenShares.TryRemove(connectionId, out _);
        _latestFrames.TryRemove(connectionId, out _);
        _frameBuffers.TryRemove(connectionId, out _); // Clear frame buffer

        if (_viewingConnectionId == connectionId)
        {
            _viewingConnectionId = null;
        }

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var existing = ActiveScreenShares.FirstOrDefault(s => s.SharerConnectionId == connectionId);
            if (existing != null)
            {
                ActiveScreenShares.Remove(existing);
            }

            OnScreenShareStopped?.Invoke(connectionId);
        });
    }

    public ImageSource? GetLatestFrame(string sharerConnectionId)
    {
        return _latestFrames.TryGetValue(sharerConnectionId, out var frame) ? frame : null;
    }

    public void Clear()
    {
        _viewingConnectionId = null;
        _ownConnectionId = null;
        _latestFrames.Clear();
        _screenShares.Clear();

        // Stop playback and clear buffers
        StopPlaybackTimer();
        _frameBuffers.Clear();

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ActiveScreenShares.Clear();
        });
    }

    private class BufferedFrame
    {
        public BitmapImage? DecodedBitmap { get; set; } // Pre-decoded bitmap (~3.5MB per 720p frame)
        public int Width { get; set; }
        public int Height { get; set; }
        public long ReceivedAt { get; set; }
    }
}
