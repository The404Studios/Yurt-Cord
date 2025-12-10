using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class ProfileView : UserControl
{
    private readonly ProfileViewModel? _viewModel;
    private readonly IApiService? _apiService;

    public ProfileView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ProfileViewModel)App.ServiceProvider.GetService(typeof(ProfileViewModel))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;

        DataContext = _viewModel;
        Loaded += (s, e) => UpdateUI();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProfileViewModel.User))
            {
                Dispatcher.Invoke(UpdateUI);
            }
        };
    }

    private void UpdateUI()
    {
        var user = _viewModel?.User ?? _apiService?.CurrentUser;
        if (user == null) return;

        UsernameText.Text = user.Username;
        BioText.Text = string.IsNullOrEmpty(user.Bio) ? "No bio yet..." : user.Bio;
        DescriptionText.Text = string.IsNullOrEmpty(user.Description) ? "This user hasn't written anything about themselves yet." : user.Description;
        JoinedText.Text = $"Joined {user.CreatedAt:MMMM yyyy}";
        BalanceText.Text = $"${user.Balance:F2}";
        ReputationText.Text = user.Reputation.ToString();
        SalesText.Text = user.TotalSales.ToString();
        PurchasesText.Text = user.TotalPurchases.ToString();

        // Avatar
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try
            {
                AvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
            }
            catch { }
        }

        // Banner
        if (!string.IsNullOrEmpty(user.BannerUrl))
        {
            try
            {
                BannerBrush.ImageSource = new BitmapImage(new Uri(user.BannerUrl));
            }
            catch { }
        }

        // Role Badge
        RoleBadge.Background = new SolidColorBrush(GetRoleColor(user.Role));
        RoleBadgeText.Text = user.Role.ToString().ToUpper();

        // Rank Badge
        RankBadge.Background = new SolidColorBrush(GetRankColor(user.Rank));
        RankBadgeText.Text = $"{GetRankEmoji(user.Rank)} {user.Rank}";

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
}
