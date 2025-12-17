using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ChannelSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IApiService? _apiService;
    private readonly IVoiceService? _voiceService;
    private readonly INavigationService? _navigationService;
    private bool _isMuted;
    private bool _isDeafened;

    // Channel name mapping for display
    private static readonly Dictionary<string, string> ChannelDisplayNames = new()
    {
        { "general-voice", "General Voice" },
        { "music-voice", "Music" },
        { "marketplace-voice", "Marketplace Deals" }
    };

    public ChannelSidebar()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;

        ChannelsItemsControl.ItemsSource = _viewModel.Channels;
        VoiceUsersItemsControl.ItemsSource = _viewModel.VoiceUsers;

        // Update user panel when logged in
        Loaded += (s, e) => UpdateUserPanel();

        // Show/hide voice connected panel and users when in a voice channel
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.IsInVoiceChannel) ||
                e.PropertyName == nameof(ChatViewModel.CurrentVoiceChannel))
            {
                Dispatcher.Invoke(() =>
                {
                    var isInVoice = _viewModel.IsInVoiceChannel;
                    var currentChannel = _viewModel.CurrentVoiceChannel;

                    // Hide all voice user panels first
                    GeneralVoiceUsersPanel.Visibility = Visibility.Collapsed;
                    MusicVoiceUsersPanel.Visibility = Visibility.Collapsed;
                    MarketplaceVoiceUsersPanel.Visibility = Visibility.Collapsed;
                    VoiceUsersPanel.Visibility = Visibility.Collapsed;

                    VoiceConnectedPanel.Visibility = isInVoice ? Visibility.Visible : Visibility.Collapsed;

                    if (isInVoice && currentChannel != null)
                    {
                        VoiceChannelNameText.Text = ChannelDisplayNames.TryGetValue(
                            currentChannel, out var name) ? name : currentChannel;

                        // Show the correct voice users panel based on current channel
                        switch (currentChannel)
                        {
                            case "general-voice":
                                GeneralVoiceUsersPanel.Visibility = Visibility.Visible;
                                GeneralVoiceUsersControl.ItemsSource = _viewModel.VoiceUsers;
                                break;
                            case "music-voice":
                                MusicVoiceUsersPanel.Visibility = Visibility.Visible;
                                MusicVoiceUsersControl.ItemsSource = _viewModel.VoiceUsers;
                                break;
                            case "marketplace-voice":
                                MarketplaceVoiceUsersPanel.Visibility = Visibility.Visible;
                                MarketplaceVoiceUsersControl.ItemsSource = _viewModel.VoiceUsers;
                                break;
                        }
                    }
                });
            }
        };

        // Subscribe to audio level updates
        if (_voiceService != null)
        {
            _voiceService.OnLocalAudioLevel += level =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Update audio level bar (max width is about 150px based on panel width)
                    var maxWidth = 150.0;
                    AudioLevelBar.Width = level * maxWidth;

                    // Change icon based on mute state
                    AudioLevelIcon.Text = _isMuted ? "🔇" : "🎤";
                    AudioLevelIcon.Foreground = _isMuted
                        ? (System.Windows.Media.Brush)FindResource("TextMutedBrush")
                        : (level > 0.1
                            ? (System.Windows.Media.Brush)FindResource("AccentGreenBrush")
                            : (System.Windows.Media.Brush)FindResource("TextMutedBrush"));
                });
            };

            _voiceService.OnUserDisconnectedByAdmin += reason =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(reason, "Disconnected", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            };

            _voiceService.OnUserMovedToChannel += (channelId, movedBy) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var channelName = ChannelDisplayNames.TryGetValue(channelId, out var name) ? name : channelId;
                    VoiceChannelNameText.Text = channelName;
                    MessageBox.Show($"You were moved to {channelName}", "Moved", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            };
        }
    }

    private void UpdateUserPanel()
    {
        var user = _apiService?.CurrentUser;
        if (user != null)
        {
            UserNameText.Text = user.Username;
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                try
                {
                    UserAvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
                }
                catch { }
            }
        }
    }

    private async void ChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var button = (Button)sender;
        var channelName = button.Tag?.ToString();
        if (!string.IsNullOrEmpty(channelName))
        {
            await _viewModel.SwitchChannelCommand.ExecuteAsync(channelName);
        }
    }

    private async void VoiceChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var button = (Button)sender;
        var channelId = button.Tag?.ToString();
        if (!string.IsNullOrEmpty(channelId))
        {
            await _viewModel.JoinVoiceChannelCommand.ExecuteAsync(channelId);
        }
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;
        MicIcon.Text = _isMuted ? "🔇" : "🎤";
        MicIcon.Opacity = _isMuted ? 0.5 : 1;
    }

    private void DeafenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;
        DeafenIcon.Text = _isDeafened ? "🔈" : "🔊";
        DeafenIcon.Opacity = _isDeafened ? 0.5 : 1;

        if (_isDeafened)
        {
            _isMuted = true;
            _voiceService.IsMuted = true;
            MicIcon.Text = "🔇";
            MicIcon.Opacity = 0.5;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToSettings();
    }

    private void BrowseRooms_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateTo("ServerBrowser");
    }

    private async void DisconnectVoice_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        await _viewModel.LeaveVoiceChannelCommand.ExecuteAsync(null);
    }

    private async void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;

        if (_voiceService.IsScreenSharing)
        {
            await _voiceService.StopScreenShareAsync();
            ScreenShareIcon.Text = "🖥";
            ScreenShareButton.Background = null;
            ScreenShareButton.ToolTip = "Share Screen";
        }
        else
        {
            await _voiceService.StartScreenShareAsync();
            ScreenShareIcon.Text = "🛑";
            ScreenShareButton.Background = (System.Windows.Media.Brush)FindResource("AccentGreenBrush");
            ScreenShareButton.ToolTip = "Stop Sharing";
        }
    }

    #region Voice User Context Menu Handlers

    private VoiceUserState? GetVoiceUserFromSender(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            // Navigate up to find the context menu
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
                return element.Tag as VoiceUserState ?? element.DataContext as VoiceUserState;
            }
        }
        return null;
    }

    private void VoiceViewProfile_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        _navigationService?.NavigateToProfile(user.UserId);
    }

    private void VoiceSendMessage_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        _navigationService?.NavigateToFriends();
    }

    private async void VoiceAddFriend_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        if (friendService != null)
        {
            try
            {
                await friendService.SendFriendRequestAsync(user.Username);
                MessageBox.Show($"Friend request sent to {user.Username}!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send friend request: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void VoiceMuteUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var isMuted = _voiceService.IsUserMuted(user.ConnectionId);
        _voiceService.SetUserMuted(user.ConnectionId, !isMuted);

        var action = isMuted ? "Unmuted" : "Muted";
        MessageBox.Show($"{action} {user.Username} for yourself", $"User {action}",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void VoiceAdjustVolume_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        // Show a simple input dialog for volume (0-200%)
        var currentVolume = _voiceService.GetUserVolume(user.ConnectionId) * 100;
        var input = InputDialog.Show(
            "Adjust User Volume",
            $"Enter volume percentage for {user.Username} (0-200):",
            currentVolume.ToString("F0"));

        if (!string.IsNullOrEmpty(input) && float.TryParse(input, out var volume))
        {
            volume = Math.Clamp(volume, 0, 200);
            _voiceService.SetUserVolume(user.ConnectionId, volume / 100f);
            MessageBox.Show($"Set {user.Username}'s volume to {volume}%", "Volume Adjusted",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void VoiceWatchScreen_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var viewer = new ScreenShareViewer(_voiceService, user.ConnectionId, user.Username);
        viewer.Show();
    }

    private async void MoveToChannel_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var menuItem = sender as MenuItem;
        var channelId = menuItem?.Tag?.ToString();

        if (!string.IsNullOrEmpty(channelId))
        {
            await _voiceService.MoveUserToChannelAsync(user.ConnectionId, channelId);
            var channelName = ChannelDisplayNames.TryGetValue(channelId, out var name) ? name : channelId;
            MessageBox.Show($"Moved {user.Username} to {channelName}", "User Moved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void VoiceDisconnectUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var result = MessageBox.Show(
            $"Disconnect {user.Username} from voice?",
            "Disconnect User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _voiceService.DisconnectUserAsync(user.ConnectionId);
        }
    }

    private async void VoiceKickUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var reason = InputDialog.Show(
            "Kick User",
            $"Enter reason for kicking {user.Username}:",
            "Violation of rules");

        if (!string.IsNullOrEmpty(reason))
        {
            var result = MessageBox.Show(
                $"Kick {user.Username} from the server?\nReason: {reason}",
                "Confirm Kick",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _voiceService.KickUserAsync(user.UserId, reason);
                MessageBox.Show($"Kicked {user.Username}", "User Kicked",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private async void VoiceBanUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        var reason = InputDialog.Show(
            "Ban User",
            $"Enter reason for banning {user.Username}:",
            "Violation of rules");

        if (!string.IsNullOrEmpty(reason))
        {
            var durationInput = InputDialog.Show(
                "Ban Duration",
                "Enter ban duration in minutes (leave empty for permanent):",
                "");

            TimeSpan? duration = null;
            if (!string.IsNullOrEmpty(durationInput) && double.TryParse(durationInput, out var minutes))
            {
                duration = TimeSpan.FromMinutes(minutes);
            }

            var durationText = duration.HasValue ? $" for {duration.Value.TotalMinutes} minutes" : " permanently";
            var result = MessageBox.Show(
                $"Ban {user.Username}{durationText}?\nReason: {reason}",
                "Confirm Ban",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _voiceService.BanUserAsync(user.UserId, reason, duration);
                MessageBox.Show($"Banned {user.Username}", "User Banned",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void VoiceCopyUserId_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        Clipboard.SetText(user.UserId);
        MessageBox.Show("User ID copied to clipboard", "Copied",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Channel Context Menu Handlers

    private void CopyChannelId_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
        {
            if (contextMenu.PlacementTarget is FrameworkElement element)
            {
                var channelName = element.Tag?.ToString() ??
                    (element.DataContext as ChannelDto)?.Id ?? "unknown";
                Clipboard.SetText(channelName);
            }
        }
    }

    #endregion
}
