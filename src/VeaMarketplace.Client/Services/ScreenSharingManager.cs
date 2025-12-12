using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Manages screen sharing with optimized capture, encoding, and network transmission.
/// Features adaptive quality, frame buffering, and efficient network replication.
/// </summary>
public class ScreenSharingManager : IScreenSharingManager
{
    // State
    private bool _isSharing;
    private bool _isConnected;
    private DisplayInfo? _currentDisplay;
    private ScreenShareSettings _settings = new();
    private ScreenShareStats _stats = new();
    private readonly ConcurrentDictionary<string, RemoteScreenShare> _activeShares = new();

    // Threading
    private CancellationTokenSource? _captureCts;
    private Thread? _captureThread;
    private Task? _sendTask;
    private readonly ConcurrentQueue<CapturedFrame> _frameQueue = new();
    private readonly object _settingsLock = new();

    // Network callbacks
    private Func<byte[], int, int, Task>? _sendFrameFunc;
    private Func<Task>? _notifyStartFunc;
    private Func<Task>? _notifyStopFunc;

    // Performance tracking
    private readonly Stopwatch _sessionStopwatch = new();
    private int _framesSentThisSecond;
    private int _framesDroppedThisSecond;
    private long _bytesSentThisSecond;
    private DateTime _lastStatsUpdate = DateTime.Now;
    private int _frameNumber;

    // Adaptive quality - only adjusts JPEG quality, NEVER reduces FPS for smooth streaming
    private readonly Queue<double> _sendTimeHistory = new();
    private const int SendTimeHistorySize = 30;
    private int _consecutiveSlowFrames;
    private int _consecutiveFastFrames;
    private const int AdaptiveThreshold = 10;
    private int _initialTargetFps; // Store initial FPS to maintain steady framerate
    private int _initialJpegQuality; // Store initial quality for reference

    // Bitmap reuse for capture efficiency
    private Bitmap? _captureBitmap;
    private Bitmap? _resizeBitmap;
    private Graphics? _captureGraphics;
    private Graphics? _resizeGraphics;

    // JPEG encoder
    private ImageCodecInfo? _jpegEncoder;
    private EncoderParameters? _encoderParams;

    public bool IsSharing => _isSharing;
    public bool IsConnected => _isConnected;
    public DisplayInfo? CurrentDisplay => _currentDisplay;
    public ScreenShareSettings CurrentSettings => _settings;
    public ScreenShareStats CurrentStats => _stats;
    public IReadOnlyDictionary<string, RemoteScreenShare> ActiveShares => _activeShares;

    public event Action<string, byte[], int, int>? OnFrameReady;
    public event Action<byte[], int, int>? OnLocalFrameReady;
    public event Action<ScreenFrame>? OnFrameReceived;
    public event Action<RemoteScreenShare>? OnScreenShareStarted;
    public event Action<string>? OnScreenShareStopped;
    public event Action<int>? OnViewerCountChanged;
    public event Action<ScreenShareStats>? OnStatsUpdated;

    public ScreenSharingManager()
    {
        InitializeJpegEncoder();
    }

    private void InitializeJpegEncoder()
    {
        _jpegEncoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(e => e.MimeType == "image/jpeg");
        _encoderParams = new EncoderParameters(1);
    }

