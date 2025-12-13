using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public partial class UserPresenceCard : UserControl
{
    private DispatcherTimer? _elapsedTimer;
    private DateTime _activityStartTime;
    private UserActivity? _currentActivity;

    public UserPresenceCard()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _elapsedTimer?.Stop();
        _elapsedTimer = null;
    }

    public void SetActivity(UserActivity? activity)
    {
        _currentActivity = activity;

        if (activity == null || activity.Type == ActivityType.None)
        {
            RootBorder.Visibility = Visibility.Collapsed;
            _elapsedTimer?.Stop();
            return;
        }

        RootBorder.Visibility = Visibility.Visible;
        _activityStartTime = activity.StartedAt;

        // Set activity type
        ActivityTypeText.Text = activity.Type switch
        {
            ActivityType.Playing => "PLAYING A GAME",
            ActivityType.Streaming => "LIVE ON " + (activity.Platform?.ToUpperInvariant() ?? "TWITCH"),
            ActivityType.Listening => "LISTENING TO " + (activity.Platform?.ToUpperInvariant() ?? "SPOTIFY"),
            ActivityType.Watching => "WATCHING",
            ActivityType.Custom => activity.CustomLabel?.ToUpperInvariant() ?? "CUSTOM STATUS",
            ActivityType.Competing => "COMPETING IN",
            _ => "ACTIVITY"
        };

        // Set activity icon
        ActivityIcon.Text = activity.Type switch
        {
            ActivityType.Playing => "🎮",
            ActivityType.Streaming => "📺",
            ActivityType.Listening => "🎵",
            ActivityType.Watching => "🎬",
            ActivityType.Custom => activity.Emoji ?? "💬",
            ActivityType.Competing => "🏆",
            _ => "📱"
        };

        // Set activity image
        if (!string.IsNullOrEmpty(activity.LargeImageUrl))
        {
            try
            {
                ActivityImage.Source = new BitmapImage(new Uri(activity.LargeImageUrl));
                ActivityImage.Visibility = Visibility.Visible;
                ActivityIcon.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ActivityImage.Visibility = Visibility.Collapsed;
                ActivityIcon.Visibility = Visibility.Visible;
            }
        }
        else
        {
            ActivityImage.Visibility = Visibility.Collapsed;
            ActivityIcon.Visibility = Visibility.Visible;
        }

        // Set activity name
        ActivityNameText.Text = activity.Name;

        // Set activity details
        if (!string.IsNullOrEmpty(activity.Details))
        {
            ActivityDetailsText.Text = activity.Details;
            ActivityDetailsText.Visibility = Visibility.Visible;
        }
        else
        {
            ActivityDetailsText.Visibility = Visibility.Collapsed;
        }

        // Set activity badge
        if (activity.Type == ActivityType.Streaming)
        {
            ActivityBadge.Visibility = Visibility.Visible;
            ActivityBadge.Background = FindResource("AccentRedBrush") as System.Windows.Media.Brush;
            ActivityBadgeIcon.Text = "🔴";
        }
        else if (activity.Type == ActivityType.Listening)
        {
            ActivityBadge.Visibility = Visibility.Visible;
            ActivityBadge.Background = FindResource("AccentGreenBrush") as System.Windows.Media.Brush;
            ActivityBadgeIcon.Text = "▶";
        }
        else
        {
            ActivityBadge.Visibility = Visibility.Collapsed;
        }

        // Set progress for music
        if (activity.Type == ActivityType.Listening && activity.Duration > TimeSpan.Zero)
        {
            ActivityProgress.Visibility = Visibility.Visible;
            ActivityProgress.Maximum = activity.Duration.TotalSeconds;
            ActivityProgress.Value = activity.Elapsed.TotalSeconds;
        }
        else
        {
            ActivityProgress.Visibility = Visibility.Collapsed;
        }

        // Start elapsed timer
        UpdateElapsedTime();
        _elapsedTimer?.Stop();
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(activity.Type == ActivityType.Listening ? 1 : 60) };
        _elapsedTimer.Tick += (s, e) => UpdateElapsedTime();
        _elapsedTimer.Start();
    }

    private void UpdateElapsedTime()
    {
        if (_currentActivity == null) return;

        var elapsed = DateTime.UtcNow - _activityStartTime;

        if (_currentActivity.Type == ActivityType.Listening && _currentActivity.Duration > TimeSpan.Zero)
        {
            // Update progress bar
            ActivityProgress.Value = Math.Min(_currentActivity.Elapsed.TotalSeconds + (DateTime.UtcNow - _activityStartTime).TotalSeconds,
                _currentActivity.Duration.TotalSeconds);

            // Format as time remaining
            var currentPos = TimeSpan.FromSeconds(ActivityProgress.Value);
            ActivityStateText.Text = $"{FormatTime(currentPos)} / {FormatTime(_currentActivity.Duration)}";
        }
        else
        {
            // Format as elapsed time
            ActivityStateText.Text = FormatElapsed(elapsed);
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? $"{time.Hours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes < 1)
            return "just now";
        if (elapsed.TotalHours < 1)
            return $"for {(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes == 1 ? "" : "s")}";
        if (elapsed.TotalDays < 1)
            return $"for {(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours == 1 ? "" : "s")}";
        return $"for {(int)elapsed.TotalDays} day{((int)elapsed.TotalDays == 1 ? "" : "s")}";
    }

    public void ClearActivity()
    {
        _currentActivity = null;
        _elapsedTimer?.Stop();
        RootBorder.Visibility = Visibility.Collapsed;
    }
}

public class UserActivity
{
    public ActivityType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? State { get; set; }
    public string? LargeImageUrl { get; set; }
    public string? SmallImageUrl { get; set; }
    public string? Platform { get; set; }
    public string? CustomLabel { get; set; }
    public string? Emoji { get; set; }
    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Elapsed { get; set; }
    public string? Url { get; set; }
}

public enum ActivityType
{
    None,
    Playing,
    Streaming,
    Listening,
    Watching,
    Custom,
    Competing
}
