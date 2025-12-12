using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

/// <summary>
/// Screen share viewer with real-time frame display and statistics.
/// </summary>
public partial class ScreenShareViewer : Window
{
    private readonly IVoiceService _voiceService;
    private readonly string _sharerConnectionId;
    private readonly string _sharerUsername;

    // Frame statistics
    private int _frameCount;
    private int _framesThisSecond;
    private long _totalBytesReceived;
    private long _bytesThisSecond;
    private DateTime _lastFpsUpdate = DateTime.Now;
    private readonly DispatcherTimer _statsTimer;

    // Fullscreen state
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private Rect _previousBounds;

    // Performance tracking
    private readonly Queue<double> _fpsHistory = new();
    private const int FpsHistorySize = 5;
    private bool _showStats;
    private int _lastWidth;
    private int _lastHeight;

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

        // Setup stats timer (update stats every second)
        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += StatsTimer_Tick;
        _statsTimer.Start();

        // Focus window for keyboard input
        Loaded += (s, e) => Focus();
    }

    private void OnScreenFrameReceived(string senderConnectionId, byte[] frameData, int width, int height)
    {
        if (senderConnectionId != _sharerConnectionId) return;

        _totalBytesReceived += frameData.Length;
        _bytesThisSecond += frameData.Length;
        _framesThisSecond++;
        _lastWidth = width;
        _lastHeight = height;

        // Display frame immediately on UI thread
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            try
            {
                // Hide no stream message
                NoStreamPanel.Visibility = Visibility.Collapsed;

                // Convert byte array to BitmapImage
                using var ms = new MemoryStream(frameData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                ScreenImage.Source = bitmap;
            }
            catch
            {
                // Ignore frame decoding errors
            }
        });
    }

    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        var fps = _framesThisSecond;
        _fpsHistory.Enqueue(fps);
        if (_fpsHistory.Count > FpsHistorySize)
            _fpsHistory.Dequeue();

        var avgFps = _fpsHistory.Count > 0 ? _fpsHistory.Average() : 0;

        // Update display
        FpsText.Text = $"{avgFps:F0} FPS";
        if (_lastWidth > 0 && _lastHeight > 0)
            ResolutionText.Text = $"{_lastWidth}x{_lastHeight}";

        // Calculate bitrate
        var bitrateMbps = (_bytesThisSecond * 8.0) / 1_000_000.0;
        BitrateText.Text = bitrateMbps >= 1 ? $"{bitrateMbps:F1} Mbps" : $"{(_bytesThisSecond * 8.0 / 1000.0):F0} Kbps";

        // Update quality badge based on resolution and FPS
        UpdateQualityBadge(_lastWidth, _lastHeight, avgFps);

        // Update detailed stats panel
        if (_showStats)
        {
            FrameBufferText.Text = $"Frames: {_frameCount}";
            DroppedFramesText.Text = $"This sec: {_framesThisSecond}";
            LatencyText.Text = $"Avg FPS: {avgFps:F1}";
            TotalBytesText.Text = $"Received: {_totalBytesReceived / (1024.0 * 1024.0):F1} MB";
        }

        _frameCount += _framesThisSecond;
        _framesThisSecond = 0;
        _bytesThisSecond = 0;
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
        else if (fps > 0)
        {
            quality = "LOW";
            badgeColor = Color.FromRgb(237, 66, 69); // Red
        }
        else
        {
            quality = "---";
            badgeColor = Color.FromRgb(114, 118, 125); // Gray
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
            _statsTimer.Stop();
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
        _statsTimer.Stop();
        _voiceService.OnScreenFrameReceived -= OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged -= OnUserScreenShareChanged;
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
}