    public Task ConnectAsync(Func<byte[], int, int, Task> sendFrameFunc, Func<Task> notifyStartFunc, Func<Task> notifyStopFunc)
    {
        _sendFrameFunc = sendFrameFunc;
        _notifyStartFunc = notifyStartFunc;
        _notifyStopFunc = notifyStopFunc;
        _isConnected = true;
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        // Mark as disconnected first to prevent further operations
        _isConnected = false;

        // Clear callbacks immediately to prevent async operations
        var notifyStop = _notifyStopFunc;
        _sendFrameFunc = null;
        _notifyStartFunc = null;
        _notifyStopFunc = null;

        // Stop sharing if active (synchronously to avoid async issues during disconnect)
        if (_isSharing)
        {
            _isSharing = false;

            // Cancel and cleanup synchronously
            try { _captureCts?.Cancel(); } catch { }

            // Give threads a moment to stop
            _captureThread?.Join(500);
            _captureThread = null;

            // Don't wait for send task - just let it cancel
            _sendTask = null;

            try { _captureCts?.Dispose(); } catch { }
            _captureCts = null;

            // Clear frame queue
            while (_frameQueue.TryDequeue(out _)) { }

            // Cleanup capture resources
            CleanupCaptureResources();

            _sessionStopwatch.Stop();
        }
    }

    public List<DisplayInfo> GetAvailableDisplays()
    {
        var displays = new List<DisplayInfo>();
        var screens = System.Windows.Forms.Screen.AllScreens;

        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            displays.Add(new DisplayInfo
            {
                DeviceName = screen.DeviceName,
                FriendlyName = screen.Primary ? $"Display {i + 1} (Primary)" : $"Display {i + 1}",
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                IsPrimary = screen.Primary,
                Index = i
            });
        }

