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
        _ = LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        try
        {
            IsLoading = true;
            var notifications = await _apiService.GetNotificationsAsync();
            _allNotifications = notifications;
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
            await _apiService.MarkAllNotificationsReadAsync();

            foreach (var notification in Notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }
            foreach (var notification in _allNotifications)
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
                await _apiService.MarkNotificationReadAsync(notification.Id);
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }
            catch
            {
                // Ignore errors
            }
        }

        // Navigate based on notification type and action URL
        if (!string.IsNullOrEmpty(notification.ActionUrl))
        {
            NavigateToAction(notification.ActionUrl);
        }
        else
        {
            // Navigate based on notification type
            switch (notification.Type)
            {
                case Shared.Models.NotificationType.FriendRequest:
                    _navigationService.NavigateToFriends();
                    break;
                case Shared.Models.NotificationType.Message:
                    _navigationService.NavigateToChat();
                    break;
                case Shared.Models.NotificationType.Order:
                    _navigationService.NavigateToOrders();
                    break;
                default:
                    // Stay on notifications page
                    break;
            }
        }
    }

    private void NavigateToAction(string actionUrl)
    {
        // Parse action URL format: "type/id" (e.g., "product/123", "order/456", "user/789")
        var parts = actionUrl.Split('/');
        if (parts.Length < 2) return;

        switch (parts[0].ToLower())
        {
            case "product":
                _navigationService.NavigateToProduct(parts[1]);
                break;
            case "order":
                _navigationService.NavigateToOrder(parts[1]);
                break;
            case "user":
                _navigationService.NavigateToProfile(parts[1]);
                break;
            case "chat":
                _navigationService.NavigateToChat(parts[1]);
                break;
            case "friends":
                _navigationService.NavigateToFriends();
                break;
            default:
                break;
        }
    }

    [RelayCommand]
    private async Task DeleteNotification(NotificationDto notification)
    {
        try
        {
            if (await _apiService.DeleteNotificationAsync(notification.Id))
            {
                _allNotifications.Remove(notification);
                Notifications.Remove(notification);
                HasNotifications = Notifications.Any();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete notification: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshNotifications()
    {
        await LoadNotificationsAsync();
    }
}
