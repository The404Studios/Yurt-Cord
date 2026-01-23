using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.ViewModels;

public partial class LeaderboardViewModel : BaseViewModel
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly IFriendService _friendService;

    // Store event handlers for proper unsubscription
    private readonly Action<UserStats> _onStatsUpdated;
    private readonly Action<LeaderboardEntry> _onLeaderboardUpdated;

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

        // Create and store event handlers for proper cleanup
        _onStatsUpdated = stats =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentUserStats = stats;
                OnPropertyChanged(nameof(HasNoAchievements));
            });
        };

        _onLeaderboardUpdated = entry =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(CurrentLeaderboard));
                OnPropertyChanged(nameof(IsEmpty));
            });
        };

        // Subscribe to events
        _leaderboardService.OnStatsUpdated += _onStatsUpdated;
        _leaderboardService.OnLeaderboardUpdated += _onLeaderboardUpdated;
        // Don't auto-load - call LoadDataAsync() explicitly after authentication
    }

    /// <summary>
    /// Loads leaderboard data. Call this after user is authenticated.
    /// </summary>
    public Task LoadDataAsync() => SafeLoadAsync();

    private async Task SafeLoadAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LeaderboardViewModel: Load failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Unsubscribes from all events to prevent memory leaks
    /// </summary>
    public void Cleanup()
    {
        _leaderboardService.OnStatsUpdated -= _onStatsUpdated;
        _leaderboardService.OnLeaderboardUpdated -= _onLeaderboardUpdated;
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
