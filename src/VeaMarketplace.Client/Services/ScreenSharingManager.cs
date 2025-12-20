using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using VeaMarketplace.Client.Services.Streaming;
using DrawingRectangle = System.Drawing.Rectangle;

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

    // H.264 decoders for receiving frames from other users (one per sharer)
    private readonly ConcurrentDictionary<string, HardwareVideoDecoder> _h264Decoders = new();
    private readonly ReaderWriterLockSlim _decoderLock = new();

    // Threading - dedicated threads for capture, encoding, and sending
    private CancellationTokenSource? _captureCts;
    private Thread? _captureThread;
    private Thread? _encodeThread;
    private Task? _sendTask;
    private readonly ConcurrentQueue<Bitmap> _captureQueue = new();  // Raw captured bitmaps
    private readonly ConcurrentQueue<CapturedFrame> _frameQueue = new();  // Encoded frames ready to send
    private readonly object _settingsLock = new();
    private readonly AutoResetEvent _captureSignal = new(false);  // Signal encode thread

    // Performance tuning constants
    private const int MaxCaptureQueueSize = 5;   // Raw frames waiting for encoding
    private const int MaxFrameQueueSize = 30;    // Encoded frames waiting to send (increased from 10)

    // Network callbacks
    private Func<byte[], int, int, Task>? _sendFrameFunc;
    private Func<Task>? _notifyStartFunc;
    private Func<Task>? _notifyStopFunc;

    // Voice priority - yield to audio when someone is speaking to prevent choppy voice
    private volatile bool _voiceActive;
    public void SetVoiceActive(bool active) => _voiceActive = active;

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

    // JPEG encoder (fallback)
    private ImageCodecInfo? _jpegEncoder;

    // Hardware encoder (H.264 with NVENC/AMF/QSV)
    private HardwareVideoEncoder? _hardwareEncoder;
    private bool _useHardwareEncoding;
    public bool IsHardwareEncodingEnabled => _useHardwareEncoding && _hardwareEncoder != null;

    // Adaptive streaming engine (smart compression + delta encoding)
    private AdaptiveStreamingEngine? _streamingEngine;
    private bool _useAdaptiveStreaming = true; // Enable by default
    public string EncoderName => _hardwareEncoder?.EncoderName ?? "jpeg";

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
        _sendFrameFunc = null;
        _notifyStartFunc = null;
        _notifyStopFunc = null;

        // Stop sharing if active (synchronously to avoid async issues during disconnect)
        if (_isSharing)
        {
            _isSharing = false;

            // Cancel and cleanup synchronously
            try { _captureCts?.Cancel(); } catch (Exception ex) { Debug.WriteLine($"Cleanup: CaptureCts cancel failed: {ex.Message}"); }

            // Signal encode thread to wake up
            _captureSignal.Set();

            // Give threads a moment to stop
            _captureThread?.Join(500);
            _captureThread = null;
            _encodeThread?.Join(500);
            _encodeThread = null;

            // Don't wait for send task - just let it cancel
            _sendTask = null;

            try { _captureCts?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Cleanup: CaptureCts dispose failed: {ex.Message}"); }
            _captureCts = null;

            // Clear both queues
            while (_frameQueue.TryDequeue(out _)) { }
            while (_captureQueue.TryDequeue(out var bitmap)) { bitmap?.Dispose(); }

            // Cleanup capture resources
            CleanupCaptureResources();

            _sessionStopwatch.Stop();
        }

        // Clean up all H.264 decoders for received frames
        _decoderLock.EnterWriteLock();
        try
        {
            foreach (var decoder in _h264Decoders.Values)
            {
                decoder.Dispose();
            }
            _h264Decoders.Clear();
        }
        finally
        {
            _decoderLock.ExitWriteLock();
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

        // Clear any old frames from both queues
        while (_frameQueue.TryDequeue(out _)) { }
        while (_captureQueue.TryDequeue(out var oldBitmap)) { oldBitmap?.Dispose(); }

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

        // Start capture and encode threads
        _captureCts = new CancellationTokenSource();

        // Capture thread - AboveNormal priority for consistent frame timing
        // (Audio thread is Highest, so audio always wins)
        _captureThread = new Thread(() => CaptureLoop(_captureCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "ScreenCaptureThread"
        };

        // Encode thread - Normal priority (CPU intensive, shouldn't starve audio)
        _encodeThread = new Thread(() => EncodeLoop(_captureCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal,
            Name = "ScreenEncodeThread"
        };

        _sessionStopwatch.Restart();
        _captureThread.Start();
        _encodeThread.Start();

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
        catch (Exception ex)
        {
            Debug.WriteLine($"Capture cancel failed: {ex.Message}");
        }

        // Signal encode thread to wake up and exit
        _captureSignal.Set();

        // Wait for capture thread to stop with timeout
        if (_captureThread != null)
        {
            if (!_captureThread.Join(2000))
            {
                Debug.WriteLine("Capture thread did not stop gracefully");
            }
            _captureThread = null;
        }

        // Wait for encode thread to stop with timeout
        if (_encodeThread != null)
        {
            if (!_encodeThread.Join(2000))
            {
                Debug.WriteLine("Encode thread did not stop gracefully");
            }
            _encodeThread = null;
        }

        // Wait for send task to complete with timeout
        if (_sendTask != null)
        {
            try
            {
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
        catch (Exception ex)
        {
            Debug.WriteLine($"CaptureCts dispose failed: {ex.Message}");
        }
        _captureCts = null;

        // Clear both queues
        while (_frameQueue.TryDequeue(out _)) { }
        while (_captureQueue.TryDequeue(out var bitmap)) { bitmap?.Dispose(); }

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

        // Initialize adaptive streaming engine (smart compression + delta encoding)
        try
        {
            _streamingEngine?.Dispose();
            _streamingEngine = new AdaptiveStreamingEngine(new StreamingConfig
            {
                MaxWidth = _settings.TargetWidth,
                MaxHeight = _settings.TargetHeight,
                BaseQuality = _settings.JpegQuality,
                TargetFps = _settings.TargetFps,
                TargetBitrateMbps = _settings.BitrateKbps / 1000
            });
            _streamingEngine.Start();
            Debug.WriteLine("Adaptive streaming engine initialized");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize adaptive streaming: {ex.Message}");
            _useAdaptiveStreaming = false;
            _streamingEngine = null;
        }

        // Try to initialize hardware encoder (H.264 with GPU acceleration)
        // First check if FFmpeg is available at all
        if (!FFmpegHelper.IsAvailable)
        {
            Debug.WriteLine("FFmpeg not available, using JPEG encoding only");
            _useHardwareEncoding = false;
            _hardwareEncoder = null;
        }
        else
        {
            try
            {
                _hardwareEncoder?.Dispose();
                _hardwareEncoder = new HardwareVideoEncoder();

                if (_hardwareEncoder.Initialize(_settings.TargetWidth, _settings.TargetHeight, _settings.TargetFps, _settings.BitrateKbps))
                {
                    _useHardwareEncoding = true;
                    Debug.WriteLine($"Hardware encoding enabled: {_hardwareEncoder.EncoderName} (GPU: {_hardwareEncoder.IsHardwareAccelerated})");
                }
                else
                {
                    _useHardwareEncoding = false;
                    _hardwareEncoder.Dispose();
                    _hardwareEncoder = null;
                    Debug.WriteLine("Hardware encoding unavailable, using JPEG fallback");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize hardware encoder: {ex.Message}");
                _useHardwareEncoding = false;
                _hardwareEncoder = null;
            }
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

        // Cleanup streaming engine
        _streamingEngine?.Stop();
        _streamingEngine?.Dispose();
        _streamingEngine = null;

        // Cleanup hardware encoder
        _hardwareEncoder?.Dispose();
        _hardwareEncoder = null;
        _useHardwareEncoding = false;
    }

    /// <summary>
    /// Main capture loop running on dedicated thread for consistent timing.
    /// Only captures screen - encoding is done by separate EncodeLoop thread.
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

                    // Capture screen only (encoding is done by EncodeLoop)
                    var capturedBitmap = CaptureScreen();

                    captureTimer.Stop();
                    _stats.CaptureTimeMs = captureTimer.Elapsed.TotalMilliseconds;

                    if (capturedBitmap != null)
                    {
                        // Queue captured bitmap for encoding
                        if (_captureQueue.Count < MaxCaptureQueueSize)
                        {
                            _captureQueue.Enqueue(capturedBitmap);
                            _captureSignal.Set(); // Wake up encode thread
                        }
                        else
                        {
                            // Capture queue full - drop oldest and add new
                            if (_captureQueue.TryDequeue(out var oldBitmap))
                            {
                                oldBitmap.Dispose();
                            }
                            _captureQueue.Enqueue(capturedBitmap);
                            _captureSignal.Set();
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
    /// Encoding loop running on dedicated thread.
    /// Encodes captured bitmaps and queues them for sending.
    /// </summary>
    private void EncodeLoop(CancellationToken cancellationToken)
    {
        var encodeTimer = new Stopwatch();
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested && _isSharing)
        {
            try
            {
                // Wait for signal or timeout (16ms = ~60fps max check rate)
                _captureSignal.WaitOne(16);

                // Process all queued captures
                while (_captureQueue.TryDequeue(out var bitmap))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        bitmap.Dispose();
                        break;
                    }

                    encodeTimer.Restart();

                    // Encode the bitmap
                    var frameData = EncodeBitmap(bitmap);
                    bitmap.Dispose(); // Always dispose after encoding

                    encodeTimer.Stop();

                    if (frameData != null && frameData.Length > 0)
                    {
                        // Queue encoded frame for sending
                        if (_frameQueue.Count < MaxFrameQueueSize)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Encode error: {ex.Message}");
            }
        }

        // Cleanup remaining items
        while (_captureQueue.TryDequeue(out var bitmap))
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// Captures screen and returns a new bitmap (for EncodeLoop to process).
    /// </summary>
    private Bitmap? CaptureScreen()
    {
        if (_currentDisplay == null || _captureBitmap == null || _captureGraphics == null)
            return null;

        try
        {
            // Capture screen to reusable bitmap
            _captureGraphics.CopyFromScreen(
                _currentDisplay.Left,
                _currentDisplay.Top,
                0, 0,
                new Size(_currentDisplay.Width, _currentDisplay.Height),
                CopyPixelOperation.SourceCopy);

            // Create a copy for the encode queue
            int targetWidth, targetHeight;
            lock (_settingsLock)
            {
                targetWidth = _settings.TargetWidth;
                targetHeight = _settings.TargetHeight;
            }

            // Resize if needed, otherwise clone
            if (_currentDisplay.Width != targetWidth || _currentDisplay.Height != targetHeight)
            {
                var resized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.CompositingQuality = CompositingQuality.HighSpeed;
                    g.SmoothingMode = SmoothingMode.HighSpeed;
                    g.DrawImage(_captureBitmap, 0, 0, targetWidth, targetHeight);
                }
                return resized;
            }
            else
            {
                return (Bitmap)_captureBitmap.Clone();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CaptureScreen error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Encodes a bitmap using adaptive streaming, hardware H.264, or JPEG fallback.
    /// Called from EncodeLoop on dedicated encoding thread.
    /// </summary>
    private byte[]? EncodeBitmap(Bitmap bitmap)
    {
        try
        {
            // Try adaptive streaming engine (smart compression + delta encoding)
            if (_useAdaptiveStreaming && _streamingEngine != null)
            {
                var encodedFrame = _streamingEngine.ProcessFrame(bitmap, _frameNumber);
                if (encodedFrame != null && encodedFrame.Data.Length > 0)
                {
                    // Update stats from streaming engine
                    var engineStats = _streamingEngine.Stats;
                    _stats.AverageFrameSize = (int)engineStats.LastFrameSizeBytes;
                    return encodedFrame.Data;
                }
                // Frame was skipped (no changes) - return null to skip sending
                if (encodedFrame == null && _streamingEngine.Stats.FramesSkipped > 0)
                {
                    return null;
                }
                // Fall through to other encoders if adaptive fails
            }

            // Try hardware encoding (H.264 with NVENC/AMF/QSV)
            if (_useHardwareEncoding && _hardwareEncoder != null)
            {
                var h264Data = _hardwareEncoder.EncodeFrame(bitmap, _frameNumber);
                if (h264Data != null && h264Data.Length > 0)
                {
                    return h264Data;
                }
                // Fall through to JPEG if hardware encoding fails
            }

            // Fallback: Encode to JPEG
            int jpegQuality;
            int maxFrameSizeKb;
            bool adaptiveQuality;
            lock (_settingsLock)
            {
                jpegQuality = _settings.JpegQuality;
                maxFrameSizeKb = _settings.MaxFrameSizeKb;
                adaptiveQuality = _settings.AdaptiveQuality;
            }

            using var ms = new MemoryStream();
            if (_jpegEncoder != null)
            {
                // Set quality parameter for each encode (quality may change with adaptive settings)
                using var encodeParams = new EncoderParameters(1);
                encodeParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
                bitmap.Save(ms, _jpegEncoder, encodeParams);
            }
            else
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
            }

            var frameData = ms.ToArray();

            // Adaptive quality: compress more if frame too large
            if (adaptiveQuality && frameData.Length > maxFrameSizeKb * 1024)
            {
                frameData = RecompressFrame(frameData, Math.Max(20, jpegQuality - 15));
            }

            return frameData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EncodeBitmap error: {ex.Message}");
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
    /// Yields to voice audio to prevent choppy audio during screen sharing
    /// Sends frames at a steady rate to prevent burst/lag issues
    /// </summary>
    private async Task SendLoop(CancellationToken cancellationToken)
    {
        var sendTimer = new Stopwatch();
        var frameIntervalStopwatch = Stopwatch.StartNew();
        long lastSendTime = 0;
        var frameCounter = 0;
        var orchestrator = StreamingOrchestrator.Instance;

        while (!cancellationToken.IsCancellationRequested && _isSharing)
        {
            try
            {
                frameCounter++;

                // Calculate frame interval for pacing - send at target FPS rate
                // Use orchestrator's recommended FPS if network is congested
                var targetFps = orchestrator.GetRecommendedFps(_settings.TargetFps);
                var frameIntervalMs = 1000.0 / targetFps;
                var now = frameIntervalStopwatch.ElapsedMilliseconds;
                var elapsed = now - lastSendTime;

                // VOICE PRIORITY: Use orchestrator for coordinated voice/video handling
                var voiceDelay = orchestrator.GetVideoYieldDelay();
                if (voiceDelay > 0)
                {
                    await Task.Delay(voiceDelay, cancellationToken).ConfigureAwait(false);
                }

                // Check if this frame should be skipped (voice active or network congested)
                if (orchestrator.ShouldSkipVideoFrame(frameCounter))
                {
                    if (_frameQueue.TryDequeue(out _))
                    {
                        _framesDroppedThisSecond++;
                        orchestrator.SignalVideoFrameDropped();
                    }
                    continue;
                }

                // Also check local voice flag for backward compatibility
                if (_voiceActive && frameCounter % 3 == 0)
                {
                    await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                }

                // Wait for next frame interval to maintain steady send rate
                if (elapsed < frameIntervalMs)
                {
                    var waitTime = (int)(frameIntervalMs - elapsed);
                    if (waitTime > 1)
                    {
                        await Task.Delay(waitTime - 1, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Get next frame from queue - don't skip all frames, just keep queue manageable
                // Only skip if queue is backing up significantly (>3 frames)
                CapturedFrame? frameToSend = null;
                if (_frameQueue.TryDequeue(out var frame))
                {
                    frameToSend = frame;
                    // If queue is too backed up, skip some to catch up (but not all)
                    if (_frameQueue.Count > 3)
                    {
                        // Skip half the backlog, not all of it
                        var skipCount = _frameQueue.Count / 2;
                        for (int i = 0; i < skipCount && _frameQueue.TryDequeue(out var skipped); i++)
                        {
                            frameToSend = skipped; // Use the newer frame
                            _framesDroppedThisSecond++;
                        }
                    }
                }

                if (frameToSend != null && _sendFrameFunc != null && _isConnected)
                {
                    lastSendTime = frameIntervalStopwatch.ElapsedMilliseconds;
                    sendTimer.Restart();

                    try
                    {
                        // Use longer timeout to allow network latency without dropping
                        var sendTask = _sendFrameFunc(frameToSend.Data, frameToSend.Width, frameToSend.Height);
                        var timeoutTask = Task.Delay(200, cancellationToken);
                        var completed = await Task.WhenAny(sendTask, timeoutTask).ConfigureAwait(false);

                        sendTimer.Stop();
                        var sendTimeMs = sendTimer.Elapsed.TotalMilliseconds;
                        _stats.SendTimeMs = sendTimeMs;

                        // Track for adaptive quality and orchestrator
                        TrackSendTime(sendTimeMs);
                        orchestrator.RecordLatency((int)sendTimeMs);
                        orchestrator.SignalVideoFrameSent();

                        // Update stats
                        _framesSentThisSecond++;
                        _bytesSentThisSecond += frameToSend.Data.Length;
                        _stats.FramesSent++;
                        _stats.BytesSent += frameToSend.Data.Length;

                        // Fire events for local display
                        OnFrameReady?.Invoke("self", frameToSend.Data, frameToSend.Width, frameToSend.Height);
                        OnLocalFrameReady?.Invoke(frameToSend.Data, frameToSend.Width, frameToSend.Height);

                        // VOICE PRIORITY: Extra yield after sending when voice active
                        if (_voiceActive || orchestrator.IsVoiceActive)
                        {
                            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                        }
                        await Task.Yield();
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
        if (frameData == null || frameData.Length == 0) return;

        // Detect frame type: JPEG starts with 0xFF 0xD8, H.264 NAL units start with 0x00 0x00
        var isJpeg = frameData.Length >= 2 && frameData[0] == 0xFF && frameData[1] == 0xD8;
        var isH264 = frameData.Length >= 4 &&
                    ((frameData[0] == 0x00 && frameData[1] == 0x00 && frameData[2] == 0x00 && frameData[3] == 0x01) ||
                     (frameData[0] == 0x00 && frameData[1] == 0x00 && frameData[2] == 0x01));

        byte[] outputData = frameData;

        // If H.264, decode to JPEG so existing consumers can display it
        if (isH264)
        {
            // First check if FFmpeg is even available
            if (!FFmpegHelper.IsAvailable)
            {
                Debug.WriteLine($"Cannot decode H.264 from {senderConnectionId} - FFmpeg not available. Install FFmpeg to view H.264 streams.");
                return;
            }

            _decoderLock.EnterWriteLock();
            try
            {
                // Check if we already have a failed decoder for this sender
                if (_h264Decoders.TryGetValue(senderConnectionId, out var existingDecoder))
                {
                    if (existingDecoder.IsDisposed || existingDecoder.DecoderName == "none")
                    {
                        // Decoder failed previously, skip H.264 decoding
                        return;
                    }
                }

                var decoder = _h264Decoders.GetOrAdd(senderConnectionId, _ =>
                {
                    var d = new HardwareVideoDecoder();
                    if (!d.Initialize())
                    {
                        Debug.WriteLine($"Failed to initialize H.264 decoder for {senderConnectionId}");
                    }
                    else
                    {
                        Debug.WriteLine($"Created H.264 decoder for {senderConnectionId}: {d.DecoderName} (HW: {d.IsHardwareAccelerated})");
                    }
                    return d;
                });

                if (!decoder.IsDisposed && decoder.DecoderName != "none")
                {
                    var (decodedData, decodedWidth, decodedHeight, stride) = decoder.DecodeFrameRaw(frameData);
                    if (decodedData != null && decodedData.Length > 0)
                    {
                        // Convert decoded BGR24 to JPEG for existing consumers
                        outputData = ConvertBgrToJpeg(decodedData, decodedWidth, decodedHeight, stride);
                        width = decodedWidth;
                        height = decodedHeight;
                    }
                    else
                    {
                        // Decode failed (might need keyframe), skip this frame silently
                        return;
                    }
                }
                else
                {
                    // Decoder not available, can't decode H.264
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"H.264 decode error for {senderConnectionId}: {ex.Message}");
                return;
            }
            finally
            {
                _decoderLock.ExitWriteLock();
            }
        }

        var frame = new ScreenFrame
        {
            SenderConnectionId = senderConnectionId,
            Data = outputData,
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

    private byte[] ConvertBgrToJpeg(byte[] bgrData, int width, int height, int stride)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var bitmapData = bitmap.LockBits(
            new DrawingRectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            // Copy row by row to handle stride differences
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(bgrData, y * stride, bitmapData.Scan0 + y * bitmapData.Stride, width * 3);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        using var ms = new MemoryStream();
        if (_jpegEncoder != null)
        {
            // Use standard quality (75) for decoded H.264 frames
            using var encodeParams = new EncoderParameters(1);
            encodeParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
            bitmap.Save(ms, _jpegEncoder, encodeParams);
        }
        else
        {
            bitmap.Save(ms, ImageFormat.Jpeg);
        }
        return ms.ToArray();
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

        // Clean up H.264 decoder for this connection
        _decoderLock.EnterWriteLock();
        try
        {
            if (_h264Decoders.TryRemove(connectionId, out var decoder))
            {
                decoder.Dispose();
                Debug.WriteLine($"Disposed H.264 decoder for {connectionId}");
            }
        }
        finally
        {
            _decoderLock.ExitWriteLock();
        }

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
        catch (Exception ex)
        {
            Debug.WriteLine($"Dispose: Stop sharing failed: {ex.Message}");
        }

        // Disconnect synchronously
        Disconnect();

        // Final cleanup
        CleanupCaptureResources();

        try { _captureSignal?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: CaptureSignal dispose failed: {ex.Message}"); }
        try { _decoderLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: DecoderLock dispose failed: {ex.Message}"); }

        // Clear any remaining queues
        while (_captureQueue.TryDequeue(out var bitmap)) { bitmap?.Dispose(); }
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
