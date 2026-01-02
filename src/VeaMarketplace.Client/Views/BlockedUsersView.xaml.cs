using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class BlockedUsersView : UserControl
{
    private readonly IFriendService? _friendService;
    private readonly INavigationService? _navigationService;
    private readonly ObservableCollection<BlockedUserDisplay> _blockedUsers = [];

    public BlockedUsersView()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _friendService = App.ServiceProvider.GetService(typeof(IFriendService)) as IFriendService;
        _navigationService = App.ServiceProvider.GetService(typeof(INavigationService)) as INavigationService;

        BlockedUsersControl.ItemsSource = _blockedUsers;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Subscribe to blocked user changes
        if (_friendService != null)
        {
            _friendService.OnUserBlocked += OnUserBlocked;
            _friendService.OnUserUnblocked += OnUserUnblocked;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        if (_friendService != null)
        {
            _friendService.OnUserBlocked -= OnUserBlocked;
            _friendService.OnUserUnblocked -= OnUserUnblocked;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = LoadBlockedUsersAsync();
    }

    private async Task LoadBlockedUsersAsync()
    {
        if (_friendService == null) return;

        try
        {
            await _friendService.GetBlockedUsersAsync();

            // The blocked users are populated in the service's BlockedUsers collection
            _blockedUsers.Clear();
            foreach (var user in _friendService.BlockedUsers)
            {
                _blockedUsers.Add(new BlockedUserDisplay
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    BlockedAt = user.BlockedAt,
                    Reason = user.Reason,
                    HasReason = !string.IsNullOrEmpty(user.Reason)
                });
            }

            UpdateDisplay();
        }
        catch
        {
            // Handle error
        }
    }

    private void OnUserBlocked(BlockedUserDto user)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_blockedUsers.Any(u => u.UserId == user.UserId))
            {
                _blockedUsers.Add(new BlockedUserDisplay
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    BlockedAt = user.BlockedAt,
                    Reason = user.Reason,
                    HasReason = !string.IsNullOrEmpty(user.Reason)
                });
                UpdateDisplay();
            }
        });
    }

    private void OnUserUnblocked(string oderId)
    {
        Dispatcher.Invoke(() =>
        {
            var user = _blockedUsers.FirstOrDefault(u => u.UserId == oderId);
            if (user != null)
            {
                _blockedUsers.Remove(user);
                UpdateDisplay();
            }
        });
    }

    private void UpdateDisplay()
    {
        var count = _blockedUsers.Count;
        BlockedCountText.Text = count == 1 ? "1 blocked user" : $"{count} blocked users";
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BlockedListScroll.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void UnblockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string oderId && _friendService != null)
        {
            var result = MessageBox.Show(
                "Are you sure you want to unblock this user? They will be able to send you messages and friend requests again.",
                "Unblock User",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _friendService.UnblockUserAsync(oderId);
                }
                catch
                {
                    MessageBox.Show("Failed to unblock user. Please try again.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateTo("Settings");
    }
}

public class BlockedUserDisplay
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
    public string? Reason { get; set; }
    public bool HasReason { get; set; }
}
