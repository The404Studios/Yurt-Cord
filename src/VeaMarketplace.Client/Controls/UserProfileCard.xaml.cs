using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

public partial class UserProfileCard : UserControl
{
    public static readonly DependencyProperty UserProperty =
        DependencyProperty.Register(nameof(User), typeof(UserDto), typeof(UserProfileCard),
            new PropertyMetadata(null, OnUserChanged));

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(nameof(ShowActions), typeof(bool), typeof(UserProfileCard),
            new PropertyMetadata(false, OnShowActionsChanged));

    public static readonly DependencyProperty IsOnlineProperty =
        DependencyProperty.Register(nameof(IsOnline), typeof(bool), typeof(UserProfileCard),
            new PropertyMetadata(false, OnIsOnlineChanged));

    public UserDto? User
    {
        get => (UserDto?)GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public bool IsOnline
    {
        get => (bool)GetValue(IsOnlineProperty);
        set => SetValue(IsOnlineProperty, value);
    }

    public event EventHandler<UserDto>? SendMessageClicked;
    public event EventHandler<UserDto>? AddFriendClicked;

    public UserProfileCard()
    {
        InitializeComponent();
    }

    private static void OnUserChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserProfileCard card)
            card.UpdateUserDisplay();
    }

    private static void OnShowActionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserProfileCard card)
            card.ActionsPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnIsOnlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserProfileCard card)
            card.UpdateStatusIndicator();
    }

    private void UpdateUserDisplay()
    {
        if (User == null)
        {
            DisplayNameText.Text = "Unknown User";
            UsernameText.Text = "@unknown";
            return;
        }

        // Display name and username
        DisplayNameText.Text = !string.IsNullOrEmpty(User.DisplayName) ? User.DisplayName : User.Username;
        UsernameText.Text = $"@{User.Username}";

        // Avatar
        if (!string.IsNullOrEmpty(User.AvatarUrl))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(User.AvatarUrl));
                AvatarBrush.ImageSource = bitmap;
            }
            catch
            {
                SetDefaultAvatar();
            }
        }
        else
        {
            SetDefaultAvatar();
        }

        // Accent color for banner
        if (!string.IsNullOrEmpty(User.AccentColor))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(User.AccentColor);
                BannerColor1.Color = color;
                // Make a slightly different shade for gradient
                BannerColor2.Color = Color.FromRgb(
                    (byte)Math.Max(0, color.R - 40),
                    (byte)Math.Max(0, color.G - 40),
                    (byte)Math.Max(0, color.B + 40));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserProfileCard: Failed to parse accent color: {ex.Message}");
            }
        }

        // Status message
        if (!string.IsNullOrEmpty(User.StatusMessage))
        {
            StatusSection.Visibility = Visibility.Visible;
            StatusText.Text = User.StatusMessage;
        }
        else
        {
            StatusSection.Visibility = Visibility.Collapsed;
        }

        // Bio/About Me
        if (!string.IsNullOrEmpty(User.Bio))
        {
            AboutSection.Visibility = Visibility.Visible;
            BioText.Text = User.Bio;
        }
        else
        {
            AboutSection.Visibility = Visibility.Collapsed;
        }

        // Member since
        MemberSinceText.Text = User.CreatedAt.ToString("MMM d, yyyy");

        // Role badge
        UpdateRoleBadge();

        // Status indicator
        UpdateStatusIndicator();
    }

    private void SetDefaultAvatar()
    {
        // Set a default gradient avatar
        AvatarEllipse.Fill = new LinearGradientBrush(
            Color.FromRgb(88, 101, 242),
            Color.FromRgb(235, 69, 158),
            45);
    }

    private void UpdateRoleBadge()
    {
        if (User == null)
        {
            RoleBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var (roleName, roleColor) = User.Role switch
        {
            UserRole.Owner => ("OWNER", Color.FromRgb(255, 215, 0)),
            UserRole.Admin => ("ADMIN", Color.FromRgb(231, 76, 60)),
            UserRole.Moderator => ("MOD", Color.FromRgb(155, 89, 182)),
            UserRole.VIP => ("VIP", Color.FromRgb(0, 255, 136)),
            UserRole.Verified => ("VERIFIED", Color.FromRgb(52, 152, 219)),
            _ => (null, default(Color))
        };

        if (roleName != null)
        {
            RoleBadge.Visibility = Visibility.Visible;
            RoleBadge.Background = new SolidColorBrush(roleColor);
            RoleBadgeText.Text = roleName;
        }
        else
        {
            RoleBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateStatusIndicator()
    {
        if (IsOnline)
        {
            StatusIndicator.Background = (Brush)FindResource("AccentGreenBrush");
        }
        else
        {
            StatusIndicator.Background = (Brush)FindResource("TextMutedBrush");
        }
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        if (User != null)
            SendMessageClicked?.Invoke(this, User);
    }

    private void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (User != null)
            AddFriendClicked?.Invoke(this, User);
    }

    public void SetUser(UserDto user, bool isOnline = false, bool showActions = true)
    {
        User = user;
        IsOnline = isOnline;
        ShowActions = showActions;
    }
}
