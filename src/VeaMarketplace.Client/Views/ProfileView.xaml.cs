using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class ProfileView : UserControl
{
    private readonly ProfileViewModel? _viewModel;
    private readonly IApiService? _apiService;
    private readonly INavigationService? _navigationService;
    private readonly IFriendService? _friendService;
    private readonly IToastNotificationService? _toastService;
    private bool _isFollowing;
    private bool _isFriend;

    public ProfileView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ProfileViewModel)App.ServiceProvider.GetService(typeof(ProfileViewModel))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;
        _friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        _toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));

        DataContext = _viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Cleanup();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileViewModel.User))
        {
            Dispatcher.Invoke(UpdateUI);
        }
    }

    private void UpdateUI()
    {
        var user = _viewModel?.User ?? _apiService?.CurrentUser;
        if (user == null) return;

        // Determine if viewing own profile or another user's
        var currentUser = _apiService?.CurrentUser;
        var isOwnProfile = currentUser != null && user.Id == currentUser.Id;

        // Show/hide buttons based on profile ownership
        EditProfileButton.Visibility = isOwnProfile ? Visibility.Visible : Visibility.Collapsed;
        FollowButton.Visibility = isOwnProfile ? Visibility.Collapsed : Visibility.Visible;
        AddFriendButton.Visibility = isOwnProfile ? Visibility.Collapsed : Visibility.Visible;
        MessageButton.Visibility = isOwnProfile ? Visibility.Collapsed : Visibility.Visible;

        // Update follow/friend button states for other users
        if (!isOwnProfile)
        {
            UpdateFollowFriendStates(user);
        }

        // Display Name and Username
        var displayName = user.GetDisplayName();
        DisplayNameText.Text = displayName;

        // Show username separately if display name is different
        if (!string.IsNullOrEmpty(user.DisplayName) && user.DisplayName != user.Username)
        {
            UsernameText.Text = $"@{user.Username}";
            UsernameText.Visibility = Visibility.Visible;
        }
        else
        {
            UsernameText.Visibility = Visibility.Collapsed;
        }

        // Status Message
        if (!string.IsNullOrEmpty(user.StatusMessage))
        {
            StatusText.Text = user.StatusMessage;
            StatusPanel.Visibility = Visibility.Visible;

            // Set status dot color based on accent color
            try
            {
                var accentColor = string.IsNullOrEmpty(user.AccentColor) ? "#5865F2" : user.AccentColor;
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor));
            }
            catch
            {
                StatusDot.Fill = (Brush)FindResource("AccentBrush");
            }
        }
        else
        {
            StatusPanel.Visibility = Visibility.Collapsed;
        }

        // Bio and Description
        BioText.Text = string.IsNullOrEmpty(user.Bio) ? "No bio yet..." : user.Bio;
        DescriptionText.Text = string.IsNullOrEmpty(user.Description)
            ? "This user hasn't written anything about themselves yet."
            : user.Description;

        // Stats
        JoinedText.Text = $"Joined {user.CreatedAt:MMMM yyyy}";
        BalanceText.Text = $"${user.Balance:F2}";
        ReputationText.Text = user.Reputation.ToString();
        SalesText.Text = user.TotalSales.ToString();
        PurchasesText.Text = user.TotalPurchases.ToString();

        // Load follower/following counts
        _ = LoadFollowStatsAsync(user.Id);

        // Accent Color for Avatar Ring
        try
        {
            var accentColor = string.IsNullOrEmpty(user.AccentColor) ? "#5865F2" : user.AccentColor;
            var color = (Color)ColorConverter.ConvertFromString(accentColor);
            AvatarAccentRing.Stroke = new SolidColorBrush(color);

            // Update banner gradient with accent color
            BannerGradient1.Color = color;
            // Create a secondary color by shifting hue
            var secondaryColor = Color.FromRgb(
                (byte)Math.Max(0, color.R - 50),
                (byte)Math.Min(255, color.G + 30),
                (byte)Math.Min(255, color.B + 80)
            );
            BannerGradient2.Color = secondaryColor;
        }
        catch
        {
            AvatarAccentRing.Stroke = (Brush)FindResource("AccentBrush");
        }

        // Avatar
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try
            {
                AvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProfileView: Failed to load avatar: {ex.Message}");
            }
        }

        // Banner
        if (!string.IsNullOrEmpty(user.BannerUrl))
        {
            try
            {
                BannerBrush.ImageSource = new BitmapImage(new Uri(user.BannerUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProfileView: Failed to load banner: {ex.Message}");
            }
        }

        // Role Badge
        RoleBadge.Background = new SolidColorBrush(GetRoleColor(user.Role));
        RoleBadgeText.Text = user.Role.ToString().ToUpper();

        // Rank Badge
        RankBadge.Background = new SolidColorBrush(GetRankColor(user.Rank));
        RankBadgeText.Text = $"{GetRankEmoji(user.Rank)} {user.Rank}";

        // Social Links
        UpdateSocialLinks(user);

        // Custom Roles
        RolesPanel.Children.Clear();
        foreach (var role in user.CustomRoles)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(role.Color)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var text = new TextBlock
            {
                Text = role.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            border.Child = text;
            RolesPanel.Children.Add(border);
        }

        // If no custom roles, show a placeholder
        if (user.CustomRoles.Count == 0)
        {
            RolesPanel.Children.Add(new TextBlock
            {
                Text = "No custom roles assigned",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextMutedBrush")
            });
        }
    }

    private void UpdateSocialLinks(UserDto user)
    {
        SocialLinksPanel.Children.Clear();

        bool hasAnyLink = false;

        // Discord
        if (!string.IsNullOrEmpty(user.DiscordUsername))
        {
            hasAnyLink = true;
            SocialLinksPanel.Children.Add(CreateSocialLinkItem("Discord", user.DiscordUsername, "#5865F2"));
        }

        // Twitter/X
        if (!string.IsNullOrEmpty(user.TwitterHandle))
        {
            hasAnyLink = true;
            SocialLinksPanel.Children.Add(CreateSocialLinkItem("Twitter", $"@{user.TwitterHandle}", "#1DA1F2"));
        }

        // Telegram
        if (!string.IsNullOrEmpty(user.TelegramUsername))
        {
            hasAnyLink = true;
            SocialLinksPanel.Children.Add(CreateSocialLinkItem("Telegram", $"@{user.TelegramUsername}", "#0088CC"));
        }

        // Website
        if (!string.IsNullOrEmpty(user.WebsiteUrl))
        {
            hasAnyLink = true;
            SocialLinksPanel.Children.Add(CreateSocialLinkItem("Website", user.WebsiteUrl, "#95A5A6"));
        }

        // Show placeholder if no links
        if (!hasAnyLink)
        {
            SocialLinksPanel.Children.Add(new TextBlock
            {
                Text = "No connections added",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextMutedBrush")
            });
        }
    }

    private UIElement CreateSocialLinkItem(string platform, string value, string colorHex)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Platform icon
        var iconBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            Margin = new Thickness(0, 0, 10, 0)
        };
        var iconText = new TextBlock
        {
            Text = !string.IsNullOrEmpty(platform) ? platform[0].ToString() : "?",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        iconBorder.Child = iconText;
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        // Value text
        var valuePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var platformText = new TextBlock
        {
            Text = platform,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMutedBrush")
        };
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        valuePanel.Children.Add(platformText);
        valuePanel.Children.Add(valueText);
        Grid.SetColumn(valuePanel, 1);
        grid.Children.Add(valuePanel);

        return grid;
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        var user = _viewModel?.User ?? _apiService?.CurrentUser;
        if (user == null) return;

        var dialog = new ProfileEditDialog(user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.UpdatedUser != null)
        {
            // Refresh the UI with updated user
            if (_viewModel != null)
            {
                _viewModel.User = dialog.UpdatedUser;
            }
            UpdateUI();
        }
    }

    private static Color GetRoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.FromRgb(255, 215, 0),
            UserRole.Admin => Color.FromRgb(231, 76, 60),
            UserRole.Moderator => Color.FromRgb(155, 89, 182),
            UserRole.VIP => Color.FromRgb(0, 255, 136),
            UserRole.Verified => Color.FromRgb(52, 152, 219),
            _ => Color.FromRgb(149, 165, 166)
        };
    }

    private static Color GetRankColor(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => Color.FromRgb(255, 215, 0),
            UserRank.Elite => Color.FromRgb(231, 76, 60),
            UserRank.Diamond => Color.FromRgb(0, 255, 255),
            UserRank.Platinum => Color.FromRgb(229, 228, 226),
            UserRank.Gold => Color.FromRgb(255, 215, 0),
            UserRank.Silver => Color.FromRgb(192, 192, 192),
            UserRank.Bronze => Color.FromRgb(205, 127, 50),
            _ => Color.FromRgb(149, 165, 166)
        };
    }

    private static string GetRankEmoji(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "Crown",
            UserRank.Elite => "Fire",
            UserRank.Diamond => "Diamond",
            UserRank.Platinum => "Star",
            UserRank.Gold => "Gold",
            UserRank.Silver => "Silver",
            UserRank.Bronze => "Bronze",
            _ => "Star"
        };
    }

    private void Wishlist_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToWishlist();
    }

    private void OrderHistory_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateTo("Orders");
    }

    private void MyListings_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void ViewCart_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToCart();
    }

    private void UpdateFollowFriendStates(UserDto user)
    {
        // Check if already following
        _isFollowing = user.IsFollowedByCurrentUser;
        UpdateFollowButtonUI();

        // Check if already friends
        if (_friendService?.Friends != null)
        {
            _isFriend = _friendService.Friends.Any(f => f.UserId == user.Id);
            UpdateFriendButtonUI();
        }
    }

    private void UpdateFollowButtonUI()
    {
        if (_isFollowing)
        {
            FollowIcon.Text = "✓";
            FollowText.Text = "Following";
            FollowButton.Background = (Brush)FindResource("AccentBrush");
        }
        else
        {
            FollowIcon.Text = "👁";
            FollowText.Text = "Follow";
            FollowButton.Background = (Brush)FindResource("SecondaryDarkBrush");
        }
    }

    private void UpdateFriendButtonUI()
    {
        if (_isFriend)
        {
            FriendIcon.Text = "✓";
            FriendText.Text = "Friends";
            AddFriendButton.IsEnabled = false;
        }
        else
        {
            FriendIcon.Text = "👥";
            FriendText.Text = "Add Friend";
            AddFriendButton.IsEnabled = true;
        }
    }

    private async void FollowUser_Click(object sender, RoutedEventArgs e)
    {
        var user = _viewModel?.User;
        if (user == null || _apiService == null) return;

        try
        {
            if (_isFollowing)
            {
                await _apiService.UnfollowUserAsync(user.Id);
                _isFollowing = false;
            }
            else
            {
                await _apiService.FollowUserAsync(user.Id);
                _isFollowing = true;
            }
            UpdateFollowButtonUI();
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Follow Failed", ex.Message);
        }
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        var user = _viewModel?.User;
        if (user == null || _friendService == null) return;

        try
        {
            await _friendService.SendFriendRequestAsync(user.Username);
            _toastService?.ShowSuccess("Friend Request Sent", $"Sent to {user.Username}");
            FriendText.Text = "Request Sent";
            AddFriendButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            _toastService?.ShowError("Request Failed", ex.Message);
        }
    }

    private void MessageUser_Click(object sender, RoutedEventArgs e)
    {
        var user = _viewModel?.User;
        if (user == null) return;

        // Navigate to friends view with DM to this user
        _navigationService?.NavigateToFriends();
    }

    private async Task LoadFollowStatsAsync(string userId)
    {
        if (_apiService == null) return;

        try
        {
            var followStatus = await _apiService.GetFollowStatusAsync(userId);
            Dispatcher.Invoke(() =>
            {
                FollowersCountText.Text = followStatus.FollowerCount.ToString("N0");
                FollowingCountText.Text = followStatus.FollowingCount.ToString("N0");

                // Update follow button state if viewing another user's profile
                var currentUser = _apiService?.CurrentUser;
                if (currentUser != null && userId != currentUser.Id)
                {
                    _isFollowing = followStatus.IsFollowing;
                    UpdateFollowButtonUI();
                }
            });
        }
        catch
        {
            // Default to 0 on error
            Dispatcher.Invoke(() =>
            {
                FollowersCountText.Text = "0";
                FollowingCountText.Text = "0";
            });
        }
    }

    private void Followers_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var user = _viewModel?.User ?? _apiService?.CurrentUser;
        if (user == null) return;

        // Show followers list dialog
        ShowFollowListDialog(user.Id, "Followers");
    }

    private void Following_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var user = _viewModel?.User ?? _apiService?.CurrentUser;
        if (user == null) return;

        // Show following list dialog
        ShowFollowListDialog(user.Id, "Following");
    }

    private void ShowFollowListDialog(string userId, string listType)
    {
        // For now, show a simple toast - can be expanded to a full dialog later
        var displayName = _viewModel?.User?.GetDisplayName() ?? _apiService?.CurrentUser?.GetDisplayName() ?? "User";
        _toastService?.ShowInfo(listType, $"View {listType} list for {displayName} - coming soon!");
    }
}
