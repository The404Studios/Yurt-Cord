using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool _isScreenSharing;

    // Blade UI theme colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0, 255, 159));       // #00FF9F
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(255, 68, 102));        // #FF4466
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(160, 176, 200));     // #A0B0C8
    private static readonly SolidColorBrush WhiteBrush = new(Color.FromRgb(232, 240, 255));     // #E8F0FF
    private static readonly SolidColorBrush DarkBrush = new(Color.FromRgb(2, 4, 8));            // #020408

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
                UpdateEmptyState();
            }
        });
    }

    private void OnGroupCallParticipantLeft(string callId, string userId)
    {
        Dispatcher.Invoke(() =>
        {
            var participant = _participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                _participants.Remove(participant);
                UpdateParticipantCount();
                UpdateEmptyState();
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

    private void OnLocalAudioLevel(double level)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (MicLevelBar.Parent is Border parent)
            {
                MicLevelBar.Width = level * parent.ActualWidth;
            }

            // Update self speaking ring
            SelfSpeakingRing.Visibility = level > 0.1 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnSpeakingChanged(bool speaking)
    {
        Dispatcher.InvokeAsync(() =>
        {
            SelfSpeakingRing.Visibility = speaking ? Visibility.Visible : Visibility.Collapsed;
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load avatar: {ex.Message}");
                }
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
        UpdateEmptyState();
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
        UpdateEmptyState();
    }

    private void UpdateParticipantCount()
    {
        ParticipantCountText.Text = $"{_participants.Count} participant{(_participants.Count != 1 ? "s" : "")}";
    }

    private void UpdateEmptyState()
    {
        // Show empty state when no other participants (just self)
        EmptyCallState.Visibility = _participants.Count <= 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DurationTimer_Tick(object? sender, EventArgs e)
    {
        var duration = DateTime.UtcNow - _callStartTime;
        if (duration.TotalHours >= 1)
        {
            DurationText.Text = duration.ToString(@"h\:mm\:ss");
        }
        else
        {
            DurationText.Text = duration.ToString(@"mm\:ss");
        }
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
            var query = InviteSearchBox.Text?.ToLower() ?? "";

            foreach (var friend in friends.Where(f =>
                !participantIds.Contains(f.UserId) &&
                (string.IsNullOrEmpty(query) || f.Username.ToLower().Contains(query))))
            {
                _friends.Add(friend);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load friends: {ex.Message}");
        }
    }

    private void InviteSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = InviteSearchBox.Text ?? "";
        InviteSearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Re-filter friends based on search
        LoadFriends();
    }

    private void Participant_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is GroupCallParticipantDto participant)
        {
            // Show participant context menu or profile
            // For now, just log the click
            System.Diagnostics.Debug.WriteLine($"Clicked on participant: {participant.Username}");
        }
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
        CloseInviteModal();
    }

    private void InviteModal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == InviteModal)
        {
            CloseInviteModal();
        }
    }

    private void CloseInviteModal()
    {
        // Clear state when closing
        InviteModal.Visibility = Visibility.Collapsed;
        _friends.Clear();
        InviteSearchBox.Text = string.Empty;
    }

    private void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        _isScreenSharing = !_isScreenSharing;

        if (_isScreenSharing)
        {
            _ = _voiceService.StartScreenShareAsync();
            ScreenSharePath.Fill = WhiteBrush;
            ScreenShareBtn.Style = (Style)FindResource("CallControlButtonActive");
        }
        else
        {
            _ = _voiceService.StopScreenShareAsync();
            ScreenSharePath.Fill = MutedBrush;
            ScreenShareBtn.Style = (Style)FindResource("CallControlButton");
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;

        if (_isMuted)
        {
            // Show muted icon with strike-through
            MutePath.Data = Geometry.Parse("M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23c.56-.98.9-2.09.9-3.28zm-4.02.17c0-.06.02-.11.02-.17V5c0-1.66-1.34-3-3-3S9 3.34 9 5v.18l5.98 5.99zM4.27 3L3 4.27l6.01 6.01V11c0 1.66 1.33 3 2.99 3 .22 0 .44-.03.65-.08l1.66 1.66c-.71.33-1.5.52-2.31.52-2.76 0-5.3-2.1-5.3-5.1H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c.91-.13 1.77-.45 2.54-.9L19.73 21 21 19.73 4.27 3z");
            MutePath.Fill = DarkBrush;
            MuteBtn.Style = (Style)FindResource("CallControlButtonActive");
            SelfStatusText.Text = "Muted";
            SelfStatusText.Foreground = RedBrush;
        }
        else
        {
            // Show normal mic icon
            MutePath.Data = Geometry.Parse("M12 14c1.66 0 2.99-1.34 2.99-3L15 5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72h-1.7z");
            MutePath.Fill = MutedBrush;
            MuteBtn.Style = (Style)FindResource("CallControlButton");
            SelfStatusText.Text = _isDeafened ? "Deafened" : "Connected";
            SelfStatusText.Foreground = _isDeafened ? RedBrush : GreenBrush;
        }
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;

        if (_isDeafened)
        {
            // Show deafened icon with strike-through
            DeafenPath.Data = Geometry.Parse("M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z");
            DeafenPath.Fill = DarkBrush;
            DeafenBtn.Style = (Style)FindResource("CallControlButtonActive");

            // Deafening also mutes
            _isMuted = true;
            _voiceService.IsMuted = true;
            MutePath.Data = Geometry.Parse("M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23c.56-.98.9-2.09.9-3.28zm-4.02.17c0-.06.02-.11.02-.17V5c0-1.66-1.34-3-3-3S9 3.34 9 5v.18l5.98 5.99zM4.27 3L3 4.27l6.01 6.01V11c0 1.66 1.33 3 2.99 3 .22 0 .44-.03.65-.08l1.66 1.66c-.71.33-1.5.52-2.31.52-2.76 0-5.3-2.1-5.3-5.1H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c.91-.13 1.77-.45 2.54-.9L19.73 21 21 19.73 4.27 3z");
            MutePath.Fill = DarkBrush;
            MuteBtn.Style = (Style)FindResource("CallControlButtonActive");

            SelfStatusText.Text = "Deafened";
            SelfStatusText.Foreground = RedBrush;
        }
        else
        {
            // Show normal headphone icon
            DeafenPath.Data = Geometry.Parse("M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z");
            DeafenPath.Fill = MutedBrush;
            DeafenBtn.Style = (Style)FindResource("CallControlButton");

            SelfStatusText.Text = _isMuted ? "Muted" : "Connected";
            SelfStatusText.Foreground = _isMuted ? RedBrush : GreenBrush;
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