        return displays;
    }

    public Task StartSharingAsync(DisplayInfo display, ScreenShareQuality quality)
    {
        return StartSharingAsync(display, ScreenShareSettings.FromQuality(quality));
    }

    public async Task StartSharingAsync(DisplayInfo display, ScreenShareSettings? settings = null)
    {
        if (_isSharing || !_isConnected) return;

        _currentDisplay = display;
        _settings = settings ?? new ScreenShareSettings();
        _isSharing = true;
        _frameNumber = 0;

        // Store initial settings for maintaining steady framerate
        _initialTargetFps = _settings.TargetFps;
        _initialJpegQuality = _settings.JpegQuality;

        // Reset stats
        _stats = new ScreenShareStats
        {
            TargetFps = _settings.TargetFps,
            CurrentWidth = _settings.TargetWidth,
            CurrentHeight = _settings.TargetHeight,
            CurrentQuality = _settings.JpegQuality,
            StartTime = DateTime.Now
        };

        // Initialize capture resources
        InitializeCaptureResources();

        // Clear any old frames
        while (_frameQueue.TryDequeue(out _)) { }

        // Notify server
        if (_notifyStartFunc != null)
        {
            try
            {
                await _notifyStartFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to notify start: {ex.Message}");
            }
        }

        // Start capture thread
        _captureCts = new CancellationTokenSource();
        _captureThread = new Thread(() => CaptureLoop(_captureCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "ScreenCaptureThread"
        };

        _sessionStopwatch.Restart();
        _captureThread.Start();

        // Start send task
        _sendTask = Task.Run(() => SendLoop(_captureCts.Token), _captureCts.Token);
    }

    public async Task StopSharingAsync()
    {
        if (!_isSharing) return;

        _isSharing = false;

        // Cancel the token first
        try
        {
            _captureCts?.Cancel();
        }
        catch { }

        // Wait for capture thread to stop with timeout
        if (_captureThread != null)
        {
            if (!_captureThread.Join(2000))
            {
                // Thread didn't stop in time, force it
                Debug.WriteLine("Capture thread did not stop gracefully");
            }
            _captureThread = null;
        }

        // Wait for send task to complete with timeout
        if (_sendTask != null)
        {
            try
            {
                // Use a timeout to prevent hanging
                var timeoutTask = Task.Delay(2000);
                var completedTask = await Task.WhenAny(_sendTask, timeoutTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    Debug.WriteLine("Send task did not complete in time");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error waiting for send task: {ex.Message}");
            }
        }
        _sendTask = null;

        // Dispose CTS after tasks are stopped
        try
        {
            _captureCts?.Dispose();
        }
        catch { }
        _captureCts = null;

        // Clear frame queue
        while (_frameQueue.TryDequeue(out _)) { }

        // Cleanup capture resources
        CleanupCaptureResources();

        _sessionStopwatch.Stop();
        _stats.Duration = _sessionStopwatch.Elapsed;

        // Notify server - VoiceService already handles this, but try as backup
        // Do NOT fire-and-forget as that causes InvokeCoreAsync errors on closed connections
        if (_isConnected && _notifyStopFunc != null)
        {
            try
            {
                await _notifyStopFunc().ConfigureAwait(false);
            }
            catch
            {
                // Ignore - server notification is also handled by VoiceService.StopScreenShareAsync
            }
        }
    }

    public void UpdateSettings(ScreenShareSettings settings)
    {
        lock (_settingsLock)
        {
            var needsResize = _settings.TargetWidth != settings.TargetWidth ||
                              _settings.TargetHeight != settings.TargetHeight;

            _settings = settings;
            _stats.TargetFps = settings.TargetFps;
            _stats.CurrentWidth = settings.TargetWidth;
            _stats.CurrentHeight = settings.TargetHeight;

            // Update encoder params
            if (_encoderParams != null)
            {
                _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)settings.JpegQuality);
            }

            if (needsResize && _isSharing)
            {
                // Reinitialize resize bitmap
                _resizeBitmap?.Dispose();
                _resizeGraphics?.Dispose();
                _resizeBitmap = new Bitmap(settings.TargetWidth, settings.TargetHeight, PixelFormat.Format24bppRgb);
                _resizeGraphics = Graphics.FromImage(_resizeBitmap);
                _resizeGraphics.InterpolationMode = InterpolationMode.Bilinear;
                _resizeGraphics.CompositingQuality = CompositingQuality.HighSpeed;
                _resizeGraphics.SmoothingMode = SmoothingMode.HighSpeed;
            }
        }
    }

    public void SetQuality(ScreenShareQuality quality)
    {
        UpdateSettings(ScreenShareSettings.FromQuality(quality));
    }

    private void InitializeCaptureResources()
    {
        if (_currentDisplay == null) return;

        // Capture bitmap at source resolution
        _captureBitmap = new Bitmap(_currentDisplay.Width, _currentDisplay.Height, PixelFormat.Format24bppRgb);
        _captureGraphics = Graphics.FromImage(_captureBitmap);
        _captureGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _captureGraphics.SmoothingMode = SmoothingMode.None;

        // Resize bitmap at target resolution
        _resizeBitmap = new Bitmap(_settings.TargetWidth, _settings.TargetHeight, PixelFormat.Format24bppRgb);
        _resizeGraphics = Graphics.FromImage(_resizeBitmap);
        _resizeGraphics.InterpolationMode = InterpolationMode.Bilinear;
        _resizeGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _resizeGraphics.SmoothingMode = SmoothingMode.HighSpeed;

        // Initialize encoder params
        if (_encoderParams != null)
        {
            _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_settings.JpegQuality);
        }
    }

    private void CleanupCaptureResources()
    {
        _captureGraphics?.Dispose();
        _captureGraphics = null;
        _captureBitmap?.Dispose();
        _captureBitmap = null;
        _resizeGraphics?.Dispose();
        _resizeGraphics = null;
        _resizeBitmap?.Dispose();
        _resizeBitmap = null;
    }

    /// <summary>
    /// Main capture loop running on dedicated thread for consistent timing
    /// </summary>
    private void CaptureLoop(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        long lastCaptureTime = 0;
        int frameIntervalMs;
        var captureTimer = new Stopwatch();

        while (!cancellationToken.IsCancellationRequested && _isSharing)
        {
            try
            {
                // Get current frame interval (may change with adaptive quality)
                lock (_settingsLock)
                {
                    frameIntervalMs = 1000 / _settings.TargetFps;
                }

                var now = stopwatch.ElapsedMilliseconds;
                var elapsed = now - lastCaptureTime;

                if (elapsed >= frameIntervalMs)
                {
                    lastCaptureTime = now;
                    captureTimer.Restart();

                    // Capture and encode frame
                    var frameData = CaptureAndEncode();

                    captureTimer.Stop();
                    _stats.CaptureTimeMs = captureTimer.Elapsed.TotalMilliseconds;

                    if (frameData != null && frameData.Length > 0)
                    {
                        // Queue frame with larger buffer to handle network jitter for steady FPS
                        if (_frameQueue.Count < 10)
                        {
                            _frameQueue.Enqueue(new CapturedFrame
                            {
                                Data = frameData,
                                Width = _settings.TargetWidth,
                                Height = _settings.TargetHeight,
                                FrameNumber = _frameNumber++,
                                Timestamp = stopwatch.ElapsedMilliseconds
                            });
                        }
                        else
                        {
                            _framesDroppedThisSecond++;
                        }
                    }
                }

                // Precise sleep
                var remaining = frameIntervalMs - (int)(stopwatch.ElapsedMilliseconds - lastCaptureTime);
                if (remaining > 2)
                {
                    Thread.Sleep(remaining - 1);
                }
                else if (remaining > 0)
                {
                    Thread.SpinWait(100);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Capture error: {ex.Message}");
                Thread.Sleep(16);
            }
        }
    }

    /// <summary>
    /// Captures screen and encodes to JPEG
    /// </summary>
    private byte[]? CaptureAndEncode()
    {
        if (_currentDisplay == null || _captureBitmap == null || _captureGraphics == null)
            return null;

        try
        {
            // Capture screen
            _captureGraphics.CopyFromScreen(
                _currentDisplay.Left,
                _currentDisplay.Top,
                0, 0,
                new Size(_currentDisplay.Width, _currentDisplay.Height),
                CopyPixelOperation.SourceCopy);

            Bitmap bitmapToEncode;

            // Resize if needed
            if (_currentDisplay.Width != _settings.TargetWidth ||
                _currentDisplay.Height != _settings.TargetHeight)
            {
                if (_resizeBitmap == null || _resizeGraphics == null)
                    return null;

                _resizeGraphics.DrawImage(_captureBitmap, 0, 0, _settings.TargetWidth, _settings.TargetHeight);
                bitmapToEncode = _resizeBitmap;
            }
            else
            {
                bitmapToEncode = _captureBitmap;
            }

            // Encode to JPEG
            using var ms = new MemoryStream();
            if (_jpegEncoder != null && _encoderParams != null)
            {
                bitmapToEncode.Save(ms, _jpegEncoder, _encoderParams);
            }
            else
            {
                bitmapToEncode.Save(ms, ImageFormat.Jpeg);
            }

            var frameData = ms.ToArray();

            // Adaptive quality: compress more if frame too large
            if (_settings.AdaptiveQuality && frameData.Length > _settings.MaxFrameSizeKb * 1024)
            {
                frameData = RecompressFrame(frameData, Math.Max(20, _settings.JpegQuality - 15));
            }

            return frameData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CaptureAndEncode error: {ex.Message}");
            return null;
        }
    }

    private byte[] RecompressFrame(byte[] frameData, int quality)
    {
        try
        {
            using var inputMs = new MemoryStream(frameData);
            using var image = Image.FromStream(inputMs);
            using var outputMs = new MemoryStream();

            if (_jpegEncoder != null)
            {
                using var recompressParams = new EncoderParameters(1);
                recompressParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                image.Save(outputMs, _jpegEncoder, recompressParams);
            }
            else
            {
                image.Save(outputMs, ImageFormat.Jpeg);
            }

            return outputMs.ToArray();
        }
        catch
        {
            return frameData;
        }
    }

    /// <summary>
    /// Send loop running as async task for network I/O
    /// </summary>
    private async Task SendLoop(CancellationToken cancellationToken)
    {
        var sendTimer = new Stopwatch();

        while (!cancellationToken.IsCancellationRequested && _isSharing)
        {
            try
            {
                // Get latest frame (skip old ones)
                CapturedFrame? frameToSend = null;
                while (_frameQueue.TryDequeue(out var frame))
                {
                    frameToSend = frame;
                    if (_frameQueue.IsEmpty) break;
                }

                if (frameToSend != null && _sendFrameFunc != null && _isConnected)
                {
                    sendTimer.Restart();

                    try
                    {
                        await _sendFrameFunc(frameToSend.Data, frameToSend.Width, frameToSend.Height)
                            .ConfigureAwait(false);

                        sendTimer.Stop();
                        var sendTimeMs = sendTimer.Elapsed.TotalMilliseconds;
                        _stats.SendTimeMs = sendTimeMs;

                        // Track for adaptive quality
                        TrackSendTime(sendTimeMs);

                        // Update stats
                        _framesSentThisSecond++;
                        _bytesSentThisSecond += frameToSend.Data.Length;
                        _stats.FramesSent++;
                        _stats.BytesSent += frameToSend.Data.Length;

                        // Fire events for local display
                        OnFrameReady?.Invoke("self", frameToSend.Data, frameToSend.Width, frameToSend.Height);
                        OnLocalFrameReady?.Invoke(frameToSend.Data, frameToSend.Width, frameToSend.Height);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Send error: {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(2, cancellationToken).ConfigureAwait(false);
                }

                // Update stats every second
                UpdateStats();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendLoop error: {ex.Message}");
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void TrackSendTime(double sendTimeMs)
    {
        _sendTimeHistory.Enqueue(sendTimeMs);
        if (_sendTimeHistory.Count > SendTimeHistorySize)
        {
            _sendTimeHistory.Dequeue();
        }

        if (!_settings.AdaptiveQuality) return;

        var frameIntervalMs = 1000.0 / _settings.TargetFps;

        // If send time > frame interval, network is congested
        if (sendTimeMs > frameIntervalMs * 0.8)
        {
            _consecutiveSlowFrames++;
            _consecutiveFastFrames = 0;

            if (_consecutiveSlowFrames >= AdaptiveThreshold)
            {
                ReduceQuality();
                _consecutiveSlowFrames = 0;
            }
        }
        else if (sendTimeMs < frameIntervalMs * 0.3)
        {
            _consecutiveFastFrames++;
            _consecutiveSlowFrames = 0;

            if (_consecutiveFastFrames >= AdaptiveThreshold * 2)
            {
                IncreaseQuality();
                _consecutiveFastFrames = 0;
            }
        }
    }

    private void ReduceQuality()
    {
        lock (_settingsLock)
        {
            // IMPORTANT: Never reduce FPS to maintain steady framerate - only adjust quality/resolution
            // This ensures smooth 60fps streaming without stuttering

            // First try reducing JPEG quality (most impact on bitrate with least visual impact)
            if (_settings.JpegQuality > 20)
            {
                _settings.JpegQuality -= 5;
                _stats.CurrentQuality = _settings.JpegQuality;
                if (_encoderParams != null)
                {
                    _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_settings.JpegQuality);
                }
                Debug.WriteLine($"Adaptive: Reduced quality to {_settings.JpegQuality}");
            }
            // If quality is already minimum, reduce resolution instead
            else if (_settings.TargetWidth > 854)
            {
                _settings.TargetWidth = 854;
                _settings.TargetHeight = 480;
                _stats.CurrentWidth = _settings.TargetWidth;
                _stats.CurrentHeight = _settings.TargetHeight;
                // Reset quality since resolution reduced
                _settings.JpegQuality = Math.Max(40, _initialJpegQuality - 10);
                _stats.CurrentQuality = _settings.JpegQuality;
                if (_encoderParams != null)
                {
                    _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_settings.JpegQuality);
                }
                ReinitializeResizeBitmap();
                Debug.WriteLine("Adaptive: Reduced resolution to 480p");
            }
            // FPS stays constant at initialTargetFps for smooth streaming
        }
    }

    private void IncreaseQuality()
    {
        lock (_settingsLock)
        {
            // Increase JPEG quality if below initial setting (never modify FPS)
            if (_settings.JpegQuality < _initialJpegQuality)
            {
                _settings.JpegQuality = Math.Min(_initialJpegQuality, _settings.JpegQuality + 5);
                _stats.CurrentQuality = _settings.JpegQuality;
                if (_encoderParams != null)
                {
                    _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_settings.JpegQuality);
                }
                Debug.WriteLine($"Adaptive: Increased quality to {_settings.JpegQuality}");
            }
            // FPS is always kept constant at _initialTargetFps
        }
    }

    private void ReinitializeResizeBitmap()
    {
        _resizeBitmap?.Dispose();
        _resizeGraphics?.Dispose();
        _resizeBitmap = new Bitmap(_settings.TargetWidth, _settings.TargetHeight, PixelFormat.Format24bppRgb);
        _resizeGraphics = Graphics.FromImage(_resizeBitmap);
        _resizeGraphics.InterpolationMode = InterpolationMode.Bilinear;
        _resizeGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _resizeGraphics.SmoothingMode = SmoothingMode.HighSpeed;
    }

    private void UpdateStats()
    {
        var now = DateTime.Now;
        if ((now - _lastStatsUpdate).TotalSeconds >= 1)
        {
            _stats.CurrentFps = _framesSentThisSecond;
            _stats.FramesDropped += _framesDroppedThisSecond;
            _stats.AverageBitrateMbps = (_bytesSentThisSecond * 8.0) / 1_000_000.0;
            _stats.Duration = _sessionStopwatch.Elapsed;

            _framesSentThisSecond = 0;
            _framesDroppedThisSecond = 0;
            _bytesSentThisSecond = 0;
            _lastStatsUpdate = now;

            OnStatsUpdated?.Invoke(_stats);
        }
    }

    // Receiving frames from other users
    public void HandleFrameReceived(string senderConnectionId, byte[] frameData, int width, int height)
    {
        var frame = new ScreenFrame
        {
            SenderConnectionId = senderConnectionId,
            Data = frameData,
            Width = width,
            Height = height,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Update active share info
        if (_activeShares.TryGetValue(senderConnectionId, out var share))
        {
            share.Width = width;
            share.Height = height;
            share.Fps++;
        }

        OnFrameReceived?.Invoke(frame);
    }

    public void HandleScreenShareStarted(string connectionId, string username)
    {
        var share = new RemoteScreenShare
        {
            ConnectionId = connectionId,
            Username = username,
            StartedAt = DateTime.Now,
            IsActive = true
        };

        _activeShares[connectionId] = share;
        OnScreenShareStarted?.Invoke(share);
    }

    public void HandleScreenShareStopped(string connectionId)
    {
        _activeShares.TryRemove(connectionId, out _);
        OnScreenShareStopped?.Invoke(connectionId);
    }

    public void HandleViewerCountUpdate(int count)
    {
        _stats.ViewerCount = count;
        OnViewerCountChanged?.Invoke(count);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Stop sharing with timeout
            if (_isSharing)
            {
                var stopTask = StopSharingAsync();
                var timeoutTask = Task.Delay(3000);
                await Task.WhenAny(stopTask, timeoutTask).ConfigureAwait(false);
            }
        }
        catch { }

        // Disconnect synchronously
        Disconnect();

        // Final cleanup
        CleanupCaptureResources();

        try { _encoderParams?.Dispose(); } catch { }

        // Clear any remaining state
        _activeShares.Clear();

        GC.SuppressFinalize(this);
    }

    private class CapturedFrame
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameNumber { get; set; }
        public long Timestamp { get; set; }
    }
}
