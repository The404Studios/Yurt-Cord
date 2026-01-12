namespace VeaMarketplace.Mobile.Services;

public interface ISettingsService
{
    string? GetSavedToken();
    Task SaveTokenAsync(string token);
    Task ClearTokenAsync();

    // App Settings
    bool NotificationsEnabled { get; set; }
    bool DarkMode { get; set; }
    double MasterVolume { get; set; }
    string? ServerUrl { get; set; }
}

public class SettingsService : ISettingsService
{
    private const string TokenKey = "auth_token";
    private const string NotificationsKey = "notifications_enabled";
    private const string DarkModeKey = "dark_mode";
    private const string MasterVolumeKey = "master_volume";
    private const string ServerUrlKey = "server_url";

    public string? GetSavedToken()
    {
        return SecureStorage.Default.GetAsync(TokenKey).Result;
    }

    public async Task SaveTokenAsync(string token)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
    }

    public Task ClearTokenAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        return Task.CompletedTask;
    }

    public bool NotificationsEnabled
    {
        get => Preferences.Default.Get(NotificationsKey, true);
        set => Preferences.Default.Set(NotificationsKey, value);
    }

    public bool DarkMode
    {
        get => Preferences.Default.Get(DarkModeKey, true); // Default dark mode
        set => Preferences.Default.Set(DarkModeKey, value);
    }

    public double MasterVolume
    {
        get => Preferences.Default.Get(MasterVolumeKey, 0.8);
        set => Preferences.Default.Set(MasterVolumeKey, value);
    }

    public string? ServerUrl
    {
        get => Preferences.Default.Get<string?>(ServerUrlKey, null);
        set => Preferences.Default.Set(ServerUrlKey, value ?? string.Empty);
    }
}
