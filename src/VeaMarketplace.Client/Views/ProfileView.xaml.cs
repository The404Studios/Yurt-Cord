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
    private readonly ProfileViewModel _viewModel;
    private readonly IApiService _apiService;

    public ProfileView()
    {
        InitializeComponent();

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
        var user = _viewModel.User ?? _apiService.CurrentUser;
        if (user == null) return;

        UsernameText.Text = user.Username;
        BioText.Text = string.IsNullOrEmpty(user.Bio) ? "No bio yet..." : user.Bio;
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

        // Role Badge
        RoleBadge.Background = new SolidColorBrush(GetRoleColor(user.Role));
        RoleBadgeText.Text = user.Role.ToString().ToUpper();

        // Rank Badge
        RankBadge.Background = new SolidColorBrush(GetRankColor(user.Rank));
        RankBadgeText.Text = $"{GetRankEmoji(user.Rank)} {user.Rank}";
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        // Show edit profile dialog
    }

    private Color GetRoleColor(UserRole role)
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

    private Color GetRankColor(UserRank rank)
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

    private string GetRankEmoji(UserRank rank)
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
