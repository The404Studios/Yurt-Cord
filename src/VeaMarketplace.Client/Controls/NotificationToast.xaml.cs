using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    FriendRequest,
    Message,
    Purchase,
    Achievement
}

public partial class NotificationToast : UserControl
{
    private Storyboard? _slideOutAnimation;
    private Storyboard? _iconBounceAnimation;
    private DispatcherTimer? _autoDismissTimer;
    private DoubleAnimation? _progressAnimation;
    private bool _isPaused;
    private int _remainingMs;
    private int _totalDurationMs = 5000;

    public event EventHandler? Closed;
    public event EventHandler? PrimaryActionClicked;
    public event EventHandler? SecondaryActionClicked;

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public string Message
    {
        get => MessageText.Text;
        set => MessageText.Text = value;
    }

    public NotificationToast()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        TimestampText.Text = "Just now";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var slideInAnimation = (Storyboard)FindResource("SlideInAnimation");
        _slideOutAnimation = (Storyboard)FindResource("SlideOutAnimation");
        _iconBounceAnimation = (Storyboard)FindResource("IconBounce");

        // Clone to avoid sharing issues
        slideInAnimation = slideInAnimation.Clone();
        _slideOutAnimation = _slideOutAnimation.Clone();
        _iconBounceAnimation = _iconBounceAnimation.Clone();

        _slideOutAnimation.Completed += OnSlideOutCompleted;

        // Start animations
        slideInAnimation.Begin(this);
        _iconBounceAnimation.Begin(this);

        // Set progress bar width to match container
        ProgressBar.Width = ActualWidth - 16; // Account for margins

        // Start auto-dismiss countdown
        StartCountdown();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup timer
        CleanupTimer();

        // Unsubscribe from animation events
        if (_slideOutAnimation != null)
        {
            _slideOutAnimation.Completed -= OnSlideOutCompleted;
        }

        // Unsubscribe from Loaded/Unloaded
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnSlideOutCompleted(object? sender, EventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAutoDismissTimerTick(object? sender, EventArgs e)
    {
        _autoDismissTimer?.Stop();
        if (!_isPaused)
        {
            Close();
        }
    }

    private void CleanupTimer()
    {
        if (_autoDismissTimer != null)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Tick -= OnAutoDismissTimerTick;
            _autoDismissTimer = null;
        }
    }

    private void StartCountdown()
    {
        _remainingMs = _totalDurationMs;

        // Animate progress bar
        _progressAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(_totalDurationMs)
        };
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, _progressAnimation);

        // Cleanup existing timer if any
        CleanupTimer();

        // Timer for auto-dismiss
        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_totalDurationMs)
        };
        _autoDismissTimer.Tick += OnAutoDismissTimerTick;
        _autoDismissTimer.Start();
    }

    private void PauseCountdown()
    {
        _isPaused = true;
        _autoDismissTimer?.Stop();

        // Pause progress animation
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
    }

    private void ResumeCountdown()
    {
        _isPaused = false;

        // Resume progress animation from current position
        var currentScale = ProgressScale.ScaleX;
        var remainingDuration = TimeSpan.FromMilliseconds(_totalDurationMs * currentScale);

        _progressAnimation = new DoubleAnimation
        {
            From = currentScale,
            To = 0,
            Duration = remainingDuration
        };
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, _progressAnimation);

        // Cleanup existing timer before creating new one
        CleanupTimer();

        _autoDismissTimer = new DispatcherTimer
        {
            Interval = remainingDuration
        };
        _autoDismissTimer.Tick += OnAutoDismissTimerTick;
        _autoDismissTimer.Start();
    }

    public void SetNotificationType(NotificationType type)
    {
        // Yurt Cord Teal Theme - #00B4D8 is the primary accent
        var (icon, color) = type switch
        {
            NotificationType.Success => ("\u2714", Color.FromRgb(87, 242, 135)),      // Checkmark, Green
            NotificationType.Warning => ("\u26A0", Color.FromRgb(255, 179, 71)),      // Warning, Orange-Gold
            NotificationType.Error => ("\u2716", Color.FromRgb(237, 66, 69)),         // X, Red
            NotificationType.FriendRequest => ("\uD83D\uDC64", Color.FromRgb(0, 180, 216)), // Person, Yurt Cord Teal
            NotificationType.Message => ("\uD83D\uDCAC", Color.FromRgb(72, 202, 228)), // Chat, Light Teal
            NotificationType.Purchase => ("\uD83D\uDED2", Color.FromRgb(87, 242, 135)), // Cart, Green
            NotificationType.Achievement => ("\uD83C\uDFC6", Color.FromRgb(255, 179, 71)), // Trophy, Gold
            _ => ("\uD83D\uDD14", Color.FromRgb(0, 180, 216))                         // Bell, Yurt Cord Teal
        };

        IconText.Text = icon;
        var brush = new SolidColorBrush(color);
        IconBorder.Background = brush;
        AccentBar.Background = brush;
        ProgressBar.Background = brush;
    }

    public void SetDuration(int milliseconds)
    {
        _totalDurationMs = milliseconds;
    }

    public void SetActions(string? primaryText, string? secondaryText = null)
    {
        if (!string.IsNullOrEmpty(primaryText))
        {
            ActionButtonsPanel.Visibility = Visibility.Visible;
            PrimaryActionButton.Content = primaryText;
            PrimaryActionButton.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrEmpty(secondaryText))
        {
            SecondaryActionButton.Content = secondaryText;
            SecondaryActionButton.Visibility = Visibility.Visible;
        }
    }

    public void SetTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.Now - timestamp;
        TimestampText.Text = elapsed.TotalMinutes < 1 ? "Just now" :
            elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes}m ago" :
            elapsed.TotalDays < 1 ? $"{(int)elapsed.TotalHours}h ago" :
            timestamp.ToString("MMM d");
    }

    public void Close()
    {
        _autoDismissTimer?.Stop();
        _slideOutAnimation?.Begin(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Toast_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        PauseCountdown();
    }

    private void Toast_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        ResumeCountdown();
    }

    private void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        PrimaryActionClicked?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void SecondaryAction_Click(object sender, RoutedEventArgs e)
    {
        SecondaryActionClicked?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
