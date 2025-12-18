using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.ViewModels;

public partial class LeaderboardViewModel : BaseViewModel
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly IFriendService _friendService;

    [ObservableProperty]
    private string _currentCategory = "TopSellers";

    [ObservableProperty]
    private ObservableCollection<LeaderboardEntry> _currentLeaderboard = new();

    [ObservableProperty]
    private UserStats? _currentUserStats;

    [ObservableProperty]
    private bool _isLoading;

    public string CurrentCategoryName => CurrentCategory switch
    {
        "TopSellers" => "Top Sellers",
        "TopMessagers" => "Most Messages",
        "TopBuyers" => "Top Buyers",
        "MostActive" => "Most Active",
        "TopRated" => "Top Rated Sellers",
        "WeeklyStars" => "Weekly Stars",
        _ => "Leaderboard"
    };

    public string CurrentCategoryIcon => CurrentCategory switch
    {
        "TopSellers" => "🏆",
        "TopMessagers" => "💬",
        "TopBuyers" => "🛒",
        "MostActive" => "⚡",
        "TopRated" => "⭐",
        "WeeklyStars" => "🌟",
        _ => "🏆"
    };

    public bool IsEmpty => !IsLoading && CurrentLeaderboard.Count == 0;
    public bool HasNoAchievements => CurrentUserStats?.Achievements?.Count == 0;

    public LeaderboardViewModel(ILeaderboardService leaderboardService, IFriendService friendService)
    {
        _leaderboardService = leaderboardService;
        _friendService = friendService;

        // Subscribe to events
        _leaderboardService.OnStatsUpdated += stats =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentUserStats = stats;
                OnPropertyChanged(nameof(HasNoAchievements));
            });
        };

        _leaderboardService.OnLeaderboardUpdated += entry =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(CurrentLeaderboard));
                OnPropertyChanged(nameof(IsEmpty));
            });
        };

        // Load initial data
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await _leaderboardService.RefreshLeaderboardsAsync();
            CurrentUserStats = _leaderboardService.CurrentUserStats;
            await SwitchCategoryAsync(CurrentCategory);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SwitchCategoryAsync(string category)
    {
        CurrentCategory = category;
        OnPropertyChanged(nameof(CurrentCategoryName));
        OnPropertyChanged(nameof(CurrentCategoryIcon));

        IsLoading = true;
        try
        {
            var entries = category switch
            {
                "TopSellers" => await _leaderboardService.GetTopSellersAsync(),
                "TopMessagers" => await _leaderboardService.GetTopMessagersAsync(),
                "TopBuyers" => await _leaderboardService.GetTopBuyersAsync(),
                "MostActive" => await _leaderboardService.GetMostActiveUsersAsync(),
                "TopRated" => await _leaderboardService.GetTopRatedSellersAsync(),
                "WeeklyStars" => await _leaderboardService.GetWeeklyStarsAsync(),
                _ => await _leaderboardService.GetTopSellersAsync()
            };

            CurrentLeaderboard.Clear();
            foreach (var entry in entries)
            {
                CurrentLeaderboard.Add(entry);
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    partial void OnCurrentCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentCategoryName));
        OnPropertyChanged(nameof(CurrentCategoryIcon));
    }
}
