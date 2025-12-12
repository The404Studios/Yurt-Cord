using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class ScreenShareViewer : Window
{
    private readonly IVoiceService _voiceService;
    private readonly string _sharerConnectionId;
    private readonly string _sharerUsername;
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.Now;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;

    public ScreenShareViewer(IVoiceService voiceService, string sharerConnectionId, string sharerUsername)
    {
        InitializeComponent();

        _voiceService = voiceService;
        _sharerConnectionId = sharerConnectionId;
        _sharerUsername = sharerUsername;

        SharerNameText.Text = $"{sharerUsername}'s Screen";

        // Subscribe to screen frames
        _voiceService.OnScreenFrameReceived += OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged += OnUserScreenShareChanged;
    }

    private void OnScreenFrameReceived(string senderConnectionId, byte[] frameData, int width, int height)
    {
        if (senderConnectionId != _sharerConnectionId) return;

        // Use BeginInvoke for non-blocking async UI updates to prevent stuttering
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            try
            {
                // Update frame count for FPS
                _frameCount++;
                var now = DateTime.Now;
                if ((now - _lastFpsUpdate).TotalSeconds >= 1)
                {
                    FpsText.Text = $"{_frameCount} FPS";
                    _frameCount = 0;
                    _lastFpsUpdate = now;
                }

                // Update resolution display
                ResolutionText.Text = $"{width}x{height}";

                // Hide no stream message
                NoStreamPanel.Visibility = Visibility.Collapsed;

                // Convert byte array to BitmapImage
                using var ms = new MemoryStream(frameData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                ScreenImage.Source = bitmap;
            }
            catch
            {
                // Ignore frame errors
            }
        });
    }

    private void OnUserScreenShareChanged(string connectionId, bool isSharing)
    {
        if (connectionId != _sharerConnectionId || isSharing) return;

        // Sharer stopped sharing
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show($"{_sharerUsername} stopped sharing their screen.", "Screen Share Ended",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        });
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreen)
        {
            // Exit fullscreen
            WindowState = _previousWindowState;
            WindowStyle = _previousWindowStyle;
            _isFullscreen = false;
        }
        else
        {
            // Enter fullscreen
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _voiceService.OnScreenFrameReceived -= OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged -= OnUserScreenShareChanged;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Escape to exit fullscreen
        if (e.Key == System.Windows.Input.Key.Escape && _isFullscreen)
        {
            Fullscreen_Click(this, new RoutedEventArgs());
        }
    }
}
