using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

/// <summary>
/// Screen share viewer with frame buffering, performance optimizations, and statistics.
/// </summary>
public partial class ScreenShareViewer : Window
{
    private readonly IVoiceService _voiceService;
    private readonly string _sharerConnectionId;
    private readonly string _sharerUsername;

    // Frame statistics
    private int _frameCount;
    private int _droppedFrames;
    private long _totalBytesReceived;
    private long _bytesThisSecond;
    private DateTime _lastFpsUpdate = DateTime.Now;

    // Fullscreen state
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private Rect _previousBounds;

    // Frame buffering for smooth playback
    private readonly ConcurrentQueue<BufferedFrame> _frameBuffer = new();
    private const int MaxBufferSize = 3; // Keep 3 frames buffered
    private readonly DispatcherTimer _renderTimer;
    private bool _showStats;

    // Performance tracking
    private readonly Queue<double> _fpsHistory = new();
    private const int FpsHistorySize = 5;

    public ScreenShareViewer(IVoiceService voiceService, string sharerConnectionId, string sharerUsername)
    {
        InitializeComponent();

        _voiceService = voiceService;
        _sharerConnectionId = sharerConnectionId;
        _sharerUsername = sharerUsername;

        SharerNameText.Text = $"{sharerUsername}'s Screen";
        ConnectionStatusText.Text = "Connected - Waiting for frames...";

        // Subscribe to screen frames
        _voiceService.OnScreenFrameReceived += OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged += OnUserScreenShareChanged;

        // Setup render timer for smooth frame display at consistent intervals
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps target render rate
        };
        _renderTimer.Tick += RenderTimer_Tick;
        _renderTimer.Start();

        // Focus window for keyboard input
        Loaded += (s, e) => Focus();
    }

    private void OnScreenFrameReceived(string senderConnectionId, byte[] frameData, int width, int height)
    {
        if (senderConnectionId != _sharerConnectionId) return;

        _totalBytesReceived += frameData.Length;
        _bytesThisSecond += frameData.Length;

        // Add to buffer (drop oldest if full)
        if (_frameBuffer.Count >= MaxBufferSize)
        {
            _frameBuffer.TryDequeue(out _);
            _droppedFrames++;
        }

        _frameBuffer.Enqueue(new BufferedFrame
        {
            Data = frameData,
            Width = width,
            Height = height,
            ReceivedAt = DateTime.UtcNow
        });
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        // Process frame from buffer
        if (!_frameBuffer.TryDequeue(out var frame)) return;

        try
        {
            _frameCount++;
            var latencyMs = (DateTime.UtcNow - frame.ReceivedAt).TotalMilliseconds;

            // Update UI elements
            NoStreamPanel.Visibility = Visibility.Collapsed;

            // Convert byte array to BitmapImage efficiently
            using var ms = new MemoryStream(frame.Data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            ScreenImage.Source = bitmap;

            // Update stats every second
            UpdateStatistics(frame.Width, frame.Height, latencyMs, frame.Data.Length);
        }
        catch
        {
            // Ignore frame decoding errors
        }
    }

    private void UpdateStatistics(int width, int height, double latencyMs, int frameSize)
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;

        if (elapsed >= 1.0)
        {
            var fps = _frameCount / elapsed;
            _fpsHistory.Enqueue(fps);
            if (_fpsHistory.Count > FpsHistorySize)
                _fpsHistory.Dequeue();

            var avgFps = _fpsHistory.Average();

            // Update display
            FpsText.Text = $"{avgFps:F0} FPS";
            ResolutionText.Text = $"{width}x{height}";

            // Calculate bitrate
            var bitrateKbps = (_bytesThisSecond * 8.0 / 1000.0) / elapsed;
            var bitrateMbps = bitrateKbps / 1000.0;
            BitrateText.Text = bitrateMbps >= 1 ? $"{bitrateMbps:F1} Mbps" : $"{bitrateKbps:F0} Kbps";

            // Update quality badge based on resolution and FPS
            UpdateQualityBadge(width, height, avgFps);

            // Update detailed stats panel
            if (_showStats)
            {
                FrameBufferText.Text = $"Buffer: {_frameBuffer.Count}";
                DroppedFramesText.Text = $"Dropped: {_droppedFrames}";
                LatencyText.Text = $"Latency: {latencyMs:F0}ms";
                TotalBytesText.Text = $"Received: {_totalBytesReceived / (1024.0 * 1024.0):F1} MB";
            }

            _frameCount = 0;
            _bytesThisSecond = 0;
            _lastFpsUpdate = now;
        }
    }

    private void UpdateQualityBadge(int width, int height, double fps)
    {
        string quality;
        Color badgeColor;

        if (height >= 1080 && fps >= 50)
        {
            quality = "FHD";
            badgeColor = Color.FromRgb(87, 242, 135); // Green
        }
        else if (height >= 1080 && fps >= 25)
        {
            quality = "HD+";
            badgeColor = Color.FromRgb(87, 242, 135); // Green
        }
        else if (height >= 720 && fps >= 25)
        {
            quality = "HD";
            badgeColor = Color.FromRgb(88, 101, 242); // Blurple
        }
        else if (height >= 480 && fps >= 20)
        {
            quality = "SD";
            badgeColor = Color.FromRgb(254, 231, 92); // Yellow
        }
        else
        {
            quality = "LOW";
            badgeColor = Color.FromRgb(237, 66, 69); // Red
        }

        QualityText.Text = quality;
        QualityBadge.Background = new SolidColorBrush(badgeColor);
    }

    private void OnUserScreenShareChanged(string connectionId, bool isSharing)
    {
        if (connectionId != _sharerConnectionId || isSharing) return;

        // Sharer stopped sharing
        Dispatcher.Invoke(() =>
        {
            _renderTimer.Stop();
            MessageBox.Show($"{_sharerUsername} stopped sharing their screen.", "Screen Share Ended",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        });
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            // Exit fullscreen
            WindowState = _previousWindowState;
            WindowStyle = _previousWindowStyle;
            Left = _previousBounds.Left;
            Top = _previousBounds.Top;
            Width = _previousBounds.Width;
            Height = _previousBounds.Height;
            _isFullscreen = false;
        }
        else
        {
            // Enter fullscreen
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousBounds = new Rect(Left, Top, Width, Height);
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _renderTimer.Stop();
        _voiceService.OnScreenFrameReceived -= OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged -= OnUserScreenShareChanged;

        // Clear buffer
        while (_frameBuffer.TryDequeue(out _)) { }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape when _isFullscreen:
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.S:
                // Toggle stats panel
                _showStats = !_showStats;
                StatsPanel.Visibility = _showStats ? Visibility.Visible : Visibility.Collapsed;
                e.Handled = true;
                break;

            case Key.F:
                // Toggle fullscreen with F key as well
                ToggleFullscreen();
                e.Handled = true;
                break;
        }
    }

    private class BufferedFrame
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}
