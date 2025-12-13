using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ActivityFeedViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly IFriendService _friendService;

    [ObservableProperty]
    private ObservableCollection<UserActivityDto> _activities = [];

    [ObservableProperty]
    private string _currentFilter = "All";

    [ObservableProperty]
    private bool _showFriendsOnly = true;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _hasMoreActivities = true;

    private int _currentPage = 1;
    private const int PageSize = 20;

    public bool IsEmpty => !IsLoading && Activities.Count == 0;

    public ActivityFeedViewModel(IApiService apiService, INavigationService navigationService, IFriendService friendService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        _friendService = friendService;
        _ = LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync(bool refresh = false)
    {
        if (refresh)
        {
            _currentPage = 1;
            Activities.Clear();
            HasMoreActivities = true;
        }

        if (!HasMoreActivities && !refresh) return;

        try
        {
            IsLoading = Activities.Count == 0;
            IsRefreshing = refresh;
            ErrorMessage = null;

            var filter = ShowFriendsOnly ? "friends" : null;
            var activities = await _apiService.GetActivityFeedAsync(filter, _currentPage, PageSize);

            if (activities.Count < PageSize)
            {
                HasMoreActivities = false;
            }

            foreach (var activity in activities)
            {
                Activities.Add(activity);
            }

            _currentPage++;
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load activities: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshActivities()
    {
        await LoadActivitiesAsync(refresh: true);
    }

    [RelayCommand]
    private async Task LoadMoreActivities()
    {
        if (!HasMoreActivities || IsLoading) return;
        await LoadActivitiesAsync();
    }

    [RelayCommand]
    private async Task ToggleFriendsOnly()
    {
        ShowFriendsOnly = !ShowFriendsOnly;
        await LoadActivitiesAsync(refresh: true);
    }

    [RelayCommand]
    private async Task FilterByType(string filterType)
    {
        CurrentFilter = filterType;
        await LoadActivitiesAsync(refresh: true);
    }

    [RelayCommand]
    private void ViewUserProfile(string userId)
    {
        _navigationService.NavigateToProfile(userId);
    }

    [RelayCommand]
    private void ViewProduct(string productId)
    {
        _navigationService.NavigateToProduct(productId);
    }

    public string GetActivityIcon(VeaMarketplace.Shared.Models.ActivityType type)
    {
        return type switch
        {
            VeaMarketplace.Shared.Models.ActivityType.MessageSent => "💬",
            VeaMarketplace.Shared.Models.ActivityType.VoiceJoined => "🎤",
            VeaMarketplace.Shared.Models.ActivityType.VoiceLeft => "👋",
            VeaMarketplace.Shared.Models.ActivityType.ProductListed => "📦",
            VeaMarketplace.Shared.Models.ActivityType.ProductPurchased => "🛒",
            VeaMarketplace.Shared.Models.ActivityType.FriendAdded => "🤝",
            VeaMarketplace.Shared.Models.ActivityType.ProfileUpdated => "✏️",
            VeaMarketplace.Shared.Models.ActivityType.StatusChanged => "🔄",
            VeaMarketplace.Shared.Models.ActivityType.ScreenShareStarted => "🖥️",
            VeaMarketplace.Shared.Models.ActivityType.ScreenShareStopped => "🔲",
            _ => "📝"
        };
    }

    public string GetTimeAgo(DateTime timestamp)
    {
        var timeSpan = DateTime.UtcNow - timestamp;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";

        return timestamp.ToString("MMM d");
    }
}
