using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public partial class IncomingCallNotification : UserControl
{
    public enum CallType
    {
        Voice,
        Video,
        GroupVoice,
        GroupVideo
    }

    public class IncomingCall
    {
        public string CallId { get; set; } = string.Empty;
        public CallType Type { get; set; }
        public string CallerId { get; set; } = string.Empty;
        public string CallerName { get; set; } = string.Empty;
        public string CallerAvatarUrl { get; set; } = string.Empty;
        public string? CallName { get; set; }
        public List<ParticipantInfo>? Participants { get; set; }
    }

    public class ParticipantInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
    }

    private IncomingCall? _currentCall;
    private readonly DispatcherTimer _autoDeclineTimer;
    private int _remainingSeconds = 30;

    public event EventHandler<bool>? CallAccepted; // bool = withVideo
    public event EventHandler? CallDeclined;
    public event EventHandler? DeclineWithMessageRequested;

    public IncomingCallNotification()
    {
        InitializeComponent();

        _autoDeclineTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoDeclineTimer.Tick += AutoDeclineTimer_Tick;
    }

    public void ShowCall(IncomingCall call)
    {
        _currentCall = call;
        _remainingSeconds = 30;

        // Set call type text
        CallTypeText.Text = call.Type switch
        {
            CallType.Voice => "Incoming Voice Call",
            CallType.Video => "Incoming Video Call",
            CallType.GroupVoice => "Incoming Group Call",
            CallType.GroupVideo => "Incoming Group Video Call",
            _ => "Incoming Call"
        };

        // Set status text
        StatusText.Text = call.Type switch
        {
            CallType.Voice => "Voice call...",
            CallType.Video => "Video call...",
            CallType.GroupVoice => "Group voice call...",
            CallType.GroupVideo => "Group video call...",
            _ => "Incoming call..."
        };

        // Set caller name
        CallerNameText.Text = call.CallerName;

        // Set call name for group calls
        if (!string.IsNullOrEmpty(call.CallName))
        {
            CallNameText.Text = call.CallName;
            CallNameText.Visibility = Visibility.Visible;
        }
        else
        {
            CallNameText.Visibility = Visibility.Collapsed;
        }

        // Set avatars based on call type
        if (call.Type == CallType.GroupVoice || call.Type == CallType.GroupVideo)
        {
            SetupGroupCallAvatars(call);
        }
        else
        {
            SetupSingleCallAvatar(call);
        }

        // Show video option for video calls
        VideoCallButton.Visibility = (call.Type == CallType.Video || call.Type == CallType.GroupVideo)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Update timer text
        UpdateTimerText();

        // Start auto-decline timer
        _autoDeclineTimer.Start();

        Visibility = Visibility.Visible;
    }

    private void SetupSingleCallAvatar(IncomingCall call)
    {
        SingleAvatarContainer.Visibility = Visibility.Visible;
        GroupAvatarsContainer.Visibility = Visibility.Collapsed;

        try
        {
            CallerAvatar.ImageSource = new BitmapImage(
                new Uri(call.CallerAvatarUrl, UriKind.RelativeOrAbsolute));
        }
        catch
        {
            // Default avatar fallback
        }
    }

    private void SetupGroupCallAvatars(IncomingCall call)
    {
        SingleAvatarContainer.Visibility = Visibility.Collapsed;
        GroupAvatarsContainer.Visibility = Visibility.Visible;

        var participants = call.Participants ?? new List<ParticipantInfo>();

        // Set first avatar (caller)
        try
        {
            GroupAvatar1.ImageSource = new BitmapImage(
                new Uri(call.CallerAvatarUrl, UriKind.RelativeOrAbsolute));
        }
        catch { }

        // Set second avatar if available
        if (participants.Count > 0)
        {
            try
            {
                GroupAvatar2.ImageSource = new BitmapImage(
                    new Uri(participants[0].AvatarUrl, UriKind.RelativeOrAbsolute));
            }
            catch { }
        }

        // Show +N badge for additional participants
        if (participants.Count > 1)
        {
            MoreParticipantsBadge.Visibility = Visibility.Visible;
            MoreParticipantsText.Text = $"+{participants.Count - 1}";
        }
        else
        {
            MoreParticipantsBadge.Visibility = Visibility.Collapsed;
        }

        // Show participants count
        var totalCount = participants.Count + 1; // +1 for caller
        ParticipantsText.Text = $"{totalCount} participant{(totalCount > 1 ? "s" : "")} in call";
        ParticipantsText.Visibility = Visibility.Visible;
    }

    private void AutoDeclineTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        UpdateTimerText();

        if (_remainingSeconds <= 0)
        {
            _autoDeclineTimer.Stop();
            CallDeclined?.Invoke(this, EventArgs.Empty);
            Hide();
        }
    }

    private void UpdateTimerText()
    {
        TimerText.Text = $"Auto-declining in {_remainingSeconds}s";
    }

    public void Hide()
    {
        _autoDeclineTimer.Stop();
        Visibility = Visibility.Collapsed;
        _currentCall = null;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        CallAccepted?.Invoke(this, false);
        Hide();
    }

    private void VideoAccept_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        CallAccepted?.Invoke(this, true);
        Hide();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        CallDeclined?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void DeclineWithMessage_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        DeclineWithMessageRequested?.Invoke(this, EventArgs.Empty);
        // Don't hide - let the parent handle showing a message dialog
    }

    public IncomingCall? GetCurrentCall() => _currentCall;
}
