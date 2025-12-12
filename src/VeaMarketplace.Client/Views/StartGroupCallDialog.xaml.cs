using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public class SelectableFriend : FriendDto
{
    public bool IsSelected { get; set; }
}

public partial class StartGroupCallDialog : Window
{
    private readonly IFriendService _friendService;
    private readonly IVoiceService _voiceService;
    private readonly ObservableCollection<SelectableFriend> _friends = [];
    private readonly List<SelectableFriend> _allFriends = [];

    public event Action<string, List<string>>? OnCallStarted;

    public StartGroupCallDialog(IFriendService friendService, IVoiceService voiceService)
    {
        InitializeComponent();

        _friendService = friendService;
        _voiceService = voiceService;

        FriendsListControl.ItemsSource = _friends;
        LoadFriends();
    }

    private async void LoadFriends()
    {
        try
        {
            var friends = await _friendService.GetFriendsAsync();
            _allFriends.Clear();
            _friends.Clear();

            foreach (var friend in friends)
            {
                var selectable = new SelectableFriend
                {
                    Id = friend.Id,
                    UserId = friend.UserId,
                    Username = friend.Username,
                    DisplayName = friend.DisplayName,
                    AvatarUrl = friend.AvatarUrl,
                    Bio = friend.Bio,
                    StatusMessage = friend.StatusMessage,
                    AccentColor = friend.AccentColor,
                    Role = friend.Role,
                    Rank = friend.Rank,
                    IsOnline = friend.IsOnline,
                    FriendsSince = friend.FriendsSince,
                    IsSelected = false
                };
                _allFriends.Add(selectable);
                _friends.Add(selectable);
            }

            // Sort online friends first
            SortFriends();
        }
        catch { }
    }

    private void SortFriends()
    {
        var sorted = _friends.OrderByDescending(f => f.IsOnline)
                             .ThenBy(f => f.Username)
                             .ToList();
        _friends.Clear();
        foreach (var friend in sorted)
        {
            _friends.Add(friend);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.ToLower() ?? "";
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
            ? Visibility.Visible
            : Visibility.Collapsed;

        _friends.Clear();
        var filtered = string.IsNullOrEmpty(query)
            ? _allFriends
            : _allFriends.Where(f => f.Username.ToLower().Contains(query) ||
                                     f.DisplayName.ToLower().Contains(query));

        foreach (var friend in filtered.OrderByDescending(f => f.IsOnline).ThenBy(f => f.Username))
        {
            _friends.Add(friend);
        }
    }

    private void Friend_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SelectableFriend friend)
        {
            friend.IsSelected = !friend.IsSelected;

            // Refresh the item in the list
            var index = _friends.IndexOf(friend);
            if (index >= 0)
            {
                _friends.RemoveAt(index);
                _friends.Insert(index, friend);
            }

            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        var count = _allFriends.Count(f => f.IsSelected);
        SelectedCountText.Text = count == 1
            ? "1 friend selected"
            : $"{count} friends selected";

        StartCallButton.IsEnabled = count > 0;
    }

    private async void StartCall_Click(object sender, RoutedEventArgs e)
    {
        var selectedIds = _allFriends
            .Where(f => f.IsSelected)
            .Select(f => f.UserId)
            .ToList();

        if (selectedIds.Count == 0)
        {
            MessageBox.Show("Please select at least one friend to call.",
                "No Friends Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var callName = CallNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(callName))
        {
            callName = "Group Call";
        }

        try
        {
            await _voiceService.StartGroupCallAsync(callName, selectedIds);
            OnCallStarted?.Invoke(callName, selectedIds);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start call: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // Static method to show dialog
    public static bool? ShowDialog(IFriendService friendService, IVoiceService voiceService, Window? owner = null)
    {
        var dialog = new StartGroupCallDialog(friendService, voiceService);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        return dialog.ShowDialog();
    }
}
