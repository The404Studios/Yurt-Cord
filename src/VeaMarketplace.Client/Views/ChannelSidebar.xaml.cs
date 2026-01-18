using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ChannelSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IApiService? _apiService;
    private readonly IChatService? _chatService;
    private readonly IVoiceService? _voiceService;
    private readonly INavigationService? _navigationService;
    private readonly IToastNotificationService? _toastService;
    private bool _isMuted;
    private bool _isDeafened;
    private System.Timers.Timer? _latencyTimer;

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
        _chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;

        // Setup connection status monitoring
        SetupConnectionStatusMonitoring();

        ChannelsItemsControl.ItemsSource = _viewModel.Channels;
        VoiceUsersItemsControl.ItemsSource = _viewModel.VoiceUsers;

        // Update user panel when logged in
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Show/hide voice connected panel and users when in a voice channel
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Subscribe to audio level updates
        if (_voiceService != null)
        {
            _voiceService.OnLocalAudioLevel += OnLocalAudioLevel;
            _voiceService.OnUserDisconnectedByAdmin += OnUserDisconnectedByAdmin;
            _voiceService.OnUserMovedToChannel += OnUserMovedToChannel;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateUserPanel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from ViewModel events
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Unsubscribe from VoiceService events
        if (_voiceService != null)
        {
            _voiceService.OnLocalAudioLevel -= OnLocalAudioLevel;
            _voiceService.OnUserDisconnectedByAdmin -= OnUserDisconnectedByAdmin;
            _voiceService.OnUserMovedToChannel -= OnUserMovedToChannel;
        }

        // Unsubscribe from ChatService events
        if (_chatService != null)
        {
            _chatService.OnConnectionHandshake -= OnConnectionHandshake;
            _chatService.OnAuthenticated -= OnAuthenticated;
            _chatService.OnAuthenticationFailed -= OnAuthenticationFailed;
        }

        // Stop and dispose latency timer
        if (_latencyTimer != null)
        {
            _latencyTimer.Stop();
            _latencyTimer.Elapsed -= CheckLatencyHandler;
            _latencyTimer.Dispose();
            _latencyTimer = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.IsInVoiceChannel) ||
            e.PropertyName == nameof(ChatViewModel.CurrentVoiceChannel))
        {
            Dispatcher.Invoke(() =>
            {
                var isInVoice = _viewModel!.IsInVoiceChannel;
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
    }

    private void OnLocalAudioLevel(double level)
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
    }

    private void OnUserDisconnectedByAdmin(string reason)
    {
        Dispatcher.Invoke(() =>
        {
            _toastService?.ShowWarning("Disconnected", reason);
        });
    }

    private void OnUserMovedToChannel(string channelId, string movedBy)
    {
        Dispatcher.Invoke(() =>
        {
            var channelName = ChannelDisplayNames.TryGetValue(channelId, out var name) ? name : channelId;
            VoiceChannelNameText.Text = channelName;
            _toastService?.ShowInfo("Moved", $"You were moved to {channelName}");
        });
    }

    private void UpdateUserPanel()
    {
        var user = _apiService?.CurrentUser;
        if (user != null)
        {
            UserNameText.Text = user.Username;
            SetUserAvatar(user.AvatarUrl);
        }
        else
        {
            UserNameText.Text = "Guest";
            LoadDefaultAvatar();
        }
    }

    /// <summary>
    /// Sets the user avatar with proper handling for special formats.
    /// </summary>
    private void SetUserAvatar(string? avatarUrl)
    {
        // Check if it's a special format (emoji gradient) or empty - use default
        if (string.IsNullOrWhiteSpace(avatarUrl) ||
            avatarUrl.StartsWith("emoji:") ||
            avatarUrl.StartsWith("gradient:"))
        {
            LoadDefaultAvatar();
            return;
        }

        // Try to load the URL as an image
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(avatarUrl);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            UserAvatarBrush.ImageSource = bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user avatar: {ex.Message}");
            LoadDefaultAvatar();
        }
    }

    private void LoadDefaultAvatar()
    {
        try
        {
            UserAvatarBrush.ImageSource = new BitmapImage(new Uri(AppConstants.DefaultAvatarPath));
        }
        catch
        {
            // If default avatar fails, leave it empty
        }
    }

    private async void ChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        try
        {
            var button = (Button)sender;
            var channelName = button.Tag?.ToString();
            if (!string.IsNullOrEmpty(channelName))
            {
                await _viewModel.SwitchChannelCommand.ExecuteAsync(channelName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error switching channel: {ex.Message}");
            _toastService?.ShowError("Channel Error", "Failed to switch channel");
        }
    }

    private async void VoiceChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        try
        {
            var button = (Button)sender;
            var channelId = button.Tag?.ToString();
            if (!string.IsNullOrEmpty(channelId))
            {
                await _viewModel.JoinVoiceChannelCommand.ExecuteAsync(channelId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error joining voice channel: {ex.Message}");
            _toastService?.ShowError("Voice Error", "Failed to join voice channel");
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
        try
        {
            await _viewModel.LeaveVoiceChannelCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disconnecting from voice: {ex.Message}");
            _toastService?.ShowError("Voice Error", "Failed to disconnect from voice channel");
        }
    }

    private async void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;

        try
        {
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
        catch (Exception ex)
        {
            _toastService?.ShowError("Screen Share", $"Failed: {ex.Message}");
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
                _toastService?.ShowSuccess("Friend Request Sent", $"Sent to {user.Username}");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Request Failed", ex.Message);
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
        _toastService?.ShowInfo($"User {action}", $"{action} {user.Username} for yourself");
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
            _toastService?.ShowInfo("Volume Adjusted", $"{user.Username}'s volume set to {volume}%");
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

        try
        {
            var menuItem = sender as MenuItem;
            var channelId = menuItem?.Tag?.ToString();

            if (!string.IsNullOrEmpty(channelId))
            {
                await _voiceService.MoveUserToChannelAsync(user.ConnectionId, channelId);
                var channelName = ChannelDisplayNames.TryGetValue(channelId, out var name) ? name : channelId;
                _toastService?.ShowInfo("User Moved", $"Moved {user.Username} to {channelName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error moving user to channel: {ex.Message}");
            _toastService?.ShowError("Move Failed", $"Failed to move {user.Username}");
        }
    }

    private async void VoiceDisconnectUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        try
        {
            var result = MessageBox.Show(
                $"Disconnect {user.Username} from voice?",
                "Disconnect User",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _voiceService.DisconnectUserAsync(user.ConnectionId);
                _toastService?.ShowInfo("User Disconnected", $"Disconnected {user.Username} from voice");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disconnecting user: {ex.Message}");
            _toastService?.ShowError("Disconnect Failed", $"Failed to disconnect {user.Username}");
        }
    }

    private async void VoiceKickUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        try
        {
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
                    _toastService?.ShowSuccess("User Kicked", $"Kicked {user.Username}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error kicking user: {ex.Message}");
            _toastService?.ShowError("Kick Failed", $"Failed to kick {user.Username}");
        }
    }

    private async void VoiceBanUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        try
        {
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
                    _toastService?.ShowSuccess("User Banned", $"Banned {user.Username}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error banning user: {ex.Message}");
            _toastService?.ShowError("Ban Failed", $"Failed to ban {user.Username}");
        }
    }

    private void VoiceCopyUserId_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        Clipboard.SetText(user.UserId);
        _toastService?.ShowInfo("Copied", "User ID copied to clipboard");
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
                _toastService?.ShowInfo("Copied", "Channel ID copied to clipboard");
            }
        }
    }

    #endregion

    #region Connection Status

    private void SetupConnectionStatusMonitoring()
    {
        if (_chatService == null) return;

        // Subscribe to connection handshake
        _chatService.OnConnectionHandshake += OnConnectionHandshake;

        // Subscribe to authentication events
        _chatService.OnAuthenticated += OnAuthenticated;
        _chatService.OnAuthenticationFailed += OnAuthenticationFailed;

        // Start latency monitoring timer
        _latencyTimer = new System.Timers.Timer(10000); // Check every 10 seconds
        _latencyTimer.Elapsed += CheckLatencyHandler;
        _latencyTimer.AutoReset = true;
        _latencyTimer.Start();
    }

    private void OnConnectionHandshake()
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine("ChannelSidebar: Connection handshake received");
            UpdateConnectionStatus(ConnectionState.Connected);
        });
    }

    private void OnAuthenticated(Shared.DTOs.UserDto? user)
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"ChannelSidebar: Authenticated as {user?.Username}");
            UpdateConnectionStatus(ConnectionState.Connected);
            ConnectionDetailText.Text = $"Session: {_chatService?.SessionId?[..8] ?? "active"}";
        });
    }

    private void OnAuthenticationFailed(string error)
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"ChannelSidebar: Authentication failed: {error}");
            UpdateConnectionStatus(ConnectionState.Disconnected);
        });
    }

    private void CheckLatencyHandler(object? sender, System.Timers.ElapsedEventArgs e)
    {
        CheckLatency();
    }

    private void CheckLatency()
    {
        if (_chatService == null) return;

        Dispatcher.Invoke(() =>
        {
            if (_chatService.IsConnected)
            {
                UpdateConnectionStatus(ConnectionState.Connected);
            }
            else
            {
                UpdateConnectionStatus(ConnectionState.Reconnecting);
            }
        });
    }

    private enum ConnectionState
    {
        Connected,
        Reconnecting,
        Disconnected
    }

    private void UpdateConnectionStatus(ConnectionState state)
    {
        // Hide all banners first
        ConnectionStatusBar.Visibility = Visibility.Collapsed;
        ReconnectingBanner.Visibility = Visibility.Collapsed;
        DisconnectedBanner.Visibility = Visibility.Collapsed;

        switch (state)
        {
            case ConnectionState.Connected:
                ConnectionStatusBar.Visibility = Visibility.Visible;
                ConnectionStatusText.Text = "Connected";
                StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(87, 242, 135)); // Green
                ConnectionStatusBar.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(45, 125, 70));
                break;

            case ConnectionState.Reconnecting:
                ReconnectingBanner.Visibility = Visibility.Visible;
                break;

            case ConnectionState.Disconnected:
                DisconnectedBanner.Visibility = Visibility.Visible;
                break;
        }
    }

    private async void DisconnectedBanner_Click(object sender, MouseButtonEventArgs e)
    {
        if (_chatService == null || _apiService?.AuthToken == null) return;

        UpdateConnectionStatus(ConnectionState.Reconnecting);

        try
        {
            await _chatService.ConnectAsync(_apiService.AuthToken);
            UpdateConnectionStatus(ConnectionState.Connected);
            _toastService?.ShowSuccess("Reconnected", "Connection restored");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Reconnection failed: {ex.Message}");
            UpdateConnectionStatus(ConnectionState.Disconnected);
            _toastService?.ShowError("Connection Failed", "Unable to reconnect. Please try again.");
        }
    }

    #endregion

    #region Channel Creation

    private async void CreateTextChannel_Click(object sender, RoutedEventArgs e)
    {
        var channelName = InputDialog.Show(
            "Create Text Channel",
            "Enter channel name:",
            "new-channel");

        if (string.IsNullOrWhiteSpace(channelName))
            return;

        // Sanitize channel name (lowercase, replace spaces with dashes)
        channelName = channelName.ToLower().Replace(" ", "-");

        try
        {
            // Add channel to the ViewModel's channels list
            if (_viewModel != null)
            {
                var newChannel = new ChannelDto
                {
                    Name = channelName,
                    Icon = "#",
                    Description = $"Text channel: {channelName}"
                };

                // Add to channels collection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _viewModel.Channels.Add(newChannel);
                });

                _toastService?.ShowSuccess("Channel Created", $"Created #{channelName}");
            }
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Error", $"Failed to create channel: {ex.Message}");
        }
    }

    private async void CreateVoiceChannel_Click(object sender, RoutedEventArgs e)
    {
        var channelName = InputDialog.Show(
            "Create Voice Channel",
            "Enter voice channel name:",
            "Voice Chat");

        if (string.IsNullOrWhiteSpace(channelName))
            return;

        try
        {
            // Add voice channel to available channels
            if (_viewModel != null)
            {
                var channelId = channelName.ToLower().Replace(" ", "-") + "-voice";

                // Add to the display mapping
                if (!ChannelDisplayNames.ContainsKey(channelId))
                {
                    ChannelDisplayNames[channelId] = channelName;
                }

                _toastService?.ShowSuccess("Voice Channel Created", $"Created {channelName}");
            }
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Error", $"Failed to create voice channel: {ex.Message}");
        }
    }

    #endregion
}
