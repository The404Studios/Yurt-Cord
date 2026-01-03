using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Controls;

public partial class SocialActivityPanel : UserControl
{
    public enum ActivityType
    {
        FriendOnline,
        FriendOffline,
        GameStarted,
        StatusUpdate,
        Purchase,
        NewListing,
        Achievement,
        JoinedServer,
        StartedStreaming
    }

    public class ActivityItem
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string UserAvatarUrl { get; set; } = string.Empty;
        public ActivityType Type { get; set; }
        public string ActionText { get; set; } = string.Empty;
        public string? TargetName { get; set; }
        public string? TargetId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool HasTarget => !string.IsNullOrEmpty(TargetName);
        public bool HasAction { get; set; }
        public string ActionIcon { get; set; } = "→";

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - Timestamp;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                return $"{(int)span.TotalDays}d ago";
            }
        }

        public string ActivityIcon => Type switch
        {
            ActivityType.FriendOnline => "🟢",
            ActivityType.FriendOffline => "⚫",
            ActivityType.GameStarted => "🎮",
            ActivityType.StatusUpdate => "💬",
            ActivityType.Purchase => "🛒",
            ActivityType.NewListing => "📦",
            ActivityType.Achievement => "🏆",
            ActivityType.JoinedServer => "🏠",
            ActivityType.StartedStreaming => "📺",
            _ => "📌"
        };

        public SolidColorBrush ActivityColor => Type switch
        {
            ActivityType.FriendOnline => new SolidColorBrush(Color.FromRgb(67, 181, 129)),
            ActivityType.FriendOffline => new SolidColorBrush(Color.FromRgb(116, 127, 141)),
            ActivityType.GameStarted => new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            ActivityType.StatusUpdate => new SolidColorBrush(Color.FromRgb(250, 166, 26)),
            ActivityType.Purchase => new SolidColorBrush(Color.FromRgb(67, 181, 129)),
            ActivityType.NewListing => new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            ActivityType.Achievement => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            ActivityType.JoinedServer => new SolidColorBrush(Color.FromRgb(114, 137, 218)),
            ActivityType.StartedStreaming => new SolidColorBrush(Color.FromRgb(145, 71, 255)),
            _ => new SolidColorBrush(Color.FromRgb(79, 84, 92))
        };
    }

    public class OnlineFriend
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string? Status { get; set; }
    }

    public class SuggestedFriend
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public int MutualFriendsCount { get; set; }
    }

    private readonly ObservableCollection<ActivityItem> _activities = new();
    private readonly ObservableCollection<OnlineFriend> _onlineFriends = new();
    private readonly ObservableCollection<SuggestedFriend> _suggestedFriends = new();

    public event EventHandler? StartGroupCallRequested;
    public event EventHandler? CreateGroupChatRequested;
    public event EventHandler? ShareStatusRequested;
    public event EventHandler? FindFriendsRequested;
    public event EventHandler<ActivityItem>? ActivityClicked;
    public event EventHandler<ActivityItem>? ActivityActionClicked;
    public event EventHandler<OnlineFriend>? OnlineFriendClicked;
    public event EventHandler<SuggestedFriend>? AddFriendRequested;
    public event EventHandler? RefreshRequested;

    public SocialActivityPanel()
    {
        InitializeComponent();

        ActivityFeedControl.ItemsSource = _activities;
        SuggestedFriendsControl.ItemsSource = _suggestedFriends;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup dynamic controls
        OnlineFriendsPanel.Children.Clear();
        _activities.Clear();
        _onlineFriends.Clear();
        _suggestedFriends.Clear();
        Unloaded -= OnUnloaded;
    }

    public void SetOnlineFriends(IEnumerable<OnlineFriend> friends)
    {
        _onlineFriends.Clear();
        OnlineFriendsPanel.Children.Clear();

        foreach (var friend in friends.Take(10))
        {
            _onlineFriends.Add(friend);

            var avatar = CreateOnlineFriendAvatar(friend);
            OnlineFriendsPanel.Children.Add(avatar);
        }

        OnlineCountText.Text = _onlineFriends.Count.ToString();
    }

    private Border CreateOnlineFriendAvatar(OnlineFriend friend)
    {
        var container = new Border
        {
            Width = 44,
            Height = 44,
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(22),
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = friend,
            ToolTip = friend.Username
        };

        var grid = new Grid();

        var ellipse = new Ellipse
        {
            Width = 40,
            Height = 40,
            Fill = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(friend.AvatarUrl, UriKind.RelativeOrAbsolute)),
                Stretch = Stretch.UniformToFill
            }
        };

        var statusDot = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new SolidColorBrush(Color.FromRgb(67, 181, 129)),
            Stroke = new SolidColorBrush(Color.FromRgb(43, 45, 49)),
            StrokeThickness = 3,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Bottom
        };

        grid.Children.Add(ellipse);
        grid.Children.Add(statusDot);
        container.Child = grid;

        container.MouseLeftButtonUp += (s, e) =>
        {
            if (container.Tag is OnlineFriend f)
            {
                OnlineFriendClicked?.Invoke(this, f);
            }
        };

        container.MouseEnter += (s, e) =>
        {
            container.Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
        };

        container.MouseLeave += (s, e) =>
        {
            container.Background = Brushes.Transparent;
        };

        return container;
    }

    public void SetActivities(IEnumerable<ActivityItem> activities)
    {
        _activities.Clear();

        foreach (var activity in activities.OrderByDescending(a => a.Timestamp))
        {
            _activities.Add(activity);
        }

        EmptyState.Visibility = _activities.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void AddActivity(ActivityItem activity)
    {
        _activities.Insert(0, activity);
        EmptyState.Visibility = Visibility.Collapsed;

        // Keep only last 50 activities
        while (_activities.Count > 50)
        {
            _activities.RemoveAt(_activities.Count - 1);
        }
    }

    public void SetSuggestedFriends(IEnumerable<SuggestedFriend> friends)
    {
        _suggestedFriends.Clear();

        foreach (var friend in friends.Take(5))
        {
            _suggestedFriends.Add(friend);
        }

        var hasSuggestions = _suggestedFriends.Count > 0;
        SuggestedHeader.Visibility = hasSuggestions ? Visibility.Visible : Visibility.Collapsed;
        SuggestedFriendsControl.Visibility = hasSuggestions ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MoreOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var contextMenu = new ContextMenu();

        var refreshItem = new MenuItem { Header = "Refresh Activity" };
        refreshItem.Click += (s, args) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(refreshItem);

        var hideItem = new MenuItem { Header = "Hide This Panel" };
        hideItem.Click += (s, args) => this.Visibility = Visibility.Collapsed;
        contextMenu.Items.Add(hideItem);

        contextMenu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Activity Settings..." };
        settingsItem.Click += (s, args) =>
        {
            var navService = (INavigationService?)App.ServiceProvider?.GetService(typeof(INavigationService));
            navService?.NavigateToSettings();
        };
        contextMenu.Items.Add(settingsItem);

        contextMenu.PlacementTarget = button;
        contextMenu.IsOpen = true;
    }

    private void StartGroupCall_Click(object sender, RoutedEventArgs e)
    {
        StartGroupCallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CreateGroupChat_Click(object sender, RoutedEventArgs e)
    {
        CreateGroupChatRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShareStatus_Click(object sender, RoutedEventArgs e)
    {
        ShareStatusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FindFriends_Click(object sender, RoutedEventArgs e)
    {
        FindFriendsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Activity_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ActivityItem activity)
        {
            ActivityClicked?.Invoke(this, activity);
        }
    }

    private void ActivityAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActivityItem activity)
        {
            ActivityActionClicked?.Invoke(this, activity);
        }
    }

    private void AddSuggestedFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SuggestedFriend friend)
        {
            AddFriendRequested?.Invoke(this, friend);
            _suggestedFriends.Remove(friend);

            if (_suggestedFriends.Count == 0)
            {
                SuggestedHeader.Visibility = Visibility.Collapsed;
                SuggestedFriendsControl.Visibility = Visibility.Collapsed;
            }
        }
    }
}
