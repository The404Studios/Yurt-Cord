using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private bool _darkMode;

    [ObservableProperty]
    private double _masterVolume;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAuthService authService,
        INavigationService navigationService,
        INotificationService notificationService)
    {
        _settingsService = settingsService;
        _authService = authService;
        _navigationService = navigationService;
        _notificationService = notificationService;

        LoadSettings();
    }

    private void LoadSettings()
    {
        NotificationsEnabled = _settingsService.NotificationsEnabled;
        DarkMode = _settingsService.DarkMode;
        MasterVolume = _settingsService.MasterVolume;
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        _settingsService.NotificationsEnabled = value;
    }

    partial void OnDarkModeChanged(bool value)
    {
        _settingsService.DarkMode = value;
    }

    partial void OnMasterVolumeChanged(double value)
    {
        _settingsService.MasterVolume = value;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await _notificationService.ShowConfirmAsync(
            "DISCONNECT",
            "Are you sure you want to log out?");

        if (confirm)
        {
            await _authService.LogoutAsync();
            await _navigationService.NavigateToLoginAsync();
            await _notificationService.ShowToastAsync("SESSION TERMINATED");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        // Clear app cache
        await _notificationService.ShowToastAsync("Cache cleared");
    }
}
