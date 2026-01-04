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

            List<UserActivityDto> activities;

            // Handle different filter types
            switch (CurrentFilter)
            {
                case "Following":
                    // Get activities from followed users
                    activities = await _apiService.GetFollowingActivityFeedAsync(_currentPage, PageSize);
                    break;

                case "Friends":
                    // Get activities from friends only
                    activities = await _apiService.GetActivityFeedAsync("friends", _currentPage, PageSize);
                    break;

                case "Purchases":
                    // Get purchase-related activities
                    activities = await _apiService.GetActivityFeedByTypeAsync(
                        VeaMarketplace.Shared.Models.ActivityType.ProductPurchased, _currentPage, PageSize);
                    break;

                case "Listings":
                    // Get product listing activities
                    activities = await _apiService.GetActivityFeedByTypeAsync(
                        VeaMarketplace.Shared.Models.ActivityType.ProductListed, _currentPage, PageSize);
                    break;

                case "Voice":
                    // Get voice-related activities (joined/left voice channels)
                    var voiceJoined = await _apiService.GetActivityFeedByTypeAsync(
                        VeaMarketplace.Shared.Models.ActivityType.VoiceJoined, _currentPage, PageSize);
                    var voiceLeft = await _apiService.GetActivityFeedByTypeAsync(
                        VeaMarketplace.Shared.Models.ActivityType.VoiceLeft, _currentPage, PageSize);
                    // Merge and sort by timestamp
                    activities = voiceJoined.Concat(voiceLeft)
                        .OrderByDescending(a => a.Timestamp)
                        .Take(PageSize)
                        .ToList();
                    break;

                case "All":
                default:
                    // Get all activities (optionally filtered to friends)
                    var filter = ShowFriendsOnly ? "friends" : null;
                    activities = await _apiService.GetActivityFeedAsync(filter, _currentPage, PageSize);
                    break;
            }

            if (activities.Count < PageSize)
            {
                HasMoreActivities = false;
            }

            // Apply client-side filtering for friends if needed
            if (ShowFriendsOnly && CurrentFilter != "Friends" && CurrentFilter != "Following")
            {
                var friendIds = await GetFriendIdsAsync();
                activities = activities.Where(a => friendIds.Contains(a.UserId)).ToList();
            }

            foreach (var activity in activities)
            {
                // Set icon based on activity type
                activity.Icon = GetActivityIcon(activity.Type);
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

    private async Task<HashSet<string>> GetFriendIdsAsync()
    {
        try
        {
            var friends = _friendService.Friends;
            return friends.Select(f => f.UserId).ToHashSet();
        }
        catch
        {
            return new HashSet<string>();
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
