using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class NotificationCenterViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly Services.INavigationService _navigationService;
    private List<NotificationDto> _allNotifications = new();

    [ObservableProperty]
    private ObservableCollection<NotificationDto> _notifications = new();

    [ObservableProperty]
    private bool _hasNotifications = false;

    [ObservableProperty]
    private string _currentFilter = "All";

    public NotificationCenterViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load notifications
            // var notifications = await _apiService.GetNotificationsAsync();
            // _allNotifications = notifications;
            ApplyFilter(CurrentFilter);
            HasNotifications = Notifications.Any();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load notifications: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkAllRead()
    {
        try
        {
            // TODO: Call API to mark all as read
            // await _apiService.MarkAllNotificationsReadAsync();

            foreach (var notification in Notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to mark all as read: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        // Navigate to notification settings
        _navigationService.NavigateToSettings("notifications");
    }

    [RelayCommand]
    private void Filter(string filterType)
    {
        CurrentFilter = filterType;
        ApplyFilter(filterType);
    }

    private void ApplyFilter(string filterType)
    {
        Notifications.Clear();

        var filtered = filterType switch
        {
            "Unread" => _allNotifications.Where(n => !n.IsRead),
            "Friends" => _allNotifications.Where(n => n.Type == Shared.Models.NotificationType.FriendRequest),
            "Messages" => _allNotifications.Where(n => n.Type == Shared.Models.NotificationType.Message),
            "Mentions" => _allNotifications.Where(n => n.Type == Shared.Models.NotificationType.Mention),
            _ => _allNotifications
        };

        foreach (var notification in filtered)
            Notifications.Add(notification);
    }

    [RelayCommand]
    private async Task NotificationClick(NotificationDto notification)
    {
        // Mark as read
        if (!notification.IsRead)
        {
            try
            {
                // TODO: Call API to mark as read
                // await _apiService.MarkNotificationReadAsync(notification.Id);
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }
            catch
            {
                // Ignore errors
            }
        }

        // Navigate based on notification type
        if (!string.IsNullOrEmpty(notification.ActionUrl))
        {
            // Parse and navigate to the action URL
            // TODO: Implement navigation logic
        }
    }
}
