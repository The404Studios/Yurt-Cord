using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

public partial class FriendListItem : UserControl
{
    private FriendDto? _friend;

    public FriendDto? Friend
    {
        get => _friend;
        set
        {
            _friend = value;
            UpdateDisplay();
        }
    }

    public event EventHandler<FriendDto>? ViewProfileClicked;
    public event EventHandler<FriendDto>? SendMessageClicked;
    public event EventHandler<FriendDto>? InviteToVoiceClicked;
    public event EventHandler<FriendDto>? StartCallClicked;
    public event EventHandler<FriendDto>? RemoveFriendClicked;

    public FriendListItem()
    {
        InitializeComponent();
    }

    private void UpdateDisplay()
    {
        if (_friend == null)
        {
            DisplayNameText.Text = "Unknown";
            StatusText.Text = "Offline";
            return;
        }

        // Display name
        DisplayNameText.Text = !string.IsNullOrEmpty(_friend.DisplayName)
            ? _friend.DisplayName
            : _friend.Username;

        // Status text
        if (!string.IsNullOrEmpty(_friend.StatusMessage))
        {
            StatusText.Text = _friend.StatusMessage;
        }
        else if (_friend.IsOnline)
        {
            StatusText.Text = "Online";
        }
        else
        {
            StatusText.Text = "Offline";
        }

        // Status indicator color
        StatusIndicator.Background = _friend.IsOnline
            ? (Brush)FindResource("AccentGreenBrush")
            : (Brush)FindResource("TextMutedBrush");

        // Avatar
        if (!string.IsNullOrEmpty(_friend.AvatarUrl))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(_friend.AvatarUrl));
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

        // Role badge
        UpdateRoleBadge();
    }

    private void SetDefaultAvatar()
    {
        AvatarEllipse.Fill = new LinearGradientBrush(
            Color.FromRgb(88, 101, 242),
            Color.FromRgb(235, 69, 158),
            45);
    }

    private void UpdateRoleBadge()
    {
        if (_friend == null)
        {
            RoleBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var (roleName, roleColor) = _friend.Role switch
        {
            UserRole.Owner => ("OWNER", Color.FromRgb(255, 215, 0)),
            UserRole.Admin => ("ADMIN", Color.FromRgb(231, 76, 60)),
            UserRole.Moderator => ("MOD", Color.FromRgb(155, 89, 182)),
            UserRole.VIP => ("VIP", Color.FromRgb(0, 255, 136)),
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

    private void Border_MouseEnter(object sender, MouseEventArgs e)
    {
        RootBorder.Background = (Brush)FindResource("QuaternaryDarkBrush");
        ActionButtons.Visibility = Visibility.Visible;
    }

    private void Border_MouseLeave(object sender, MouseEventArgs e)
    {
        RootBorder.Background = Brushes.Transparent;
        ActionButtons.Visibility = Visibility.Collapsed;
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_friend != null)
            ViewProfileClicked?.Invoke(this, _friend);
    }

    private void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_friend != null)
            SendMessageClicked?.Invoke(this, _friend);
    }

    private void InviteToVoice_Click(object sender, RoutedEventArgs e)
    {
        if (_friend != null)
            InviteToVoiceClicked?.Invoke(this, _friend);
    }

    private void StartCall_Click(object sender, RoutedEventArgs e)
    {
        if (_friend != null)
            StartCallClicked?.Invoke(this, _friend);
    }

    private void RemoveFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_friend == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to remove {_friend.Username} from your friends?",
            "Remove Friend",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            RemoveFriendClicked?.Invoke(this, _friend);
        }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (RootBorder.ContextMenu != null)
        {
            RootBorder.ContextMenu.PlacementTarget = MoreButton;
            RootBorder.ContextMenu.IsOpen = true;
        }
    }
}
