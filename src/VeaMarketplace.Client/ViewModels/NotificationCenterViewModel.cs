using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

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

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private ObservableCollection<NotificationDto> _recentNotifications = new();

    public bool HasUnreadNotifications => UnreadCount > 0;

    public NotificationCenterViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        _ = SafeLoadNotificationsAsync();
    }

    private async Task SafeLoadNotificationsAsync()
    {
        try
        {
            await LoadNotificationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NotificationCenterViewModel: Load failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle incoming real-time notification from SignalR
    /// </summary>
    public void OnNewNotification(NotificationDto notification)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Add to beginning of lists
            _allNotifications.Insert(0, notification);

            // If matches current filter, add to visible notifications
            if (ShouldShowNotification(notification, CurrentFilter))
            {
                Notifications.Insert(0, notification);
            }

            // Update recent notifications (top 5)
            RecentNotifications.Insert(0, notification);
            while (RecentNotifications.Count > 5)
            {
                RecentNotifications.RemoveAt(RecentNotifications.Count - 1);
            }

            // Update counts
            if (!notification.IsRead)
            {
                UnreadCount++;
                OnPropertyChanged(nameof(HasUnreadNotifications));
            }
            HasNotifications = _allNotifications.Count > 0;
        });
    }

    /// <summary>
    /// Update unread count from SignalR
    /// </summary>
    public void UpdateUnreadCountFromSignalR(int count)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            UnreadCount = count;
            OnPropertyChanged(nameof(HasUnreadNotifications));
        });
    }

    /// <summary>
    /// Called by source generator when UnreadCount property changes
    /// </summary>
    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnreadNotifications));
    }

    private bool ShouldShowNotification(NotificationDto notification, string filter)
    {
        return filter switch
        {
            "Unread" => !notification.IsRead,
            "Friends" => notification.Type == NotificationType.FriendRequest,
            "Messages" => notification.Type == NotificationType.Message,
            "Mentions" => notification.Type == NotificationType.Mention,
            "Orders" => notification.Type == NotificationType.Order,
            "Reviews" => notification.Type == NotificationType.Review,
            _ => true
        };
    }

    private async Task LoadNotificationsAsync()
    {
        try
        {
            IsLoading = true;
            IsRefreshing = true;
            var notifications = await _apiService.GetNotificationsAsync();
            _allNotifications = notifications;
            ApplyFilter(CurrentFilter);
            HasNotifications = Notifications.Any();

            // Update unread count
            UnreadCount = notifications.Count(n => !n.IsRead);
            OnPropertyChanged(nameof(HasUnreadNotifications));

            // Update recent notifications (top 5)
            RecentNotifications.Clear();
            foreach (var notification in notifications.Take(5))
            {
                RecentNotifications.Add(notification);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load notifications: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
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

            // Update unread count
            UnreadCount = 0;
            OnPropertyChanged(nameof(HasUnreadNotifications));

            // Re-apply filter in case we're viewing unread only
            if (CurrentFilter == "Unread")
            {
                ApplyFilter(CurrentFilter);
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

        var filtered = _allNotifications.Where(n => ShouldShowNotification(n, filterType));

        foreach (var notification in filtered)
            Notifications.Add(notification);

        HasNotifications = Notifications.Any();
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
