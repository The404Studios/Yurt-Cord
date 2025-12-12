using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class NudgeNotification : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly DispatcherTimer _shakeTimer;
    private int _shakeCount;
    private readonly double _originalLeft;
    private readonly double _originalTop;

    public NudgeNotification(NudgeDto nudge)
    {
        InitializeComponent();

        // Set content
        UsernameText.Text = nudge.FromUsername;
        TimeText.Text = "Just now";

        if (!string.IsNullOrEmpty(nudge.Message))
        {
            MessageText.Text = nudge.Message;
            MessageText.Visibility = Visibility.Visible;
        }

        // Set avatar
        if (!string.IsNullOrEmpty(nudge.FromAvatarUrl))
        {
            try
            {
                AvatarBrush.ImageSource = new BitmapImage(new Uri(nudge.FromAvatarUrl));
            }
            catch
            {
                // Use default avatar
            }
        }

        // Position in bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        _originalLeft = Left;
        _originalTop = Top;

        // Play notification sound
        try
        {
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Ignore sound errors
        }

        // Auto-close after 5 seconds
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            FadeOutAndClose();
        };
        _autoCloseTimer.Start();

        // Shake animation
        _shakeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _shakeTimer.Tick += ShakeTimer_Tick;

        // Start shake animation
        StartShake();
    }

    private void StartShake()
    {
        _shakeCount = 0;
        _shakeTimer.Start();
    }

    private void ShakeTimer_Tick(object? sender, EventArgs e)
    {
        _shakeCount++;

        if (_shakeCount > 12)
        {
            _shakeTimer.Stop();
            Left = _originalLeft;
            return;
        }

        // Alternate left and right shake
        var offset = (_shakeCount % 2 == 0) ? 8 : -8;
        Left = _originalLeft + offset;
    }

    private void FadeOutAndClose()
    {
        // Simple fade out by reducing opacity
        var fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        fadeTimer.Tick += (s, e) =>
        {
            Opacity -= 0.1;
            if (Opacity <= 0)
            {
                fadeTimer.Stop();
                Close();
            }
        };
        fadeTimer.Start();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        _shakeTimer.Stop();
        Close();
    }

    // Static method to show nudge notification
    public static void Show(NudgeDto nudge)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var notification = new NudgeNotification(nudge);
            notification.Show();
        });
    }
}
