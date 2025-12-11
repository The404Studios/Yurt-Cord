using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class VoiceCallDashboard : UserControl
{
    private readonly IVoiceService _voiceService = null!;
    private readonly IApiService _apiService = null!;
    private readonly ObservableCollection<VoiceParticipant> _participants = new();
    private bool _isMuted;
    private bool _isDeafened;
    private string? _currentScreenSharerConnectionId;
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.Now;

    // Channel name mapping
    private static readonly Dictionary<string, string> ChannelDisplayNames = new()
    {
        { "general-voice", "General Voice" },
        { "music-voice", "Music" },
        { "marketplace-voice", "Marketplace Deals" }
    };

    public VoiceCallDashboard()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;

        ParticipantsItemsControl.ItemsSource = _participants;

        SetupEventHandlers();
        UpdateSelfInfo();
        LoadOutputDevices();
    }

    private void LoadOutputDevices()
    {
        try
        {
            var devices = _voiceService.GetOutputDevices();
            OutputDeviceCombo.Items.Clear();

            foreach (var device in devices)
            {
                OutputDeviceCombo.Items.Add(new ComboBoxItem
                {
                    Content = device.Name,
                    Tag = device.DeviceNumber
                });
            }

            // Select first device by default
            if (OutputDeviceCombo.Items.Count > 0)
            {
                OutputDeviceCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // Audio device enumeration failed
        }
    }

    private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo.SelectedItem is ComboBoxItem item && item.Tag is int deviceNumber)
        {
            _voiceService.SetOutputDevice(deviceNumber);
        }
    }

    private void SetupEventHandlers()
    {
        _voiceService.OnUserJoinedVoice += user =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_participants.Any(p => p.ConnectionId == user.ConnectionId))
                {
                    _participants.Add(new VoiceParticipant(user));
                }
                UpdateParticipantCount();
            });
        };

        _voiceService.OnUserLeftVoice += user =>
        {
            Dispatcher.Invoke(() =>
            {
                var participant = _participants.FirstOrDefault(p => p.ConnectionId == user.ConnectionId);
                if (participant != null)
                {
                    _participants.Remove(participant);
                }
                UpdateParticipantCount();

                // If this user was sharing, clear the stream
                if (_currentScreenSharerConnectionId == user.ConnectionId)
                {
                    ClearScreenShare();
                }
            });
        };

        _voiceService.OnVoiceChannelUsers += users =>
        {
            Dispatcher.Invoke(() =>
            {
                _participants.Clear();
                foreach (var user in users)
                {
                    _participants.Add(new VoiceParticipant(user));

                    // Auto-watch if someone is already screen sharing
                    if (user.IsScreenSharing && _currentScreenSharerConnectionId == null)
                    {
                        _currentScreenSharerConnectionId = user.ConnectionId;
                        StreamerNameText.Text = user.Username;
                        StreamInfoPanel.Visibility = Visibility.Visible;
                        NoStreamPanel.Visibility = Visibility.Collapsed;
                        ScreenShareImage.Visibility = Visibility.Visible;
                    }
                }
                UpdateParticipantCount();
                UpdateChannelName();
            });
        };

        _voiceService.OnUserSpeaking += (connectionId, username, isSpeaking, audioLevel) =>
        {
            Dispatcher.Invoke(() =>
            {
                var participant = _participants.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (participant != null)
                {
                    participant.IsSpeaking = isSpeaking;
                    participant.AudioLevel = audioLevel;
                }
            });
        };

        _voiceService.OnLocalAudioLevel += level =>
        {
            Dispatcher.Invoke(() =>
            {
                var maxWidth = MicLevelBar.Parent is Border parent ? parent.ActualWidth : 200;
                MicLevelBar.Width = level * maxWidth;

                MicLevelIcon.Text = _isMuted ? "🔇" : "🎤";
                MicLevelIcon.Foreground = _isMuted
                    ? (System.Windows.Media.Brush)FindResource("TextMutedBrush")
                    : (level > 0.1
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 181, 129))
                        : (System.Windows.Media.Brush)FindResource("TextMutedBrush"));
            });
        };

        _voiceService.OnScreenFrameReceived += (senderConnectionId, frameData, width, height) =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Update FPS counter
                    _frameCount++;
                    var now = DateTime.Now;
                    if ((now - _lastFpsUpdate).TotalSeconds >= 1)
                    {
                        StreamFpsText.Text = $"{_frameCount} FPS | {width}x{height}";
                        _frameCount = 0;
                        _lastFpsUpdate = now;
                    }

                    // Show stream info
                    _currentScreenSharerConnectionId = senderConnectionId;
                    var sharer = _participants.FirstOrDefault(p => p.ConnectionId == senderConnectionId);
                    StreamerNameText.Text = sharer?.Username ?? "Unknown";
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    ScreenShareImage.Visibility = Visibility.Visible;

                    // Display frame
                    using var ms = new MemoryStream(frameData);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ScreenShareImage.Source = bitmap;
                }
                catch
                {
                    // Ignore frame errors
                }
            });
        };

        _voiceService.OnUserScreenShareChanged += (connectionId, isSharing) =>
        {
            Dispatcher.Invoke(() =>
            {
                var participant = _participants.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (participant != null)
                {
                    participant.IsScreenSharing = isSharing;
                }

                if (isSharing)
                {
                    // Auto-watch new screen share if not currently watching anyone
                    // or switch to the new sharer
                    _currentScreenSharerConnectionId = connectionId;
                    StreamerNameText.Text = participant?.Username ?? "Unknown";
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    ScreenShareImage.Visibility = Visibility.Visible;
                    _frameCount = 0;
                    _lastFpsUpdate = DateTime.Now;
                }
                else if (_currentScreenSharerConnectionId == connectionId)
                {
                    // Find another active sharer if available
                    var otherSharer = _participants.FirstOrDefault(p => p.IsScreenSharing && p.ConnectionId != connectionId);
                    if (otherSharer != null)
                    {
                        _currentScreenSharerConnectionId = otherSharer.ConnectionId;
                        StreamerNameText.Text = otherSharer.Username;
                    }
                    else
                    {
                        ClearScreenShare();
                    }
                }
            });
        };

        _voiceService.OnUserDisconnectedByAdmin += reason =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(reason, "Disconnected", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        };
    }

    private void UpdateSelfInfo()
    {
        var user = _apiService.CurrentUser;
        if (user != null)
        {
            SelfUsernameText.Text = user.Username;
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                try
                {
                    SelfAvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
                }
                catch { }
            }
        }
    }

    private void UpdateChannelName()
    {
        var channelId = _voiceService.CurrentChannelId;
        if (channelId != null)
        {
            ChannelNameText.Text = ChannelDisplayNames.TryGetValue(channelId, out var name)
                ? name : channelId;
        }
    }

    private void UpdateParticipantCount()
    {
        ParticipantCountText.Text = $"{_participants.Count} participant{(_participants.Count != 1 ? "s" : "")}";
    }

    private void ClearScreenShare()
    {
        _currentScreenSharerConnectionId = null;
        ScreenShareImage.Source = null;
        ScreenShareImage.Visibility = Visibility.Collapsed;
        StreamInfoPanel.Visibility = Visibility.Collapsed;
        NoStreamPanel.Visibility = Visibility.Visible;
    }

    private VoiceParticipant? GetParticipantFromSender(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            var parent = menuItem.Parent;
            while (parent != null && !(parent is ContextMenu))
            {
                if (parent is MenuItem parentMenuItem)
                    parent = parentMenuItem.Parent;
                else
                    break;
            }

            if (parent is ContextMenu contextMenu && contextMenu.PlacementTarget is FrameworkElement element)
            {
                return element.Tag as VoiceParticipant ?? element.DataContext as VoiceParticipant;
            }
        }
        return null;
    }

    #region Button Click Handlers

    private async void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService.IsScreenSharing)
        {
            await _voiceService.StopScreenShareAsync();
            ScreenShareIcon.Text = "🖥";
            ScreenShareBtn.Background = null;
            ScreenShareBtn.ToolTip = "Share Screen";
        }
        else
        {
            // Show screen share picker dialog
            var picker = new ScreenSharePicker(_voiceService)
            {
                Owner = Window.GetWindow(this)
            };

            if (picker.ShowDialog() == true && picker.SelectedDisplay != null)
            {
                // Start screen sharing with the selected display
                await _voiceService.StartScreenShareAsync(picker.SelectedDisplay);
                ScreenShareIcon.Text = "🛑";
                ScreenShareBtn.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(67, 181, 129));
                ScreenShareBtn.ToolTip = $"Stop Sharing ({picker.SelectedDisplay.FriendlyName})";
            }
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;
        MuteIcon.Text = _isMuted ? "🔇" : "🎤";
        MuteIcon.Opacity = _isMuted ? 0.5 : 1;
        SelfStatusText.Text = _isMuted ? "Muted" : "Connected";
        SelfStatusText.Foreground = _isMuted
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 66, 69))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 181, 129));
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;
        DeafenIcon.Text = _isDeafened ? "🔈" : "🔊";
        DeafenIcon.Opacity = _isDeafened ? 0.5 : 1;

        if (_isDeafened)
        {
            _isMuted = true;
            _voiceService.IsMuted = true;
            MuteIcon.Text = "🔇";
            MuteIcon.Opacity = 0.5;
            SelfStatusText.Text = "Deafened";
            SelfStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69));
        }
        else
        {
            SelfStatusText.Text = _isMuted ? "Muted" : "Connected";
        }
    }

    private async void Leave_Click(object sender, RoutedEventArgs e)
    {
        await _voiceService.LeaveVoiceChannelAsync();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText != null)
        {
            VolumeText.Text = $"{(int)e.NewValue}%";
        }
        // Note: Would need to implement master volume in VoiceService
    }

    #endregion

    #region Context Menu Handlers

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var isMuted = _voiceService.IsUserMuted(participant.ConnectionId);
        _voiceService.SetUserMuted(participant.ConnectionId, !isMuted);

        var action = isMuted ? "Unmuted" : "Muted";
        MessageBox.Show($"{action} {participant.Username} for yourself", $"User {action}",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AdjustVolume_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var currentVolume = _voiceService.GetUserVolume(participant.ConnectionId) * 100;
        var input = InputDialog.Show(
            "Adjust User Volume",
            $"Enter volume percentage for {participant.Username} (0-200):",
            currentVolume.ToString("F0"));

        if (!string.IsNullOrEmpty(input) && float.TryParse(input, out var volume))
        {
            volume = Math.Clamp(volume, 0, 200);
            _voiceService.SetUserVolume(participant.ConnectionId, volume / 100f);
            MessageBox.Show($"Set {participant.Username}'s volume to {volume}%", "Volume Adjusted",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void WatchScreen_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        // If they're sharing, focus on their stream
        if (participant.IsScreenSharing || _currentScreenSharerConnectionId == participant.ConnectionId)
        {
            // Already showing their stream in main view
            MessageBox.Show($"Now watching {participant.Username}'s screen", "Screen Share",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            // Open in separate window
            var viewer = new ScreenShareViewer(_voiceService, participant.ConnectionId, participant.Username);
            viewer.Show();
        }
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var navService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navService?.NavigateToProfile();
    }

    #endregion
}

/// <summary>
/// Wrapper class for voice participants with INotifyPropertyChanged support
/// </summary>
public class VoiceParticipant : INotifyPropertyChanged
{
    private bool _isSpeaking;
    private bool _isMuted;
    private bool _isDeafened;
    private bool _isScreenSharing;
    private double _audioLevel;

    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;

    public bool IsSpeaking
    {
        get => _isSpeaking;
        set { _isSpeaking = value; OnPropertyChanged(nameof(IsSpeaking)); }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(nameof(IsMuted)); }
    }

    public bool IsDeafened
    {
        get => _isDeafened;
        set { _isDeafened = value; OnPropertyChanged(nameof(IsDeafened)); }
    }

    public bool IsScreenSharing
    {
        get => _isScreenSharing;
        set { _isScreenSharing = value; OnPropertyChanged(nameof(IsScreenSharing)); }
    }

    public double AudioLevel
    {
        get => _audioLevel;
        set { _audioLevel = value; OnPropertyChanged(nameof(AudioLevel)); }
    }

    public VoiceParticipant() { }

    public VoiceParticipant(VoiceUserState state)
    {
        ConnectionId = state.ConnectionId;
        UserId = state.UserId;
        Username = state.Username;
        AvatarUrl = state.AvatarUrl;
        IsSpeaking = state.IsSpeaking;
        IsMuted = state.IsMuted;
        IsDeafened = state.IsDeafened;
        AudioLevel = state.AudioLevel;
        IsScreenSharing = state.IsScreenSharing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

