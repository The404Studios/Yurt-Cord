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

        // Navigation changes
        _navigationService.OnNavigate += view =>
        {
            Dispatcher.Invoke(() => SwitchView(view));
        };
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

        // Update sidebar button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var activeBrush = (SolidColorBrush)FindResource("AccentBrush");
        var inactiveBrush = (SolidColorBrush)FindResource("SecondaryDarkBrush");

        ChatButtonBorder.Background = _currentView == "Chat" ? activeBrush : inactiveBrush;
        MarketButtonBorder.Background = _currentView == "Marketplace" ? activeBrush : inactiveBrush;
        ProfileButtonBorder.Background = _currentView == "Profile" ? activeBrush : inactiveBrush;
        FriendsButtonBorder.Background = _currentView == "Friends" ? activeBrush : inactiveBrush;

        // Animate the corner radius
        var activeRadius = new CornerRadius(16);
        var inactiveRadius = new CornerRadius(24);

        ChatButtonBorder.CornerRadius = _currentView == "Chat" ? activeRadius : inactiveRadius;
        MarketButtonBorder.CornerRadius = _currentView == "Marketplace" ? activeRadius : inactiveRadius;
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
}
