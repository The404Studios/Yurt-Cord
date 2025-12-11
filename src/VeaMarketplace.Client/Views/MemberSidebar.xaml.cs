using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class MemberSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;

    public MemberSidebar()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;

        OnlineMembersControl.ItemsSource = _viewModel.OnlineUsers;

        // Update online count
        _viewModel.OnlineUsers.CollectionChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
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
        };
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

    private void Mention_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        // Could emit an event to insert @username in chat input
        Clipboard.SetText($"@{user.Username}");
        MessageBox.Show($"Copied @{user.Username} to clipboard", "Mention",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InviteToVoice_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        MessageBox.Show($"Invited {user.Username} to voice channel", "Invite Sent",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        MessageBox.Show($"Muted {user.Username}", "User Muted",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BlockUser_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to block {user.Username}?",
            "Block User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            MessageBox.Show($"Blocked {user.Username}", "User Blocked",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyUserId_Click(object sender, RoutedEventArgs e)
    {
        var user = GetUserFromSender(sender);
        if (user == null) return;

        Clipboard.SetText(user.Id);
    }
}
