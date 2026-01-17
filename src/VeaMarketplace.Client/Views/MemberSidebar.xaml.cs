using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using OnlineUserDto = VeaMarketplace.Shared.DTOs.OnlineUserDto;

namespace VeaMarketplace.Client.Views;

public partial class MemberSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IVoiceService? _voiceService;
    private readonly IFriendService? _friendService;
    private readonly IToastNotificationService? _toastService;
    private OnlineUserDto? _selectedUser;

    public MemberSidebar()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _friendService = (IFriendService)App.ServiceProvider.GetService(typeof(IFriendService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;

        OnlineMembersControl.ItemsSource = _viewModel.OnlineUsers;

        // Update online count
        _viewModel.OnlineUsers.CollectionChanged += OnOnlineUsersChanged;

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.OnlineUsers.CollectionChanged -= OnOnlineUsersChanged;
        }
    }

    private void OnOnlineUsersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_viewModel == null) return;

            OnlineHeaderText.Text = $"ONLINE — {_viewModel.OnlineUsers.Count}";

            // Separate staff members
            var staff = _viewModel.OnlineUsers.Where(u => u.Role >= UserRole.Moderator).ToList();
            if (staff.Count > 0)
            {
                StaffHeader.Visibility = Visibility.Visible;
                StaffMembersControl.Visibility = Visibility.Visible;
                StaffMembersControl.ItemsSource = staff;
            }
            else
            {
                StaffHeader.Visibility = Visibility.Collapsed;
                StaffMembersControl.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void StartGroupCall_Click(object sender, RoutedEventArgs e)
    {
        if (_friendService == null || _voiceService == null) return;

        var dialog = new StartGroupCallDialog(_friendService, _voiceService);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private void Member_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is OnlineUserDto user)
        {
            ShowProfilePopup(user, border);
        }
    }

    private void ShowProfilePopup(OnlineUserDto user, FrameworkElement target)
    {
        _selectedUser = user;

        // Set popup content
        PopupUsernameText.Text = user.Username;
        PopupStatusText.Text = string.IsNullOrEmpty(user.StatusMessage) ? "Online" : user.StatusMessage;

        // Set avatar
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try
            {
                PopupAvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MemberSidebar: Failed to load popup avatar: {ex.Message}");
            }
        }

        // Set role badge
        if (user.Role >= UserRole.Admin)
        {
            PopupRoleBadge.Visibility = Visibility.Visible;
            PopupRoleText.Text = user.Role.ToString().ToUpper();
            PopupRoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ED4245")!);
        }
        else if (user.Role >= UserRole.Moderator)
        {
            PopupRoleBadge.Visibility = Visibility.Visible;
            PopupRoleText.Text = "MOD";
            PopupRoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAA61A")!);
        }
        else
        {
            PopupRoleBadge.Visibility = Visibility.Collapsed;
        }

        // Set bio if available
        if (!string.IsNullOrEmpty(user.Bio))
        {
            PopupBioSection.Visibility = Visibility.Visible;
            PopupBioText.Text = user.Bio;
        }
        else
        {
            PopupBioSection.Visibility = Visibility.Collapsed;
        }

        // Member since (mock date for now)
        PopupMemberSinceText.Text = "January 2024";

        // Set banner color based on accent
        if (!string.IsNullOrEmpty(user.AccentColor))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(user.AccentColor)!;
                BannerGradient1.Color = color;
                BannerGradient2.Color = Color.FromArgb(color.A,
                    (byte)Math.Min(255, color.R + 40),
                    (byte)Math.Min(255, color.G + 40),
                    (byte)Math.Min(255, color.B + 40));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse accent color '{user.AccentColor}': {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to parse accent color: {ex.Message}");
            }
        }

        // Show popup
        ProfilePopup.PlacementTarget = target;
        ProfilePopup.IsOpen = true;
    }

    private void PopupMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        ProfilePopup.IsOpen = false;

        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToFriends();
    }

    private async void PopupAddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null || _friendService == null) return;

        try
        {
            await _friendService.SendFriendRequestAsync(_selectedUser.Username);
            _toastService?.ShowSuccess("Friend Request Sent", $"Sent to {_selectedUser.Username}");
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Request Failed", ex.Message);
        }

        ProfilePopup.IsOpen = false;
    }

    private async void PopupNudge_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null || _voiceService == null) return;

        try
        {
            await _voiceService.SendNudgeAsync(_selectedUser.Id);
            _toastService?.ShowInfo("Nudge Sent", $"Nudged {_selectedUser.Username}!");
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Nudge Failed", ex.Message);
        }

        ProfilePopup.IsOpen = false;
    }

    private OnlineUserDto? GetUserFromSender(object sender)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
        {
            if (contextMenu.PlacementTarget is FrameworkElement element)
            {
                return element.Tag as OnlineUserDto ?? element.DataContext as OnlineUserDto;
            }
        }
        return null;
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToProfile(user.Id);
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToFriends();
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
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

    private void Mention_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        // Could emit an event to insert @username in chat input
        Clipboard.SetText($"@{user.Username}");
        _toastService?.ShowInfo("Copied", $"@{user.Username} copied to clipboard");
    }

    private async void InviteToVoice_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        try
        {
            // Get current voice channel
            var currentChannel = _voiceService.CurrentChannelId;
            if (string.IsNullOrEmpty(currentChannel))
            {
                _toastService?.ShowWarning("Not in Voice", "You must be in a voice channel to invite someone");
                return;
            }

            // Invite user to current voice channel
            await _voiceService.InviteToChannelAsync(user.Id, currentChannel);
            _toastService?.ShowSuccess("Invite Sent", $"Invited {user.Username} to voice channel");
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Invite Failed", $"Failed to invite user: {ex.Message}");
        }
    }

    private async void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null || _voiceService == null) return;

        try
        {
            // Mute user in current voice channel
            var currentChannel = _voiceService.CurrentChannelId;
            if (string.IsNullOrEmpty(currentChannel))
            {
                _toastService?.ShowWarning("Not in Voice", "You must be in a voice channel to mute someone");
                return;
            }

            await _voiceService.MuteUserAsync(user.Id, currentChannel);
            _toastService?.ShowSuccess("User Muted", $"Muted {user.Username}");
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Mute Failed", $"Failed to mute user: {ex.Message}");
        }
    }

    private async void BlockUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null || _friendService == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to block {user.Username}?\n\nBlocked users cannot send you messages or friend requests.",
            "Block User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _friendService.BlockUserAsync(user.Id);
                _toastService?.ShowSuccess("User Blocked", $"Blocked {user.Username}");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Block Failed", $"Failed to block user: {ex.Message}");
            }
        }
    }

    private void CopyUserId_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        Clipboard.SetText(user.Id);
        _toastService?.ShowInfo("Copied", "User ID copied to clipboard");
    }
}
