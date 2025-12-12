using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

/// <summary>
/// A reusable nameplate component that displays user information including
/// username, role badge, rank badge, and optional avatar with online status.
/// </summary>
public partial class UserNameplate : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty UsernameProperty =
        DependencyProperty.Register(nameof(Username), typeof(string), typeof(UserNameplate),
            new PropertyMetadata(string.Empty, OnUsernameChanged));

    public static readonly DependencyProperty RoleProperty =
        DependencyProperty.Register(nameof(Role), typeof(UserRole), typeof(UserNameplate),
            new PropertyMetadata(UserRole.Member, OnRoleChanged));

    public static readonly DependencyProperty RankProperty =
        DependencyProperty.Register(nameof(Rank), typeof(UserRank), typeof(UserNameplate),
            new PropertyMetadata(UserRank.Newcomer, OnRankChanged));

    public static readonly DependencyProperty AvatarUrlProperty =
        DependencyProperty.Register(nameof(AvatarUrl), typeof(string), typeof(UserNameplate),
            new PropertyMetadata(null, OnAvatarUrlChanged));

    public static readonly DependencyProperty ShowAvatarProperty =
        DependencyProperty.Register(nameof(ShowAvatar), typeof(bool), typeof(UserNameplate),
            new PropertyMetadata(false, OnShowAvatarChanged));

    public static readonly DependencyProperty IsOnlineProperty =
        DependencyProperty.Register(nameof(IsOnline), typeof(bool), typeof(UserNameplate),
            new PropertyMetadata(false, OnIsOnlineChanged));

    public static readonly DependencyProperty ShowRoleBadgeProperty =
        DependencyProperty.Register(nameof(ShowRoleBadge), typeof(bool), typeof(UserNameplate),
            new PropertyMetadata(true, OnShowRoleBadgeChanged));

    public static readonly DependencyProperty ShowRankBadgeProperty =
        DependencyProperty.Register(nameof(ShowRankBadge), typeof(bool), typeof(UserNameplate),
            new PropertyMetadata(true, OnShowRankBadgeChanged));

    public static readonly DependencyProperty FontSizeOverrideProperty =
        DependencyProperty.Register(nameof(FontSizeOverride), typeof(double), typeof(UserNameplate),
            new PropertyMetadata(14.0, OnFontSizeOverrideChanged));

    public static readonly DependencyProperty AvatarSizeProperty =
        DependencyProperty.Register(nameof(AvatarSize), typeof(double), typeof(UserNameplate),
            new PropertyMetadata(32.0, OnAvatarSizeChanged));

    #endregion

    #region Properties

    public string Username
    {
        get => (string)GetValue(UsernameProperty);
        set => SetValue(UsernameProperty, value);
    }

    public UserRole Role
    {
        get => (UserRole)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    public UserRank Rank
    {
        get => (UserRank)GetValue(RankProperty);
        set => SetValue(RankProperty, value);
    }

    public string? AvatarUrl
    {
        get => (string?)GetValue(AvatarUrlProperty);
        set => SetValue(AvatarUrlProperty, value);
    }

    public bool ShowAvatar
    {
        get => (bool)GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    public bool IsOnline
    {
        get => (bool)GetValue(IsOnlineProperty);
        set => SetValue(IsOnlineProperty, value);
    }

    public bool ShowRoleBadge
    {
        get => (bool)GetValue(ShowRoleBadgeProperty);
        set => SetValue(ShowRoleBadgeProperty, value);
    }

    public bool ShowRankBadge
    {
        get => (bool)GetValue(ShowRankBadgeProperty);
        set => SetValue(ShowRankBadgeProperty, value);
    }

    public double FontSizeOverride
    {
        get => (double)GetValue(FontSizeOverrideProperty);
        set => SetValue(FontSizeOverrideProperty, value);
    }

    public double AvatarSize
    {
        get => (double)GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    #endregion

    public UserNameplate()
    {
        InitializeComponent();
    }

    #region Property Changed Callbacks

    private static void OnUsernameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate)
        {
            nameplate.UsernameText.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private static void OnRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is UserRole role)
        {
            nameplate.UpdateRoleDisplay(role);
        }
    }

    private static void OnRankChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is UserRank rank)
        {
            nameplate.UpdateRankDisplay(rank);
        }
    }

    private static void OnAvatarUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate)
        {
            nameplate.UpdateAvatar(e.NewValue as string);
        }
    }

    private static void OnShowAvatarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is bool show)
        {
            nameplate.AvatarContainer.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnIsOnlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is bool isOnline)
        {
            nameplate.OnlineIndicator.Visibility = (nameplate.ShowAvatar && isOnline) ? Visibility.Visible : Visibility.Collapsed;
            nameplate.OnlineIndicator.Background = isOnline
                ? (SolidColorBrush)nameplate.FindResource("AccentGreenBrush")
                : new SolidColorBrush(Color.FromRgb(116, 127, 141)); // Gray for offline
        }
    }

    private static void OnShowRoleBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate)
        {
            nameplate.UpdateRoleDisplay(nameplate.Role);
        }
    }

    private static void OnShowRankBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate)
        {
            nameplate.UpdateRankDisplay(nameplate.Rank);
        }
    }

    private static void OnFontSizeOverrideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is double fontSize)
        {
            nameplate.UsernameText.FontSize = fontSize;
        }
    }

    private static void OnAvatarSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserNameplate nameplate && e.NewValue is double size)
        {
            nameplate.AvatarBorder.Width = size;
            nameplate.AvatarBorder.Height = size;
            nameplate.AvatarBorder.CornerRadius = new CornerRadius(size / 2);
            nameplate.AvatarEllipse.Width = size;
            nameplate.AvatarEllipse.Height = size;

            // Scale online indicator
            var indicatorSize = Math.Max(8, size * 0.375);
            nameplate.OnlineIndicator.Width = indicatorSize;
            nameplate.OnlineIndicator.Height = indicatorSize;
            nameplate.OnlineIndicator.CornerRadius = new CornerRadius(indicatorSize / 2);
        }
    }

    #endregion

    #region Update Methods

    private void UpdateRoleDisplay(UserRole role)
    {
        // Set username color based on role
        UsernameText.Foreground = new SolidColorBrush(GetRoleColor(role));

        // Show role badge for VIP and above
        if (ShowRoleBadge && role >= UserRole.VIP)
        {
            RoleBadge.Visibility = Visibility.Visible;
            RoleBadge.Background = new SolidColorBrush(GetRoleColor(role));
            RoleBadgeText.Text = GetRoleName(role);
        }
        else
        {
            RoleBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateRankDisplay(UserRank rank)
    {
        // Show rank badge for Silver and above
        if (ShowRankBadge && rank >= UserRank.Silver)
        {
            RankBadge.Visibility = Visibility.Visible;
            RankBadge.Background = new SolidColorBrush(GetRankColor(rank));
            RankBadgeText.Text = GetRankText(rank);
        }
        else
        {
            RankBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateAvatar(string? avatarUrl)
    {
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(avatarUrl, UriKind.Absolute);
                bitmap.EndInit();
                AvatarBrush.ImageSource = bitmap;
            }
            catch
            {
                // Use default avatar background
                AvatarBorder.Background = new SolidColorBrush(GetRoleColor(Role));
            }
        }
        else
        {
            // Use role color as avatar background when no image
            AvatarBorder.Background = new SolidColorBrush(GetRoleColor(Role));
        }
    }

    #endregion

    #region Helper Methods

    private static Color GetRoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.FromRgb(255, 215, 0),      // Gold
            UserRole.Admin => Color.FromRgb(231, 76, 60),      // Red
            UserRole.Moderator => Color.FromRgb(155, 89, 182), // Purple
            UserRole.VIP => Color.FromRgb(0, 255, 136),        // Green
            UserRole.Verified => Color.FromRgb(52, 152, 219),  // Blue
            _ => Color.FromRgb(185, 187, 190)                   // Gray
        };
    }

    private static string GetRoleName(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => "OWNER",
            UserRole.Admin => "ADMIN",
            UserRole.Moderator => "MOD",
            UserRole.VIP => "VIP",
            UserRole.Verified => "VERIFIED",
            _ => ""
        };
    }

    private static Color GetRankColor(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => Color.FromRgb(255, 215, 0),   // Gold
            UserRank.Elite => Color.FromRgb(231, 76, 60),    // Red
            UserRank.Diamond => Color.FromRgb(0, 255, 255),  // Cyan
            UserRank.Platinum => Color.FromRgb(229, 228, 226), // Silver-white
            UserRank.Gold => Color.FromRgb(255, 215, 0),     // Gold
            UserRank.Silver => Color.FromRgb(192, 192, 192), // Silver
            UserRank.Bronze => Color.FromRgb(205, 127, 50),  // Bronze
            _ => Color.FromRgb(149, 165, 166)                 // Gray
        };
    }

    private static string GetRankText(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "Legend",
            UserRank.Elite => "Elite",
            UserRank.Diamond => "Diamond",
            UserRank.Platinum => "Platinum",
            UserRank.Gold => "Gold",
            UserRank.Silver => "Silver",
            UserRank.Bronze => "Bronze",
            _ => "Newcomer"
        };
    }

    private static string GetRankEmoji(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "crown",
            UserRank.Elite => "fire",
            UserRank.Diamond => "gem",
            UserRank.Platinum => "sparkles",
            UserRank.Gold => "first_place_medal",
            UserRank.Silver => "second_place_medal",
            UserRank.Bronze => "third_place_medal",
            _ => "star"
        };
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the nameplate from a UserDto
    /// </summary>
    public void SetUser(UserDto user, bool showAvatar = false)
    {
        Username = user.DisplayName ?? user.Username;
        Role = user.Role;
        Rank = user.Rank;
        AvatarUrl = user.AvatarUrl;
        ShowAvatar = showAvatar;
        IsOnline = user.IsOnline;
    }

    /// <summary>
    /// Set the nameplate from an OnlineUserDto
    /// </summary>
    public void SetUser(OnlineUserDto user, bool showAvatar = false)
    {
        Username = user.DisplayName ?? user.Username;
        Role = user.Role;
        Rank = user.Rank;
        AvatarUrl = user.AvatarUrl;
        ShowAvatar = showAvatar;
        IsOnline = true; // Online users are always online
    }

    /// <summary>
    /// Set the nameplate from a ChatMessageDto (for message display)
    /// </summary>
    public void SetFromMessage(ChatMessageDto message)
    {
        Username = message.SenderUsername;
        Role = message.SenderRole;
        Rank = message.SenderRank;
        ShowAvatar = false; // Messages have separate avatar display
    }

    #endregion
}
