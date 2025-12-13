using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VeaMarketplace.Client.Controls;

public partial class CallParticipantTile : UserControl
{
    public class CallParticipant
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public bool IsMuted { get; set; }
        public bool IsDeafened { get; set; }
        public bool IsSpeaking { get; set; }
        public bool IsScreenSharing { get; set; }
        public bool IsVideoEnabled { get; set; }
        public bool IsConnecting { get; set; }
        public double AudioLevel { get; set; }
    }

    private CallParticipant? _participant;
    private Storyboard? _speakingPulse;

    public event EventHandler? ViewProfileRequested;
    public event EventHandler? MuteUserRequested;
    public event EventHandler? AdjustVolumeRequested;
    public event EventHandler? WatchScreenRequested;
    public event EventHandler? SendMessageRequested;
    public event EventHandler? KickUserRequested;

    public CallParticipantTile()
    {
        InitializeComponent();

        _speakingPulse = FindResource("SpeakingPulse") as Storyboard;
    }

    public void SetParticipant(CallParticipant participant)
    {
        _participant = participant;

        UsernameText.Text = participant.Username;

        // Set avatar
        try
        {
            AvatarBrush.ImageSource = new BitmapImage(new Uri(participant.AvatarUrl, UriKind.RelativeOrAbsolute));
        }
        catch
        {
            // Default avatar fallback
        }

        // Update states
        UpdateHostBadge(participant.IsHost);
        UpdateMutedState(participant.IsMuted);
        UpdateDeafenedState(participant.IsDeafened);
        UpdateSpeakingState(participant.IsSpeaking);
        UpdateScreenShareState(participant.IsScreenSharing);
        UpdateConnectingState(participant.IsConnecting);
        UpdateAudioLevel(participant.AudioLevel);

        // Show kick option if current user is host
        // This would be set by the parent control
    }

    public void UpdateHostBadge(bool isHost)
    {
        HostBadge.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateMutedState(bool isMuted)
    {
        MutedBadge.Visibility = isMuted ? Visibility.Visible : Visibility.Collapsed;
        MuteUserMenuItem.Header = isMuted ? "Unmute User" : "Mute User";
    }

    public void UpdateDeafenedState(bool isDeafened)
    {
        DeafenedBadge.Visibility = isDeafened ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateSpeakingState(bool isSpeaking)
    {
        if (isSpeaking)
        {
            SpeakingRing.Visibility = Visibility.Visible;
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(67, 181, 129));
            StatusText.Text = "Speaking...";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(67, 181, 129));

            _speakingPulse?.Begin(this, true);
        }
        else
        {
            SpeakingRing.Visibility = Visibility.Collapsed;
            MainBorder.BorderBrush = Brushes.Transparent;
            StatusText.Text = "";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(114, 118, 125));

            _speakingPulse?.Stop(this);
        }
    }

    public void UpdateScreenShareState(bool isScreenSharing)
    {
        ScreenShareBadge.Visibility = isScreenSharing ? Visibility.Visible : Visibility.Collapsed;
        WatchScreenMenuItem.Visibility = isScreenSharing ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateVideoState(bool isVideoEnabled, ImageSource? videoFrame = null)
    {
        if (isVideoEnabled && videoFrame != null)
        {
            VideoStream.Source = videoFrame;
            VideoStream.Visibility = Visibility.Visible;
            AvatarDisplay.Visibility = Visibility.Collapsed;
        }
        else
        {
            VideoStream.Visibility = Visibility.Collapsed;
            AvatarDisplay.Visibility = Visibility.Visible;
        }
    }

    public void UpdateConnectingState(bool isConnecting)
    {
        ConnectingOverlay.Visibility = isConnecting ? Visibility.Visible : Visibility.Collapsed;

        if (isConnecting)
        {
            StatusText.Text = "Connecting...";
        }
    }

    public void UpdateAudioLevel(double level)
    {
        // Level is 0.0 to 1.0
        var width = Math.Max(0, Math.Min(100, level * 100));
        VolumeLevel.Width = width;

        // Change color based on level
        if (level > 0.8)
        {
            VolumeLevel.Background = new SolidColorBrush(Color.FromRgb(237, 66, 69)); // Red
        }
        else if (level > 0.5)
        {
            VolumeLevel.Background = new SolidColorBrush(Color.FromRgb(250, 166, 26)); // Yellow
        }
        else
        {
            VolumeLevel.Background = new SolidColorBrush(Color.FromRgb(67, 181, 129)); // Green
        }
    }

    public void SetCanKick(bool canKick)
    {
        KickUserMenuItem.Visibility = canKick ? Visibility.Visible : Visibility.Collapsed;
    }

    public CallParticipant? GetParticipant() => _participant;

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        ViewProfileRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        MuteUserRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AdjustVolume_Click(object sender, RoutedEventArgs e)
    {
        AdjustVolumeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void WatchScreen_Click(object sender, RoutedEventArgs e)
    {
        WatchScreenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        SendMessageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void KickUser_Click(object sender, RoutedEventArgs e)
    {
        KickUserRequested?.Invoke(this, EventArgs.Empty);
    }
}
