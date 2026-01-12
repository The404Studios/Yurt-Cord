namespace VeaMarketplace.Mobile.Services;

public interface INotificationService
{
    Task ShowToastAsync(string message);
    Task ShowAlertAsync(string title, string message);
    Task<bool> ShowConfirmAsync(string title, string message);
    Task ShowSnackbarAsync(string message, string? actionText = null, Action? action = null);
}

public class NotificationService : INotificationService
{
    public async Task ShowToastAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Using CommunityToolkit.Maui Toast
            var toast = CommunityToolkit.Maui.Alerts.Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Short);
            await toast.Show();
        });
    }

    public async Task ShowAlertAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        });
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.MainPage != null)
            {
                return await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
            }
            return false;
        });
    }

    public async Task ShowSnackbarAsync(string message, string? actionText = null, Action? action = null)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var snackbar = CommunityToolkit.Maui.Alerts.Snackbar.Make(
                message,
                action,
                actionText ?? string.Empty,
                TimeSpan.FromSeconds(3));
            await snackbar.Show();
        });
    }
}
