using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public partial class CallControlsPanel : UserControl
{
    private bool _isMuted;
    private bool _isDeafened;
    private bool _isVideoEnabled;
    private bool _isScreenSharing;
    private bool _isNoiseSuppressionEnabled;
    private bool _isPushToTalkEnabled;
    private bool _isMoreOptionsOpen;
    private readonly DispatcherTimer _durationTimer;
    private DateTime _callStartTime;
    private TimeSpan _callDuration;

    public event EventHandler<bool>? MuteToggled;
    public event EventHandler<bool>? DeafenToggled;
    public event EventHandler<bool>? VideoToggled;
    public event EventHandler<bool>? ScreenShareToggled;
    public event EventHandler<bool>? NoiseSuppressionToggled;
    public event EventHandler<bool>? PushToTalkToggled;
    public event EventHandler? PictureInPictureRequested;
    public event EventHandler? FullscreenRequested;
    public event EventHandler? AddParticipantsRequested;
    public event EventHandler? SoundSettingsRequested;
    public event EventHandler? VideoSettingsRequested;
    public event EventHandler? CallQualityRequested;
    public event EventHandler? EndCallRequested;

    public CallControlsPanel()
    {
        InitializeComponent();

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += DurationTimer_Tick;
    }

    public void StartCall(string callType = "Voice Call")
    {
        _callStartTime = DateTime.Now;
        _callDuration = TimeSpan.Zero;
        CallStatusText.Text = callType;
        CallDurationText.Text = "00:00";
        _durationTimer.Start();

        CallTypeIcon.Text = callType.Contains("Video") ? "📹" : "📞";
    }

    public void StopCall()
    {
        _durationTimer.Stop();
    }

    public void SetDuration(TimeSpan duration)
    {
        _callDuration = duration;
        UpdateDurationDisplay();
    }

    private void DurationTimer_Tick(object? sender, EventArgs e)
    {
        _callDuration = DateTime.Now - _callStartTime;
        UpdateDurationDisplay();
    }

    private void UpdateDurationDisplay()
    {
        if (_callDuration.TotalHours >= 1)
        {
            CallDurationText.Text = _callDuration.ToString(@"h\:mm\:ss");
        }
        else
        {
            CallDurationText.Text = _callDuration.ToString(@"mm\:ss");
        }
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        MuteButton.Tag = muted;
        MuteIcon.Text = muted ? "🔇" : "🎤";
    }

    public void SetDeafened(bool deafened)
    {
        _isDeafened = deafened;
        DeafenButton.Tag = deafened;
        DeafenIcon.Text = deafened ? "🔈" : "🔊";
    }

    public void SetVideoEnabled(bool enabled)
    {
        _isVideoEnabled = enabled;
        VideoButton.Tag = enabled;
        VideoIcon.Text = enabled ? "📹" : "📷";
    }

    public void SetScreenSharing(bool sharing)
    {
        _isScreenSharing = sharing;
        ScreenShareButton.Tag = sharing;
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        SetMuted(_isMuted);
        MuteToggled?.Invoke(this, _isMuted);
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        _isDeafened = !_isDeafened;
        SetDeafened(_isDeafened);
        DeafenToggled?.Invoke(this, _isDeafened);
    }

    private void Video_Click(object sender, RoutedEventArgs e)
    {
        _isVideoEnabled = !_isVideoEnabled;
        SetVideoEnabled(_isVideoEnabled);
        VideoToggled?.Invoke(this, _isVideoEnabled);
    }

    private void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        _isScreenSharing = !_isScreenSharing;
        SetScreenSharing(_isScreenSharing);
        ScreenShareToggled?.Invoke(this, _isScreenSharing);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        _isMoreOptionsOpen = !_isMoreOptionsOpen;
        MoreOptionsPopup.Visibility = _isMoreOptionsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EndCall_Click(object sender, RoutedEventArgs e)
    {
        StopCall();
        EndCallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NoiseSuppression_Click(object sender, RoutedEventArgs e)
    {
        _isNoiseSuppressionEnabled = !_isNoiseSuppressionEnabled;
        NoiseSuppressionButton.Tag = _isNoiseSuppressionEnabled;
        NoiseSuppressionToggled?.Invoke(this, _isNoiseSuppressionEnabled);
    }

    private void PushToTalk_Click(object sender, RoutedEventArgs e)
    {
        _isPushToTalkEnabled = !_isPushToTalkEnabled;
        PushToTalkButton.Tag = _isPushToTalkEnabled;
        PushToTalkToggled?.Invoke(this, _isPushToTalkEnabled);
    }

    private void PictureInPicture_Click(object sender, RoutedEventArgs e)
    {
        PictureInPictureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        FullscreenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddParticipants_Click(object sender, RoutedEventArgs e)
    {
        AddParticipantsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SoundSettings_Click(object sender, RoutedEventArgs e)
    {
        _isMoreOptionsOpen = false;
        MoreOptionsPopup.Visibility = Visibility.Collapsed;
        SoundSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void VideoSettings_Click(object sender, RoutedEventArgs e)
    {
        _isMoreOptionsOpen = false;
        MoreOptionsPopup.Visibility = Visibility.Collapsed;
        VideoSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CallQuality_Click(object sender, RoutedEventArgs e)
    {
        _isMoreOptionsOpen = false;
        MoreOptionsPopup.Visibility = Visibility.Collapsed;
        CallQualityRequested?.Invoke(this, EventArgs.Empty);
    }
}
