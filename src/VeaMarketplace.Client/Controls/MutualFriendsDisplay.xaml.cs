using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Controls;

public partial class MutualFriendsDisplay : UserControl
{
    private readonly INavigationService? _navigationService;
    private readonly IFriendService? _friendService;
    private readonly ObservableCollection<MutualFriendDisplay> _friends = [];
    private bool _isExpanded;
    private string? _userId;

    public static readonly DependencyProperty MaxVisibleAvatarsProperty =
        DependencyProperty.Register(nameof(MaxVisibleAvatars), typeof(int),
            typeof(MutualFriendsDisplay), new PropertyMetadata(5));

    public int MaxVisibleAvatars
    {
        get => (int)GetValue(MaxVisibleAvatarsProperty);
        set => SetValue(MaxVisibleAvatarsProperty, value);
    }

    public event EventHandler<string>? FriendSelected;
    public event EventHandler<string>? MessageRequested;

    public MutualFriendsDisplay()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _navigationService = App.ServiceProvider.GetService(typeof(INavigationService)) as INavigationService;
        _friendService = App.ServiceProvider.GetService(typeof(IFriendService)) as IFriendService;

        FriendsListControl.ItemsSource = _friends;
    }

    public async Task LoadMutualFriendsAsync(string userId)
    {
        _userId = userId;

        if (_friendService == null) return;

        try
        {
            var mutualFriends = await _friendService.GetMutualFriendsAsync(userId);

            if (!mutualFriends.Any())
            {
                RootPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _friends.Clear();
            foreach (var friend in mutualFriends)
            {
                var status = friend.IsOnline ? UserStatus.Online : UserStatus.Offline;
                _friends.Add(new MutualFriendDisplay
                {
                    Id = friend.Id,
                    Username = friend.Username,
                    DisplayName = string.IsNullOrEmpty(friend.DisplayName) ? friend.Username : friend.DisplayName,
                    AvatarUrl = string.IsNullOrEmpty(friend.AvatarUrl) ? "/Assets/default-avatar.png" : friend.AvatarUrl,
                    Status = status,
                    StatusColor = GetStatusBrush(status),
                    ShowUsername = !string.IsNullOrEmpty(friend.DisplayName) && friend.DisplayName != friend.Username
                });
            }

            CountText.Text = mutualFriends.Count.ToString();
            RootPanel.Visibility = Visibility.Visible;

            // Build avatar stack
            BuildAvatarStack(mutualFriends);

            // Show "View All" if more than MaxVisibleAvatars
            ViewAllButton.Visibility = mutualFriends.Count > MaxVisibleAvatars
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch
        {
            RootPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildAvatarStack(IEnumerable<UserDto> friends)
    {
        AvatarStack.Children.Clear();
        var friendsList = friends.ToList();

        for (int i = 0; i < Math.Min(friendsList.Count, MaxVisibleAvatars); i++)
        {
            var friend = friendsList[i];
            var avatar = CreateAvatarElement(friend, i);
            AvatarStack.Children.Add(avatar);
        }

        // Add overflow indicator
        if (friendsList.Count > MaxVisibleAvatars)
        {
            var overflow = CreateOverflowIndicator(friendsList.Count - MaxVisibleAvatars);
            AvatarStack.Children.Add(overflow);
        }
    }

    private Border CreateAvatarElement(UserDto friend, int index)
    {
        var border = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(2),
            BorderBrush = FindResource("PrimaryDarkBrush") as Brush,
            Margin = new Thickness(index > 0 ? -10 : 0, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = friend.DisplayName ?? friend.Username,
            Tag = friend.Id
        };

        border.MouseLeftButtonDown += (s, e) =>
        {
            if (s is Border b && b.Tag is string id)
            {
                FriendSelected?.Invoke(this, id);
            }
        };

        var ellipse = new Ellipse
        {
            Width = 28,
            Height = 28
        };

        if (!string.IsNullOrEmpty(friend.AvatarUrl))
        {
            try
            {
                ellipse.Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(friend.AvatarUrl)),
                    Stretch = Stretch.UniformToFill
                };
            }
            catch
            {
                ellipse.Fill = FindResource("AccentBrush") as Brush;
            }
        }
        else
        {
            ellipse.Fill = FindResource("AccentBrush") as Brush;
        }

        border.Child = ellipse;
        return border;
    }

    private Border CreateOverflowIndicator(int count)
    {
        var border = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = FindResource("TertiaryDarkBrush") as Brush,
            BorderThickness = new Thickness(2),
            BorderBrush = FindResource("PrimaryDarkBrush") as Brush,
            Margin = new Thickness(-10, 0, 0, 0),
            Cursor = Cursors.Hand
        };

        border.MouseLeftButtonDown += (s, e) => ToggleExpanded();

        border.Child = new TextBlock
        {
            Text = $"+{count}",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = FindResource("TextPrimaryBrush") as Brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return border;
    }

    private Brush GetStatusBrush(UserStatus status)
    {
        return status switch
        {
            UserStatus.Online => FindResource("AccentGreenBrush") as Brush ?? Brushes.Green,
            UserStatus.Idle => FindResource("AccentYellowBrush") as Brush ?? Brushes.Yellow,
            UserStatus.DoNotDisturb => FindResource("AccentRedBrush") as Brush ?? Brushes.Red,
            _ => FindResource("TextMutedBrush") as Brush ?? Brushes.Gray
        };
    }

    private void ViewAllButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleExpanded();
    }

    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
        ExpandedList.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ViewAllButton.Content = _isExpanded ? "Show Less" : "View All";
    }

    private void FriendItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is MutualFriendDisplay friend)
        {
            FriendSelected?.Invoke(this, friend.Id);
        }
    }

    private void MessageFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string friendId)
        {
            MessageRequested?.Invoke(this, friendId);
            _navigationService?.NavigateTo($"DirectMessage:{friendId}");
        }
    }
}

public class MutualFriendDisplay
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public Brush StatusColor { get; set; } = Brushes.Gray;
    public bool ShowUsername { get; set; }
}
