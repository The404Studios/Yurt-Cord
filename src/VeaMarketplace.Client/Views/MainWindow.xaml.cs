using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly IToastNotificationService _toastService;
    private readonly IFriendService _friendService;
    private readonly IVoiceService _voiceService;
    private string _currentView = "Chat";

    public MainWindow()
    {
        InitializeComponent();

        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;
        _friendService = (IFriendService)App.ServiceProvider.GetService(typeof(IFriendService))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;

        // Set up toast notification container
        _toastService.SetContainer(ToastContainer);

        // Subscribe to friend service events for notifications
        _friendService.OnNewFriendRequest += request =>
        {
            _toastService.ShowFriendRequest(request.RequesterUsername);
        };

        _friendService.OnDirectMessageReceived += message =>
        {
            _toastService.ShowMessage(message.SenderUsername,
                message.Content.Length > 50 ? message.Content[..50] + "..." : message.Content);
        };

        // Subscribe to nudge notifications
        _voiceService.OnNudgeReceived += nudge =>
        {
            Dispatcher.Invoke(() =>
            {
                NudgeNotification.Show(nudge);
            });
        };

        // Subscribe to login success
        LoginView.OnLoginSuccess += () =>
        {
            Dispatcher.Invoke(() =>
            {
                // Animate transition
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s, e) =>
                {
                    LoginView.Visibility = Visibility.Collapsed;
                    MainAppGrid.Visibility = Visibility.Visible;

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                    MainAppGrid.BeginAnimation(OpacityProperty, fadeIn);

                    // Show welcome toast
                    _toastService.ShowSuccess("Welcome!", "You have successfully logged in.");
                };
                LoginView.BeginAnimation(OpacityProperty, fadeOut);
            });
        };

        // Subscribe to registration success - show profile setup
        LoginView.OnRegistrationSuccess += (username) =>
        {
            Dispatcher.Invoke(() =>
            {
                // Animate transition to profile setup
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s, e) =>
                {
                    LoginView.Visibility = Visibility.Collapsed;
                    ProfileSetupView.Visibility = Visibility.Visible;
                    ProfileSetupView.SetUsername(username);

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                    ProfileSetupView.BeginAnimation(OpacityProperty, fadeIn);
                };
                LoginView.BeginAnimation(OpacityProperty, fadeOut);
            });
        };

        // Subscribe to profile setup completion
        ProfileSetupView.OnSetupComplete += () =>
        {
            Dispatcher.Invoke(() =>
            {
                TransitionToMainApp("Profile setup complete!");
            });
        };

        ProfileSetupView.OnSetupSkipped += () =>
        {
            Dispatcher.Invoke(() =>
            {
                TransitionToMainApp("Welcome! You can customize your profile later.");
            });
        };

        // Navigation changes
        _navigationService.OnNavigate += view =>
        {
            Dispatcher.Invoke(() => SwitchView(view));
        };
    }

    private void TransitionToMainApp(string welcomeMessage)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (s, e) =>
        {
            ProfileSetupView.Visibility = Visibility.Collapsed;
            LoginView.Visibility = Visibility.Collapsed;
            MainAppGrid.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            MainAppGrid.BeginAnimation(OpacityProperty, fadeIn);

            // Show welcome toast
            _toastService.ShowSuccess("Welcome!", welcomeMessage);
        };

        if (ProfileSetupView.Visibility == Visibility.Visible)
        {
            ProfileSetupView.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            LoginView.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void SwitchView(string view)
    {
        // Handle parameterized views (e.g., "Product:123", "Settings:audio")
        var baseView = view.Contains(':') ? view.Split(':')[0] : view;
        _currentView = baseView;

        // Hide all views
        ChatViewControl.Visibility = baseView == "Chat" || baseView == "DirectMessage" ? Visibility.Visible : Visibility.Collapsed;
        MarketplaceViewControl.Visibility = baseView == "Marketplace" || baseView == "Product" ? Visibility.Visible : Visibility.Collapsed;
        ProfileViewControl.Visibility = baseView == "Profile" ? Visibility.Visible : Visibility.Collapsed;
        SettingsViewControl.Visibility = baseView == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        FriendsViewControl.Visibility = baseView == "Friends" ? Visibility.Visible : Visibility.Collapsed;
        VoiceCallDashboardControl.Visibility = baseView == "VoiceCall" ? Visibility.Visible : Visibility.Collapsed;
        CartViewControl.Visibility = baseView == "Cart" ? Visibility.Visible : Visibility.Collapsed;
        WishlistViewControl.Visibility = baseView == "Wishlist" ? Visibility.Visible : Visibility.Collapsed;
        OrderHistoryViewControl.Visibility = baseView == "Orders" || baseView == "Order" ? Visibility.Visible : Visibility.Collapsed;
        NotificationCenterViewControl.Visibility = baseView == "Notifications" ? Visibility.Visible : Visibility.Collapsed;
        ModerationPanelViewControl.Visibility = baseView == "Moderation" ? Visibility.Visible : Visibility.Collapsed;
        ServerBrowserViewControl.Visibility = baseView == "ServerBrowser" ? Visibility.Visible : Visibility.Collapsed;
        ActivityFeedViewControl.Visibility = baseView == "Activity" || baseView == "ActivityFeed" ? Visibility.Visible : Visibility.Collapsed;
        BlockedUsersViewControl.Visibility = baseView == "BlockedUsers" ? Visibility.Visible : Visibility.Collapsed;
        PrivacySettingsViewControl.Visibility = baseView == "Privacy" || baseView == "PrivacySettings" ? Visibility.Visible : Visibility.Collapsed;
        DiscoverViewControl.Visibility = baseView == "Discover" ? Visibility.Visible : Visibility.Collapsed;

        // Update sidebar button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var activeBrush = (SolidColorBrush)FindResource("AccentBrush");
        var inactiveBrush = (SolidColorBrush)FindResource("SecondaryDarkBrush");

        ChatButtonBorder.Background = _currentView == "Chat" ? activeBrush : inactiveBrush;
        MarketButtonBorder.Background = _currentView == "Marketplace" ? activeBrush : inactiveBrush;
        DiscoverButtonBorder.Background = _currentView == "Discover" ? activeBrush : inactiveBrush;
        ActivityButtonBorder.Background = _currentView == "Activity" || _currentView == "ActivityFeed" ? activeBrush : inactiveBrush;
        ProfileButtonBorder.Background = _currentView == "Profile" ? activeBrush : inactiveBrush;
        FriendsButtonBorder.Background = _currentView == "Friends" ? activeBrush : inactiveBrush;

        // Animate the corner radius
        var activeRadius = new CornerRadius(16);
        var inactiveRadius = new CornerRadius(24);

        ChatButtonBorder.CornerRadius = _currentView == "Chat" ? activeRadius : inactiveRadius;
        MarketButtonBorder.CornerRadius = _currentView == "Marketplace" ? activeRadius : inactiveRadius;
        DiscoverButtonBorder.CornerRadius = _currentView == "Discover" ? activeRadius : inactiveRadius;
        ActivityButtonBorder.CornerRadius = _currentView == "Activity" || _currentView == "ActivityFeed" ? activeRadius : inactiveRadius;
        ProfileButtonBorder.CornerRadius = _currentView == "Profile" ? activeRadius : inactiveRadius;
        FriendsButtonBorder.CornerRadius = _currentView == "Friends" ? activeRadius : inactiveRadius;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeRestore();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        MaximizeRestore();
    }

    private void MaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToChat();
    }

    private void ChatButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToChat();
    }

    private void MarketplaceButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToMarketplace();
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToProfile();
    }

    private void FriendsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToFriends();
    }

    private void NotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToNotifications();
    }

    private void CartButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToCart();
    }

    private void DiscoverButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Discover");
    }

    private void ActivityFeedButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("ActivityFeed");
    }

    // Activity Panel
    private bool _isActivityPanelOpen;

    private void ActivityPanelButton_Click(object sender, RoutedEventArgs e)
    {
        _isActivityPanelOpen = !_isActivityPanelOpen;
        ActivityPanelOverlay.Visibility = _isActivityPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        if (_isActivityPanelOpen)
        {
            LoadActivityPanelData();
        }
    }

    private async void LoadActivityPanelData()
    {
        // Load online friends
        var onlineFriends = new List<Controls.SocialActivityPanel.OnlineFriend>();

        if (_friendService.Friends != null)
        {
            foreach (var friend in _friendService.Friends.Where(f => f.IsOnline).Take(10))
            {
                onlineFriends.Add(new Controls.SocialActivityPanel.OnlineFriend
                {
                    UserId = friend.UserId,
                    Username = friend.Username,
                    AvatarUrl = friend.AvatarUrl ?? "pack://application:,,,/Assets/default-avatar.png",
                    Status = friend.StatusMessage
                });
            }
        }

        SocialActivityPanelControl.SetOnlineFriends(onlineFriends);

        // Load sample activities
        var activities = new List<Controls.SocialActivityPanel.ActivityItem>
        {
            new()
            {
                UserId = "1",
                Username = "Player1",
                UserAvatarUrl = "pack://application:,,,/Assets/default-avatar.png",
                Type = Controls.SocialActivityPanel.ActivityType.FriendOnline,
                ActionText = " is now online",
                Timestamp = DateTime.Now.AddMinutes(-5)
            },
            new()
            {
                UserId = "2",
                Username = "Gamer123",
                UserAvatarUrl = "pack://application:,,,/Assets/default-avatar.png",
                Type = Controls.SocialActivityPanel.ActivityType.GameStarted,
                ActionText = " started playing",
                TargetName = "Counter-Strike 2",
                Timestamp = DateTime.Now.AddMinutes(-15),
                HasAction = true,
                ActionIcon = "🎮"
            },
            new()
            {
                UserId = "3",
                Username = "Seller99",
                UserAvatarUrl = "pack://application:,,,/Assets/default-avatar.png",
                Type = Controls.SocialActivityPanel.ActivityType.NewListing,
                ActionText = " listed a new item",
                TargetName = "Premium Software License",
                Timestamp = DateTime.Now.AddHours(-1),
                HasAction = true,
                ActionIcon = "→"
            }
        };

        SocialActivityPanelControl.SetActivities(activities);
    }

    private void SocialActivity_StartGroupCall(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("Group Call", "Starting group call...");
        // Navigate to group call setup
    }

    private void SocialActivity_CreateGroupChat(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("Group Chat", "Creating group chat...");
        // Navigate to group chat creation
    }

    private void SocialActivity_ShareStatus(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("Status", "Opening status editor...");
        // Show status update dialog
    }

    private void SocialActivity_FindFriends(object? sender, EventArgs e)
    {
        _navigationService.NavigateToFriends();
        ActivityPanelOverlay.Visibility = Visibility.Collapsed;
        _isActivityPanelOpen = false;
    }

    private void SocialActivity_OnlineFriendClicked(object? sender, Controls.SocialActivityPanel.OnlineFriend friend)
    {
        // Navigate to DM with this friend
        _toastService.ShowInfo("Opening Chat", $"Starting chat with {friend.Username}...");
        _navigationService.NavigateToFriends();
        ActivityPanelOverlay.Visibility = Visibility.Collapsed;
        _isActivityPanelOpen = false;
    }
}
