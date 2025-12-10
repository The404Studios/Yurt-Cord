using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

public partial class ChatMessageControl : UserControl
{
    private ChatMessageDto? _currentMessage;

    // Events for parent to handle
    public static readonly RoutedEvent ReplyRequestedEvent = EventManager.RegisterRoutedEvent(
        "ReplyRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageControl));

    public static readonly RoutedEvent MentionRequestedEvent = EventManager.RegisterRoutedEvent(
        "MentionRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageControl));

    public event RoutedEventHandler ReplyRequested
    {
        add => AddHandler(ReplyRequestedEvent, value);
        remove => RemoveHandler(ReplyRequestedEvent, value);
    }

    public event RoutedEventHandler MentionRequested
    {
        add => AddHandler(MentionRequestedEvent, value);
        remove => RemoveHandler(MentionRequestedEvent, value);
    }

    public ChatMessageControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        MouseEnter += (s, e) => ActionsPanel.Visibility = Visibility.Visible;
        MouseLeave += (s, e) => ActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ChatMessageDto message)
        {
            _currentMessage = message;
            UpdateUI(message);
            UpdateContextMenuVisibility(message);
        }
    }

    private void UpdateContextMenuVisibility(ChatMessageDto message)
    {
        // Check if current user owns this message to show edit/delete options
        var apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));
        var currentUser = apiService?.CurrentUser;

        if (currentUser != null && message.SenderId == currentUser.Id)
        {
            // Find menu items in the context menu resource
            if (Resources["MessageContextMenu"] is ContextMenu menu)
            {
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (menuItem.Name == "EditMenuItem" || menuItem.Name == "DeleteMenuItem")
                        {
                            menuItem.Visibility = Visibility.Visible;
                        }
                        else if (menuItem.Name == "DeleteSeparator")
                        {
                            menuItem.Visibility = Visibility.Visible;
                        }
                    }
                    else if (item is Separator sep && sep.Name == "DeleteSeparator")
                    {
                        sep.Visibility = Visibility.Visible;
                    }
                }
            }
        }
    }

    private void UpdateUI(ChatMessageDto message)
    {
        // Set avatar
        if (!string.IsNullOrEmpty(message.SenderAvatarUrl))
        {
            try
            {
                AvatarImage.ImageSource = new BitmapImage(new Uri(message.SenderAvatarUrl));
            }
            catch
            {
                // Use default avatar
                AvatarBorder.Background = new SolidColorBrush(GetRoleColor(message.SenderRole));
            }
        }

        // Set username with role color
        UsernameText.Text = message.SenderUsername;
        UsernameText.Foreground = new SolidColorBrush(GetRoleColor(message.SenderRole));

        // Set timestamp
        TimestampText.Text = FormatTimestamp(message.Timestamp);

        // Set message content
        MessageText.Text = message.Content;

        // Style for system messages
        if (message.Type != MessageType.Text)
        {
            MessageText.FontStyle = FontStyles.Italic;
            MessageText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            AvatarBorder.Visibility = Visibility.Collapsed;
            UsernameText.Visibility = Visibility.Collapsed;
            RoleBadge.Visibility = Visibility.Collapsed;
            RankBadge.Visibility = Visibility.Collapsed;

            switch (message.Type)
            {
                case MessageType.Join:
                    MessageText.Text = $"➜ {message.Content}";
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(87, 242, 135));
                    break;
                case MessageType.Leave:
                    MessageText.Text = $"← {message.Content}";
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(237, 66, 69));
                    break;
                case MessageType.Announcement:
                    MessageText.FontWeight = FontWeights.Bold;
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(254, 231, 92));
                    break;
            }
            return;
        }

        // Show role badge for special roles
        if (message.SenderRole >= UserRole.VIP)
        {
            RoleBadge.Visibility = Visibility.Visible;
            RoleBadge.Background = new SolidColorBrush(GetRoleColor(message.SenderRole));
            RoleText.Text = message.SenderRole.ToString().ToUpper();
        }

        // Show rank badge
        if (message.SenderRank >= UserRank.Silver)
        {
            RankBadge.Visibility = Visibility.Visible;
            RankBadge.Background = new SolidColorBrush(GetRankColor(message.SenderRank));
            RankText.Text = GetRankEmoji(message.SenderRank) + " " + message.SenderRank.ToString();
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
            _ => Color.FromRgb(185, 187, 190)
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
            UserRank.Legend => "👑",
            UserRank.Elite => "🔥",
            UserRank.Diamond => "💎",
            UserRank.Platinum => "✨",
            UserRank.Gold => "🥇",
            UserRank.Silver => "🥈",
            UserRank.Bronze => "🥉",
            _ => "🌟"
        };
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var now = DateTime.Now;
        var local = timestamp.ToLocalTime();

        if (local.Date == now.Date)
            return $"Today at {local:h:mm tt}";

        if (local.Date == now.Date.AddDays(-1))
            return $"Yesterday at {local:h:mm tt}";

        return local.ToString("MM/dd/yyyy h:mm tt");
    }

    #region User Context Menu Handlers

    private void Avatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentMessage != null)
        {
            // Could open user profile popup
        }
    }

    private void Username_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentMessage != null)
        {
            // Could open user profile popup
        }
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToProfile();
        // In a full implementation, would pass user ID to load that profile
    }

    private async void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        if (friendService != null)
        {
            // Open DM with user
            var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
            navigationService?.NavigateToFriends();
        }
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        if (friendService != null)
        {
            try
            {
                await friendService.SendFriendRequestAsync(_currentMessage.SenderUsername);
                MessageBox.Show($"Friend request sent to {_currentMessage.SenderUsername}!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send friend request: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Mention_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        RaiseEvent(new RoutedEventArgs(MentionRequestedEvent, _currentMessage));
    }

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Implement user muting
        MessageBox.Show($"Muted {_currentMessage.SenderUsername}", "User Muted",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BlockUser_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to block {_currentMessage.SenderUsername}? You won't see their messages.",
            "Block User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Implement user blocking
            MessageBox.Show($"Blocked {_currentMessage.SenderUsername}", "User Blocked",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyUserId_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.SenderId);
    }

    #endregion

    #region Message Context Menu Handlers

    private void Reply_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        RaiseEvent(new RoutedEventArgs(ReplyRequestedEvent, _currentMessage));
    }

    private void React_Click(object sender, RoutedEventArgs e)
    {
        // Could open emoji picker
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.Content);
    }

    private void CopyMessageLink_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText($"yurtcord://message/{_currentMessage.Id}");
    }

    private void PinMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Implement message pinning
        MessageBox.Show("Message pinned!", "Pinned", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void EditMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Implement message editing - could show edit textbox inline
    }

    private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var result = MessageBox.Show(
            "Are you sure you want to delete this message?",
            "Delete Message",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var chatService = (IChatService?)App.ServiceProvider.GetService(typeof(IChatService));
            if (chatService != null)
            {
                await chatService.DeleteMessageAsync(_currentMessage.Id);
            }
        }
    }

    private void CopyMessageId_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.Id);
    }

    #endregion
}
