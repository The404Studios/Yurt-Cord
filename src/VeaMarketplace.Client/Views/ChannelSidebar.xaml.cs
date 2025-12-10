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

        // Show voice users when in a voice channel
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.IsInVoiceChannel))
            {
                Dispatcher.Invoke(() =>
                {
                    VoiceUsersPanel.Visibility = _viewModel.IsInVoiceChannel
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                });
            }
        };
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

    #region Voice User Context Menu Handlers

    private VoiceUserState? GetVoiceUserFromSender(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            // Navigate up to find the context menu
            var parent = menuItem.Parent;
            while (parent != null && parent is not ContextMenu)
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

        _navigationService?.NavigateToProfile();
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
        if (user == null) return;

        MessageBox.Show($"Muted {user.Username} for yourself", "User Muted",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void VoiceDeafenUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        MessageBox.Show($"Deafened {user.Username}", "User Deafened",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void VoiceAdjustVolume_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        // Could open a volume slider dialog
        MessageBox.Show($"Adjust volume for {user.Username}", "Volume",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MoveToChannel_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        var menuItem = sender as MenuItem;
        var channelId = menuItem?.Tag?.ToString();

        MessageBox.Show($"Moving {user.Username} to {channelId}", "Move User",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void VoiceDisconnectUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        var result = MessageBox.Show(
            $"Disconnect {user.Username} from voice?",
            "Disconnect User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            MessageBox.Show($"Disconnected {user.Username}", "User Disconnected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void VoiceCopyUserId_Click(object sender, RoutedEventArgs e)
    {
        var user = GetVoiceUserFromSender(sender);
        if (user == null) return;

        Clipboard.SetText(user.UserId);
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
