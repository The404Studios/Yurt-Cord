using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class FriendsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private ObservableCollection<FriendDto> _friends = new();

    [ObservableProperty]
    private ObservableCollection<FriendDto> _onlineFriends = new();

    [ObservableProperty]
    private ObservableCollection<FriendRequestDto> _pendingRequests = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public FriendsViewModel(IApiService apiService, INotificationService notificationService)
    {
        _apiService = apiService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    private async Task LoadFriendsAsync()
    {
        IsLoading = true;
        try
        {
            var friends = await _apiService.GetFriendsAsync();
            Friends.Clear();
            OnlineFriends.Clear();

            foreach (var friend in friends)
            {
                Friends.Add(friend);
                if (friend.IsOnline)
                {
                    OnlineFriends.Add(friend);
                }
            }

            var requests = await _apiService.GetFriendRequestsAsync();
            PendingRequests.Clear();
            foreach (var request in requests)
            {
                PendingRequests.Add(request);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcceptRequestAsync(FriendRequestDto request)
    {
        if (await _apiService.AcceptFriendRequestAsync(request.Id))
        {
            PendingRequests.Remove(request);
            await _notificationService.ShowToastAsync($"Now friends with {request.RequesterUsername}");
            await LoadFriendsAsync();
        }
    }

    [RelayCommand]
    private async Task DeclineRequestAsync(FriendRequestDto request)
    {
        if (await _apiService.DeclineFriendRequestAsync(request.Id))
        {
            PendingRequests.Remove(request);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadFriendsAsync();
        IsRefreshing = false;
    }
}
