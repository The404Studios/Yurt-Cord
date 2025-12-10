using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

public partial class ChatMessageControl : UserControl
{
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
            UpdateUI(message);
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
}
