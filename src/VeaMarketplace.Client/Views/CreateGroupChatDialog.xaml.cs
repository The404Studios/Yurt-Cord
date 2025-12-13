using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class CreateGroupChatDialog : Window
{
    private readonly IFriendService? _friendService;
    private readonly IChatService? _chatService;
    private readonly ObservableCollection<SelectableFriend> _friends = [];
    private readonly ObservableCollection<SelectableFriend> _selectedFriends = [];
    private string? _groupIconPath;

    public string? CreatedGroupId { get; private set; }

    public CreateGroupChatDialog()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _friendService = App.ServiceProvider.GetService(typeof(IFriendService)) as IFriendService;
        _chatService = App.ServiceProvider.GetService(typeof(IChatService)) as IChatService;

        SelectedFriendsControl.ItemsSource = _selectedFriends;
        FriendsListControl.ItemsSource = _friends;

        _ = LoadFriendsAsync();
    }

    private async Task LoadFriendsAsync()
    {
        if (_friendService == null) return;

        try
        {
            var friends = await _friendService.GetFriendsAsync();
            foreach (var friend in friends.OrderBy(f => f.DisplayName ?? f.Username))
            {
                _friends.Add(new SelectableFriend
                {
                    Id = friend.Id,
                    Username = friend.Username,
                    DisplayName = friend.DisplayName ?? friend.Username,
                    AvatarUrl = friend.AvatarUrl ?? "/Assets/default-avatar.png",
                    Status = friend.Status,
                    IsSelected = false
                });
            }
        }
        catch
        {
            // Handle error
        }
    }

    private void GroupIcon_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Title = "Select Group Icon"
        };

        if (dialog.ShowDialog() == true)
        {
            _groupIconPath = dialog.FileName;
            try
            {
                var bitmap = new BitmapImage(new Uri(_groupIconPath));
                GroupIconImage.Source = bitmap;
                GroupIconImage.Visibility = Visibility.Visible;
                GroupIconInitials.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // Keep default icon
            }
        }
    }

    private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        NameCharCount.Text = $"{GroupNameTextBox.Text.Length}/100";
        UpdateGroupInitials();
        UpdateCreateButtonState();
    }

    private void UpdateGroupInitials()
    {
        if (GroupIconImage.Visibility == Visibility.Visible) return;

        var name = GroupNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            GroupIconInitials.Text = "GC";
            return;
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = words.Length >= 2
            ? $"{words[0][0]}{words[1][0]}"
            : name.Length >= 2 ? name[..2] : name;

        GroupIconInitials.Text = initials.ToUpperInvariant();
    }

    private void FriendSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = FriendSearchBox.Text.ToLowerInvariant();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(searchText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var friend in _friends)
        {
            // Filter friends based on search
        }

        // For simplicity, we'll just filter the view
        if (string.IsNullOrEmpty(searchText))
        {
            FriendsListControl.ItemsSource = _friends;
        }
        else
        {
            FriendsListControl.ItemsSource = _friends
                .Where(f => f.DisplayName.ToLowerInvariant().Contains(searchText) ||
                           f.Username.ToLowerInvariant().Contains(searchText))
                .ToList();
        }
    }

    private void FriendItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SelectableFriend friend)
        {
            friend.IsSelected = !friend.IsSelected;

            if (friend.IsSelected)
            {
                if (!_selectedFriends.Any(f => f.Id == friend.Id))
                {
                    _selectedFriends.Add(friend);
                }
            }
            else
            {
                var existing = _selectedFriends.FirstOrDefault(f => f.Id == friend.Id);
                if (existing != null)
                {
                    _selectedFriends.Remove(existing);
                }
            }

            // Refresh the list view
            var currentSource = FriendsListControl.ItemsSource;
            FriendsListControl.ItemsSource = null;
            FriendsListControl.ItemsSource = currentSource;

            UpdateCreateButtonState();
        }
    }

    private void RemoveSelectedFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string friendId)
        {
            var friend = _friends.FirstOrDefault(f => f.Id == friendId);
            if (friend != null)
            {
                friend.IsSelected = false;
            }

            var selected = _selectedFriends.FirstOrDefault(f => f.Id == friendId);
            if (selected != null)
            {
                _selectedFriends.Remove(selected);
            }

            // Refresh the list view
            var currentSource = FriendsListControl.ItemsSource;
            FriendsListControl.ItemsSource = null;
            FriendsListControl.ItemsSource = currentSource;

            UpdateCreateButtonState();
        }
    }

    private void UpdateCreateButtonState()
    {
        var hasName = !string.IsNullOrWhiteSpace(GroupNameTextBox.Text);
        var hasMembers = _selectedFriends.Count >= 1; // At least 1 other person for a group

        CreateButton.IsEnabled = hasName && hasMembers;
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_chatService == null) return;

        CreateButton.IsEnabled = false;
        CreateButton.Content = "Creating...";

        try
        {
            var memberIds = _selectedFriends.Select(f => f.Id).ToList();
            var groupName = GroupNameTextBox.Text.Trim();

            var groupId = await _chatService.CreateGroupChatAsync(groupName, memberIds, _groupIconPath);

            CreatedGroupId = groupId;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create group: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            CreateButton.IsEnabled = true;
            CreateButton.Content = "Create Group";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class SelectableFriend
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public bool IsSelected { get; set; }
}
