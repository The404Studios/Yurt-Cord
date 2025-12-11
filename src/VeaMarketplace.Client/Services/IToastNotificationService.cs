using System.Timers;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.Controls;

namespace VeaMarketplace.Client.Services;

public interface IToastNotificationService
{
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000);
    void ShowSuccess(string title, string message);
    void ShowError(string title, string message);
    void ShowWarning(string title, string message);
    void ShowFriendRequest(string fromUsername);
    void ShowMessage(string fromUsername, string preview);
    void ClearAll();
    void SetContainer(Panel container);
}

public class ToastNotificationService : IToastNotificationService
{
    private Panel? _container;
    private readonly List<NotificationToast> _activeNotifications = new();
    private const int MaxNotifications = 5;

    public void SetContainer(Panel container)
    {
        _container = container;
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_container == null) return;

            // Remove oldest if at max
            while (_activeNotifications.Count >= MaxNotifications)
            {
                var oldest = _activeNotifications[0];
                oldest.Close();
            }

            var toast = new NotificationToast
            {
                Title = title,
                Message = message
            };
            toast.SetNotificationType(type);
            toast.Closed += OnToastClosed;

            _activeNotifications.Add(toast);
            _container.Children.Add(toast);

            // Auto-dismiss
            if (durationMs > 0)
            {
                var timer = new System.Timers.Timer(durationMs);
                timer.Elapsed += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    Application.Current?.Dispatcher.Invoke(() => toast.Close());
                };
                timer.Start();
            }
        });
    }

    private void OnToastClosed(object? sender, EventArgs e)
    {
        if (sender is NotificationToast toast)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _activeNotifications.Remove(toast);
                _container?.Children.Remove(toast);
            });
        }
    }

    public void ShowSuccess(string title, string message)
        => ShowNotification(title, message, NotificationType.Success);

    public void ShowError(string title, string message)
        => ShowNotification(title, message, NotificationType.Error, 8000);

    public void ShowWarning(string title, string message)
        => ShowNotification(title, message, NotificationType.Warning, 6000);

    public void ShowFriendRequest(string fromUsername)
        => ShowNotification("Friend Request", $"{fromUsername} sent you a friend request!", NotificationType.FriendRequest);

    public void ShowMessage(string fromUsername, string preview)
        => ShowNotification(fromUsername, preview, NotificationType.Message);

    public void ClearAll()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var toast in _activeNotifications.ToList())
            {
                toast.Close();
            }
        });
    }
}
