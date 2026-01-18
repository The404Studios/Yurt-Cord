using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Client.Helpers;

namespace VeaMarketplace.Client.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void SaveSettings();
    void LoadSettings();
    T GetSetting<T>(string key, T defaultValue);
    void SetSetting<T>(string key, T value);
}

#region QoL Feature Models

/// <summary>
/// Message template for quick responses - a feature Discord doesn't have
/// </summary>
public class MessageTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Shortcut { get; set; } // e.g., "/afk" triggers this template
    public string Category { get; set; } = "General";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UseCount { get; set; }
}

/// <summary>
/// Scheduled message to be sent at a specific time - Discord doesn't have this
/// </summary>
public class ScheduledMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string? TargetChannelId { get; set; }
    public string? TargetUserId { get; set; } // For DMs
    public DateTime ScheduledTime { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
}

public enum RecurrenceType
{
    None,
    Daily,
    Weekly,
    Weekdays,
    Monthly
}

/// <summary>
/// Scheduled status change - another QoL feature Discord doesn't offer
/// </summary>
public class ScheduledStatus
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Status { get; set; } = string.Empty; // Online, Away, DND, Invisible
    public string? CustomStatus { get; set; }
    public string? CustomEmoji { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; } // When to revert
    public string? RevertToStatus { get; set; }
    public bool IsActive { get; set; }
    public List<DayOfWeek> ActiveDays { get; set; } = [];
}

/// <summary>
/// Private note about a friend - Discord doesn't have this
/// </summary>
public class FriendNote
{
    public string UserId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? Nickname { get; set; } // Custom nickname
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = []; // Custom tags like "work", "gaming"
    public DateTime? Birthday { get; set; }
    public DateTime? FriendshipDate { get; set; } // When you became friends
    public string? Timezone { get; set; }
}

/// <summary>
/// Smart DND schedule - advanced version of Discord's basic DND
/// </summary>
public class SmartDndSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public List<DayOfWeek> ActiveDays { get; set; } = [];
    public bool AllowUrgentMessages { get; set; } // Allow messages with @urgent
    public List<string> WhitelistedUserIds { get; set; } = []; // These users can still notify
    public bool AutoReplyEnabled { get; set; }
    public string? AutoReplyMessage { get; set; }
}

/// <summary>
/// Activity insight for tracking social patterns
/// </summary>
public class ActivityInsight
{
    public DateTime Date { get; set; }
    public int MessagesSent { get; set; }
    public int VoiceMinutes { get; set; }
    public Dictionary<string, int> ChannelActivity { get; set; } = new();
    public int FriendsInteractedWith { get; set; }
}

/// <summary>
/// Quick action shortcut configuration
/// </summary>
public class QuickAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // "status", "template", "navigation"
    public string ActionValue { get; set; } = string.Empty;
    public string? Hotkey { get; set; }
    public int Order { get; set; }
}

#endregion

public class AppSettings
{
    public string? SavedToken { get; set; }
    public string? SavedUsername { get; set; }
    public bool RememberMe { get; set; }
    public string ServerUrl { get; set; } = AppConstants.DefaultServerUrl;
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

    #region QoL Features Settings (Discord doesn't have these)

    // Message Templates - quick responses and snippets
    public List<MessageTemplate> MessageTemplates { get; set; } = [];

    // Scheduled Messages - send messages at specific times
    public List<ScheduledMessage> ScheduledMessages { get; set; } = [];

    // Status Scheduler - auto-change status at specific times
    public List<ScheduledStatus> ScheduledStatuses { get; set; } = [];

    // Friend Notes - private notes about friends
    public List<FriendNote> FriendNotes { get; set; } = [];

    // Smart DND Schedules - advanced do-not-disturb
    public List<SmartDndSchedule> DndSchedules { get; set; } = [];

    // Activity Insights - track your usage patterns
    public List<ActivityInsight> ActivityInsights { get; set; } = [];

    // Quick Actions - custom shortcuts
    public List<QuickAction> QuickActions { get; set; } = [];

    // Friendship Dates - track when friendships started
    public Dictionary<string, DateTime> FriendshipDates { get; set; } = new();

    // Online Notifications - per-friend notification settings
    public List<OnlineNotificationPreference> OnlineNotifications { get; set; } = [];

    // Read Receipts (optional) - know when messages are read
    public bool EnableReadReceipts { get; set; } = true;
    public bool ShowReadReceipts { get; set; } = true;

    // Message History Search
    public bool SaveMessageHistory { get; set; } = true;
    public int MessageHistoryDays { get; set; } = 90;

    // Auto-Away Settings
    public bool EnableAutoAway { get; set; } = true;
    public int AutoAwayMinutes { get; set; } = 10;

    // Typing Indicator Preferences
    public bool ShowTypingIndicator { get; set; } = true;
    public bool SendTypingIndicator { get; set; } = true;

    // Link Preview Settings
    public bool ShowLinkPreviews { get; set; } = true;
    public bool EmbedYouTube { get; set; } = true;
    public bool EmbedTwitter { get; set; } = true;
    public bool EmbedImages { get; set; } = true;

    // Marketplace Preferences
    public bool ShowMarketplaceNotifications { get; set; } = true;
    public bool AutoRefreshListings { get; set; } = true;
    public int ListingRefreshIntervalMinutes { get; set; } = 5;
    public List<string> WatchedCategories { get; set; } = [];
    public decimal? PriceAlertThreshold { get; set; }

    // Privacy Settings
    public bool AllowFriendRequests { get; set; } = true;
    public bool AllowDirectMessages { get; set; } = true;
    public bool ShowOnlineStatus { get; set; } = true;
    public bool ShowActivityStatus { get; set; } = true;

    // Appearance Settings
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#00B4D8"; // Yurt Cord teal
    public double FontScale { get; set; } = 1.0;
    public bool CompactMode { get; set; } = false;
    public bool AnimationsEnabled { get; set; } = true;

    // Notification Settings
    public bool DesktopNotifications { get; set; } = true;
    public bool SoundNotifications { get; set; } = true;
    public bool BadgeNotifications { get; set; } = true;
    public bool MentionNotifications { get; set; } = true;
    public bool DMNotifications { get; set; } = true;
    public string NotificationSound { get; set; } = "default";

    // Language & Translation Settings
    public string PreferredLanguage { get; set; } = "en";
    public bool AutoTranslateMessages { get; set; } = false;

    // Keybindings
    public Dictionary<string, string> Keybindings { get; set; } = new()
    {
        { "ToggleMute", "Ctrl+M" },
        { "ToggleDeafen", "Ctrl+D" },
        { "PushToTalk", "V" },
        { "QuickSearch", "Ctrl+K" },
        { "ToggleDevMode", "Ctrl+Shift+I" }
    };

    #endregion

    // Extra settings stored by key (for extensibility)
    public Dictionary<string, object> ExtraSettings { get; set; } = new();
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        // Use XDG-compliant config directory for settings
        var appFolder = XdgDirectories.ConfigHome;
        if (!XdgDirectories.EnsureDirectoryExists(appFolder))
        {
            Debug.WriteLine($"Warning: Could not create settings directory: {appFolder}");
        }
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
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to parse settings file: {ex.Message}");
                Settings = new AppSettings();
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to read settings file: {ex.Message}");
                Settings = new AppSettings();
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Permission denied reading settings file: {ex.Message}");
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
