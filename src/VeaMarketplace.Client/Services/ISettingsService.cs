using System.IO;
using System.Text.Json;

namespace VeaMarketplace.Client.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void SaveSettings();
    void LoadSettings();
    T GetSetting<T>(string key, T defaultValue);
    void SetSetting<T>(string key, T value);
}

public class AppSettings
{
    public string? SavedToken { get; set; }
    public string? SavedUsername { get; set; }
    public bool RememberMe { get; set; }
    public string ServerUrl { get; set; } = "http://162.248.94.23:5000";
    public double Volume { get; set; } = 1.0;
    public double MicrophoneVolume { get; set; } = 1.0;
    public bool PushToTalk { get; set; }
    public string PushToTalkKey { get; set; } = "V";
    public bool ShowNotifications { get; set; } = true;
    public bool PlaySounds { get; set; } = true;

    // Audio Device Settings
    public string? InputDeviceId { get; set; }
    public string? OutputDeviceId { get; set; }
    public double VoiceActivityThreshold { get; set; } = 0.02;
    public bool NoiseSuppression { get; set; } = true;
    public bool EchoCancellation { get; set; } = true;

    // Extra settings stored by key
    public Dictionary<string, object> ExtraSettings { get; set; } = new();
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

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
        var json = JsonSerializer.Serialize(Settings, s_jsonOptions);
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

    public T GetSetting<T>(string key, T defaultValue)
    {
        if (Settings.ExtraSettings.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                }
                if (value is T typedValue)
                {
                    return typedValue;
                }
                // Try to convert
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public void SetSetting<T>(string key, T value)
    {
        Settings.ExtraSettings[key] = value!;
        SaveSettings();
    }
}
