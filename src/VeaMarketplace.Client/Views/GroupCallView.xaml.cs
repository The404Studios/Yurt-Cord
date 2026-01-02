using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class GroupCallView : UserControl
{
    private readonly IVoiceService _voiceService = null!;
    private readonly IApiService _apiService = null!;
    private readonly IFriendService _friendService = null!;
    private readonly ObservableCollection<GroupCallParticipantDto> _participants = [];
    private readonly ObservableCollection<FriendDto> _friends = [];
    private readonly DispatcherTimer _durationTimer;
    private DateTime _callStartTime;
    private bool _isMuted;
    private bool _isDeafened;

    public event Action? OnCallLeft;

    public GroupCallView()
    {
        InitializeComponent();

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += DurationTimer_Tick;

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _friendService = (IFriendService)App.ServiceProvider.GetService(typeof(IFriendService))!;

        ParticipantsGrid.ItemsSource = _participants;
        FriendsListControl.ItemsSource = _friends;

        Unloaded += OnUnloaded;

        SetupEventHandlers();
        InitializeSelfView();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop and cleanup timer
        _durationTimer.Stop();
        _durationTimer.Tick -= DurationTimer_Tick;

        // Unsubscribe from events to prevent memory leaks
        _voiceService.OnGroupCallUpdated -= OnGroupCallUpdated;
        _voiceService.OnGroupCallParticipantJoined -= OnGroupCallParticipantJoined;
        _voiceService.OnGroupCallParticipantLeft -= OnGroupCallParticipantLeft;
        _voiceService.OnGroupCallEnded -= OnGroupCallEnded;
        _voiceService.OnLocalAudioLevel -= OnLocalAudioLevel;
        _voiceService.OnSpeakingChanged -= OnSpeakingChanged;
    }

    private void SetupEventHandlers()
    {
        _voiceService.OnGroupCallUpdated += OnGroupCallUpdated;
        _voiceService.OnGroupCallParticipantJoined += OnGroupCallParticipantJoined;
        _voiceService.OnGroupCallParticipantLeft += OnGroupCallParticipantLeft;
        _voiceService.OnGroupCallEnded += OnGroupCallEnded;
        _voiceService.OnLocalAudioLevel += OnLocalAudioLevel;
        _voiceService.OnSpeakingChanged += OnSpeakingChanged;
    }

    private void OnGroupCallUpdated(GroupCallDto call)
    {
        Dispatcher.Invoke(() => UpdateCallInfo(call));
    }

    private void OnGroupCallParticipantJoined(string callId, GroupCallParticipantDto participant)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_participants.Any(p => p.UserId == participant.UserId))
            {
                _participants.Add(participant);
                UpdateParticipantCount();
            }
        });
    }

    private void OnGroupCallParticipantLeft(string callId, string oderId)
    {
        Dispatcher.Invoke(() =>
        {
            var participant = _participants.FirstOrDefault(p => p.UserId == oderId);
            if (participant != null)
            {
                _participants.Remove(participant);
                UpdateParticipantCount();
            }
        });
    }

    private void OnGroupCallEnded(string callId, string reason)
    {
        Dispatcher.Invoke(() =>
        {
            _durationTimer.Stop();
            OnCallLeft?.Invoke();
        });
    }

    private void OnLocalAudioLevel(float level)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MicLevelBar.Width = level * (MicLevelBar.Parent as Border)!.ActualWidth;
        });
    }

    private void OnSpeakingChanged(bool speaking)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MicLevelIcon.Foreground = speaking
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        });
    }

    private void InitializeSelfView()
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

    public void SetCallInfo(GroupCallDto call)
    {
        CallNameText.Text = call.Name;
        _callStartTime = call.StartedAt;
        _participants.Clear();
        foreach (var participant in call.Participants)
        {
            _participants.Add(participant);
        }
        UpdateParticipantCount();
        _durationTimer.Start();
    }

    private void UpdateCallInfo(GroupCallDto call)
    {
        CallNameText.Text = call.Name;
        _participants.Clear();
        foreach (var participant in call.Participants)
        {
            _participants.Add(participant);
        }
        UpdateParticipantCount();
    }

    private void UpdateParticipantCount()
    {
        ParticipantCountText.Text = $"{_participants.Count} participant{(_participants.Count != 1 ? "s" : "")}";
    }

    private void DurationTimer_Tick(object? sender, EventArgs e)
    {
        var duration = DateTime.UtcNow - _callStartTime;
        DurationText.Text = duration.ToString(@"mm\:ss");
    }

    private void Invite_Click(object sender, RoutedEventArgs e)
    {
        LoadFriends();
        InviteModal.Visibility = Visibility.Visible;
    }

    private async void LoadFriends()
    {
        try
        {
            var friends = await _friendService.GetFriendsAsync();
            _friends.Clear();

            // Filter out users already in the call
            var participantIds = _participants.Select(p => p.UserId).ToHashSet();
            foreach (var friend in friends.Where(f => !participantIds.Contains(f.UserId)))
            {
                _friends.Add(friend);
            }
        }
        catch { }
    }

    private void InviteSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = InviteSearchBox.Text?.ToLower() ?? "";
        InviteSearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Re-filter friends based on search
        LoadFriends();
    }

    private void Friend_Click(object sender, MouseButtonEventArgs e)
    {
        // Just visual feedback - actual invite happens via button
    }

    private async void InviteFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is FriendDto friend)
        {
            var callId = _voiceService.CurrentGroupCallId;
            if (callId != null)
            {
                await _voiceService.InviteToGroupCallAsync(callId, friend.UserId);
                _friends.Remove(friend);
            }
        }
    }

    private void CloseInviteModal_Click(object sender, RoutedEventArgs e)
    {
        InviteModal.Visibility = Visibility.Collapsed;
    }

    private void InviteModal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == InviteModal)
        {
            InviteModal.Visibility = Visibility.Collapsed;
        }
    }

    private void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService.IsScreenSharing)
        {
            _ = _voiceService.StopScreenShareAsync();
            ScreenShareIcon.Text = "🖥";
        }
        else
        {
            _ = _voiceService.StartScreenShareAsync();
            ScreenShareIcon.Text = "🖥";
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;
        MuteIcon.Text = _isMuted ? "🔇" : "🎤";
        SelfStatusText.Text = _isMuted ? "Muted" : "In call";
        SelfStatusText.Foreground = _isMuted
            ? (System.Windows.Media.Brush)FindResource("ErrorBrush")
            : (System.Windows.Media.Brush)FindResource("SuccessBrush");
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;
        DeafenIcon.Text = _isDeafened ? "🔈" : "🔊";
        if (_isDeafened)
        {
            // Deafening also mutes
            _isMuted = true;
            _voiceService.IsMuted = true;
            MuteIcon.Text = "🔇";
            SelfStatusText.Text = "Deafened";
            SelfStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else
        {
            SelfStatusText.Text = _isMuted ? "Muted" : "In call";
        }
    }

    private async void Leave_Click(object sender, RoutedEventArgs e)
    {
        var callId = _voiceService.CurrentGroupCallId;
        if (callId != null)
        {
            await _voiceService.LeaveGroupCallAsync(callId);
        }
        _durationTimer.Stop();
        OnCallLeft?.Invoke();
    }
}
