using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class OverseerWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly IToastNotificationService _toastService;
    private readonly IFriendService _friendService;
    private readonly IVoiceService _voiceService;
    private string _currentView = "Chat";
    private Button? _activeNavButton;

    public OverseerWindow()
    {
        InitializeComponent();

        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;
        _friendService = (IFriendService)App.ServiceProvider.GetService(typeof(IFriendService))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;

        // Set up toast notification container
        _toastService.SetContainer(ToastContainer);

        // Subscribe to events
        _friendService.OnNewFriendRequest += OnNewFriendRequest;
        _friendService.OnDirectMessageReceived += OnDirectMessageReceived;
        _voiceService.OnNudgeReceived += OnNudgeReceived;
        _voiceService.OnGroupCallInvite += OnGroupCallInvite;
        _voiceService.OnGroupCallStarted += OnGroupCallStarted;

        LoginView.OnLoginSuccess += OnLoginSuccess;
        LoginView.OnRegistrationSuccess += OnRegistrationSuccess;
        ProfileSetupView.OnSetupComplete += OnProfileSetupComplete;
        ProfileSetupView.OnSetupSkipped += OnProfileSetupSkipped;

        _navigationService.OnNavigate += OnNavigate;
        _navigationService.OnLogoutRequested += OnLogoutRequested;

        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _friendService.OnNewFriendRequest -= OnNewFriendRequest;
        _friendService.OnDirectMessageReceived -= OnDirectMessageReceived;
        _voiceService.OnNudgeReceived -= OnNudgeReceived;
        _voiceService.OnGroupCallInvite -= OnGroupCallInvite;
        _voiceService.OnGroupCallStarted -= OnGroupCallStarted;
        LoginView.OnLoginSuccess -= OnLoginSuccess;
        LoginView.OnRegistrationSuccess -= OnRegistrationSuccess;
        ProfileSetupView.OnSetupComplete -= OnProfileSetupComplete;
        ProfileSetupView.OnSetupSkipped -= OnProfileSetupSkipped;
        _navigationService.OnNavigate -= OnNavigate;
        _navigationService.OnLogoutRequested -= OnLogoutRequested;
    }

    private void OnNewFriendRequest(Shared.DTOs.FriendRequestDto request)
    {
        _toastService.ShowFriendRequest(request.RequesterUsername);
    }

    private void OnDirectMessageReceived(Shared.DTOs.DirectMessageDto message)
    {
        _toastService.ShowMessage(message.SenderUsername,
            message.Content.Length > 50 ? message.Content[..50] + "..." : message.Content);
    }

    private void OnNudgeReceived(Shared.DTOs.NudgeDto nudge)
    {
        Dispatcher.Invoke(() =>
        {
            NudgeNotification.Show(nudge);
        });
    }

    private void OnGroupCallInvite(Shared.DTOs.GroupCallInviteDto invite)
    {
        Dispatcher.Invoke(() =>
        {
            GroupCallInviteNotification.Show(invite, _voiceService);
        });
    }

    private void OnGroupCallStarted(Shared.DTOs.GroupCallDto call)
    {
        Dispatcher.Invoke(() =>
        {
            GroupCallViewControl.SetCallInfo(call);
            _navigationService.NavigateToGroupCall();
        });
    }

    private void OnLoginSuccess()
    {
        Dispatcher.Invoke(() =>
        {
            // Animate with neon glow pulse
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            fadeOut.Completed += (s, e) =>
            {
                LoginView.Visibility = Visibility.Collapsed;
                MainAppGrid.Visibility = Visibility.Visible;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // Scale animation for dramatic entrance
                var scale = new ScaleTransform(0.9, 0.9);
                MainAppGrid.RenderTransform = scale;
                MainAppGrid.RenderTransformOrigin = new Point(0.5, 0.5);

                var scaleX = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 }
                };
                var scaleY = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 }
                };

                MainAppGrid.BeginAnimation(OpacityProperty, fadeIn);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

                _toastService.ShowSuccess("Welcome!", "Connected to Yurt Cord");
            };
            LoginView.BeginAnimation(OpacityProperty, fadeOut);
        });
    }

    private void OnRegistrationSuccess(string username)
    {
        Dispatcher.Invoke(() =>
        {
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
    }

    private void OnProfileSetupComplete()
    {
        Dispatcher.Invoke(() =>
        {
            TransitionToMainApp("Profile initialized. Welcome to Yurt Cord!");
        });
    }

    private void OnProfileSetupSkipped()
    {
        Dispatcher.Invoke(() =>
        {
            TransitionToMainApp("Profile setup skipped. You can customize later.");
        });
    }

    private void OnNavigate(string view)
    {
        Dispatcher.Invoke(() => SwitchView(view));
    }

    private void OnLogoutRequested()
    {
        Dispatcher.Invoke(async () => await HandleLogoutAsync());
    }

    private async Task HandleLogoutAsync()
    {
        var apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        var chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
        var profileService = (IProfileService)App.ServiceProvider.GetService(typeof(IProfileService))!;
        var contentService = (IContentService)App.ServiceProvider.GetService(typeof(IContentService))!;
        var settingsService = (ISettingsService)App.ServiceProvider.GetService(typeof(ISettingsService))!;

        if (_voiceService.IsInVoiceChannel)
        {
            await _voiceService.LeaveVoiceChannelAsync();
        }

        try
        {
            await Task.WhenAll(
                _voiceService.DisconnectAsync(),
                chatService.DisconnectAsync(),
                _friendService.DisconnectAsync(),
                profileService.DisconnectAsync(),
                contentService.DisconnectAsync()
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during logout disconnect: {ex.Message}");
        }

        apiService.Logout();
        settingsService.Settings.SavedToken = null;
        settingsService.SaveSettings();

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (s, e) =>
        {
            MainAppGrid.Visibility = Visibility.Collapsed;
            LoginView.Visibility = Visibility.Visible;
            LoginView.Reset();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            LoginView.BeginAnimation(OpacityProperty, fadeIn);

            _toastService.ShowInfo("DISCONNECTED", "Session terminated successfully.");
        };
        MainAppGrid.BeginAnimation(OpacityProperty, fadeOut);
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

            _toastService.ShowSuccess("INITIALIZED", welcomeMessage);
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
        var baseView = view.Contains(':') ? view.Split(':')[0] : view;
        _currentView = baseView;

        // Animate view transition
        AnimateViewTransition(() =>
        {
            ChatViewControl.Visibility = baseView == "Chat" || baseView == "DirectMessage" ? Visibility.Visible : Visibility.Collapsed;
            MarketplaceViewControl.Visibility = baseView == "Marketplace" || baseView == "Product" ? Visibility.Visible : Visibility.Collapsed;
            ProfileViewControl.Visibility = baseView == "Profile" ? Visibility.Visible : Visibility.Collapsed;
            SettingsViewControl.Visibility = baseView == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            FriendsViewControl.Visibility = baseView == "Friends" ? Visibility.Visible : Visibility.Collapsed;
            VoiceCallDashboardControl.Visibility = baseView == "VoiceCall" ? Visibility.Visible : Visibility.Collapsed;
            GroupCallViewControl.Visibility = baseView == "GroupCall" ? Visibility.Visible : Visibility.Collapsed;
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
            LeaderboardViewControl.Visibility = baseView == "Leaderboard" ? Visibility.Visible : Visibility.Collapsed;
        });

        UpdateButtonStates();
    }

    private void AnimateViewTransition(Action switchAction)
    {
        var fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(100));
        fadeOut.Completed += (s, e) =>
        {
            switchAction();
            var fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(150));
            ChatViewControl.BeginAnimation(OpacityProperty, fadeIn);
        };
        ChatViewControl.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateButtonStates()
    {
        var buttons = new Dictionary<string, Button>
        {
            { "Chat", ChatButtonOrb },
            { "DirectMessage", ChatButtonOrb },
            { "Marketplace", MarketButtonOrb },
            { "Product", MarketButtonOrb },
            { "Discover", DiscoverButtonOrb },
            { "Activity", ActivityButtonOrb },
            { "ActivityFeed", ActivityButtonOrb },
            { "Profile", ProfileButtonOrb },
            { "Friends", FriendsButtonOrb },
            { "Settings", SettingsButtonOrb }
        };

        // Reset all buttons
        foreach (var btn in buttons.Values.Distinct())
        {
            btn.BorderBrush = (SolidColorBrush)FindResource("OverseerPrimaryBrush");
            btn.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 180, 216),  // #00B4D8 teal
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.5
            };
        }

        // Highlight active button
        if (buttons.TryGetValue(_currentView, out var activeButton))
        {
            activeButton.BorderBrush = (SolidColorBrush)FindResource("OverseerAccentBrush");
            activeButton.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(72, 202, 228),  // #48CAE4 light teal
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            _activeNavButton = activeButton;
        }
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Settings");
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

    private void LoadActivityPanelData()
    {
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

        var activities = new List<Controls.SocialActivityPanel.ActivityItem>
        {
            new()
            {
                UserId = "1",
                Username = "Agent_X",
                UserAvatarUrl = "pack://application:,,,/Assets/default-avatar.png",
                Type = Controls.SocialActivityPanel.ActivityType.FriendOnline,
                ActionText = " connected to network",
                Timestamp = DateTime.Now.AddMinutes(-5)
            },
            new()
            {
                UserId = "2",
                Username = "CyberNinja",
                UserAvatarUrl = "pack://application:,,,/Assets/default-avatar.png",
                Type = Controls.SocialActivityPanel.ActivityType.GameStarted,
                ActionText = " initiated session",
                TargetName = "Neural Network Training",
                Timestamp = DateTime.Now.AddMinutes(-15),
                HasAction = true,
                ActionIcon = ">"
            }
        };

        SocialActivityPanelControl.SetActivities(activities);
    }

    private void SocialActivity_StartGroupCall(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("INITIATING", "Starting secure group channel...");
    }

    private void SocialActivity_CreateGroupChat(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("CREATING", "Establishing group connection...");
    }

    private void SocialActivity_ShareStatus(object? sender, EventArgs e)
    {
        _toastService.ShowInfo("STATUS", "Broadcasting status update...");
    }

    private void SocialActivity_FindFriends(object? sender, EventArgs e)
    {
        _navigationService.NavigateToFriends();
        ActivityPanelOverlay.Visibility = Visibility.Collapsed;
        _isActivityPanelOpen = false;
    }

    private void SocialActivity_OnlineFriendClicked(object? sender, Controls.SocialActivityPanel.OnlineFriend friend)
    {
        _toastService.ShowInfo("CONNECTING", $"Opening channel with {friend.Username}...");
        _navigationService.NavigateToFriends();
        ActivityPanelOverlay.Visibility = Visibility.Collapsed;
        _isActivityPanelOpen = false;
    }
}
