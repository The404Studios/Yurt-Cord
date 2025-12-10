using System.IO;
using System.Text.Json;

namespace VeaMarketplace.Client.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void SaveSettings();
    void LoadSettings();
}

public class AppSettings
{
    public string? SavedToken { get; set; }
    public string? SavedUsername { get; set; }
    public bool RememberMe { get; set; }
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public double Volume { get; set; } = 1.0;
    public double MicrophoneVolume { get; set; } = 1.0;
    public bool PushToTalk { get; set; }
    public string PushToTalkKey { get; set; } = "V";
    public bool ShowNotifications { get; set; } = true;
    public bool PlaySounds { get; set; } = true;
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "YurtCord");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");

        LoadSettings();
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                Settings = new AppSettings();
            }
        }
    }
}
