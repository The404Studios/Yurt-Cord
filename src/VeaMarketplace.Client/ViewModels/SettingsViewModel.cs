using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IVoiceService _voiceService;

    // Store event handler for proper unsubscription
    private readonly Action<double> _onLocalAudioLevel;

    [ObservableProperty]
    private ObservableCollection<AudioDeviceItem> _inputDevices = new();

    [ObservableProperty]
    private ObservableCollection<AudioDeviceItem> _outputDevices = new();

    [ObservableProperty]
    private AudioDeviceItem? _selectedInputDevice;

    [ObservableProperty]
    private AudioDeviceItem? _selectedOutputDevice;

    [ObservableProperty]
    private double _volume;

    [ObservableProperty]
    private double _microphoneVolume;

    [ObservableProperty]
    private bool _pushToTalkEnabled;

    [ObservableProperty]
    private string _pushToTalkKeyDisplay = "V";

    [ObservableProperty]
    private Key _pushToTalkKey = Key.V;

    [ObservableProperty]
    private double _voiceActivityThreshold;

    [ObservableProperty]
    private bool _noiseSuppression;

    [ObservableProperty]
    private bool _echoCancellation;

    [ObservableProperty]
    private bool _isRecordingPttKey;

    [ObservableProperty]
    private double _testAudioLevel;

    [ObservableProperty]
    private bool _isTesting;

    // Privacy Settings
    [ObservableProperty]
    private bool _allowFriendRequests;

    [ObservableProperty]
    private bool _allowDirectMessages;

    [ObservableProperty]
    private bool _showOnlineStatus;

    [ObservableProperty]
    private bool _showActivityStatus;

    // Appearance Settings
    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _accentColor = "#00B4D8";

    [ObservableProperty]
    private double _fontScale = 1.0;

    [ObservableProperty]
    private bool _compactMode;

    [ObservableProperty]
    private bool _animationsEnabled = true;

    // Profile Settings
    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _bio = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _avatarUrl = "";

    // Notification Settings
    [ObservableProperty]
    private bool _desktopNotifications = true;

    [ObservableProperty]
    private bool _soundNotifications = true;

    [ObservableProperty]
    private bool _badgeNotifications = true;

    [ObservableProperty]
    private bool _mentionNotifications = true;

    [ObservableProperty]
    private bool _dmNotifications = true;

    [ObservableProperty]
    private string _notificationSound = "default";

    // Language & Translation Settings
    [ObservableProperty]
    private string _preferredLanguage = "en";

    [ObservableProperty]
    private bool _autoTranslate;

    [ObservableProperty]
    private List<LanguageOption> _availableLanguages = new();

    public SettingsViewModel(
        ISettingsService settingsService,
        IAudioDeviceService audioDeviceService,
        IVoiceService voiceService)
    {
        _settingsService = settingsService;
        _audioDeviceService = audioDeviceService;
        _voiceService = voiceService;

        LoadSettings();
        RefreshDevices();

        // Create and store event handler for proper cleanup
        _onLocalAudioLevel = level =>
        {
            TestAudioLevel = level;
        };

        // Subscribe to local audio level for testing
        _voiceService.OnLocalAudioLevel += _onLocalAudioLevel;
    }

    /// <summary>
    /// Unsubscribes from all events to prevent memory leaks
    /// </summary>
    public void Cleanup()
    {
        _voiceService.OnLocalAudioLevel -= _onLocalAudioLevel;
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;

        // Voice & Audio
        Volume = settings.Volume;
        MicrophoneVolume = settings.MicrophoneVolume;
        PushToTalkEnabled = settings.PushToTalk;
        VoiceActivityThreshold = settings.VoiceActivityThreshold;
        NoiseSuppression = settings.NoiseSuppression;
        EchoCancellation = settings.EchoCancellation;

        // Parse PTT key
        if (Enum.TryParse<Key>(settings.PushToTalkKey, out var key))
        {
            PushToTalkKey = key;
            PushToTalkKeyDisplay = key.ToString();
        }

        // Privacy Settings
        AllowFriendRequests = settings.AllowFriendRequests;
        AllowDirectMessages = settings.AllowDirectMessages;
        ShowOnlineStatus = settings.ShowOnlineStatus;
        ShowActivityStatus = settings.ShowActivityStatus;

        // Appearance Settings
        Theme = settings.Theme;
        AccentColor = settings.AccentColor;
        FontScale = settings.FontScale;
        CompactMode = settings.CompactMode;
        AnimationsEnabled = settings.AnimationsEnabled;

        // Notification Settings
        DesktopNotifications = settings.DesktopNotifications;
        SoundNotifications = settings.SoundNotifications;
        BadgeNotifications = settings.BadgeNotifications;
        MentionNotifications = settings.MentionNotifications;
        DmNotifications = settings.DMNotifications;
        NotificationSound = settings.NotificationSound;

        // Language Settings
        PreferredLanguage = settings.PreferredLanguage;
        AutoTranslate = settings.AutoTranslateMessages;
        LoadAvailableLanguages();

        // Apply to voice service
        _voiceService.PushToTalkEnabled = PushToTalkEnabled;
        _voiceService.PushToTalkKey = PushToTalkKey;
        _voiceService.SetVoiceActivityThreshold(VoiceActivityThreshold);
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        InputDevices.Clear();
        OutputDevices.Clear();

        var inputDevices = _audioDeviceService.GetInputDevices();
        foreach (var device in inputDevices)
        {
            var item = new AudioDeviceItem
            {
                Id = device.Id,
                Name = device.Name,
                DeviceNumber = device.DeviceNumber,
                IsDefault = device.IsDefault
            };
            InputDevices.Add(item);

            // Select saved or default device
            if (_settingsService.Settings.InputDeviceId == device.Id ||
                (string.IsNullOrEmpty(_settingsService.Settings.InputDeviceId) && device.IsDefault))
            {
                SelectedInputDevice = item;
            }
        }

        var outputDevices = _audioDeviceService.GetOutputDevices();
        foreach (var device in outputDevices)
        {
            var item = new AudioDeviceItem
            {
                Id = device.Id,
                Name = device.Name,
                DeviceNumber = device.DeviceNumber,
                IsDefault = device.IsDefault
            };
            OutputDevices.Add(item);

            // Select saved or default device
            if (_settingsService.Settings.OutputDeviceId == device.Id ||
                (string.IsNullOrEmpty(_settingsService.Settings.OutputDeviceId) && device.IsDefault))
            {
                SelectedOutputDevice = item;
            }
        }
    }

    partial void OnSelectedInputDeviceChanged(AudioDeviceItem? value)
    {
        if (value != null)
        {
            _settingsService.Settings.InputDeviceId = value.Id;
            _voiceService.SetInputDevice(value.DeviceNumber);
            SaveSettings();
        }
    }

    partial void OnSelectedOutputDeviceChanged(AudioDeviceItem? value)
    {
        if (value != null)
        {
            _settingsService.Settings.OutputDeviceId = value.Id;
            _voiceService.SetOutputDevice(value.DeviceNumber);
            SaveSettings();
        }
    }

    partial void OnVolumeChanged(double value)
    {
        _settingsService.Settings.Volume = value;
        SaveSettings();
    }

    partial void OnMicrophoneVolumeChanged(double value)
    {
        _settingsService.Settings.MicrophoneVolume = value;
        SaveSettings();
    }

    partial void OnPushToTalkEnabledChanged(bool value)
    {
        _settingsService.Settings.PushToTalk = value;
        _voiceService.PushToTalkEnabled = value;
        SaveSettings();
    }

    partial void OnVoiceActivityThresholdChanged(double value)
    {
        _settingsService.Settings.VoiceActivityThreshold = value;
        _voiceService.SetVoiceActivityThreshold(value);
        SaveSettings();
    }

    partial void OnNoiseSuppressionChanged(bool value)
    {
        _settingsService.Settings.NoiseSuppression = value;
        SaveSettings();
    }

    partial void OnEchoCancellationChanged(bool value)
    {
        _settingsService.Settings.EchoCancellation = value;
        SaveSettings();
    }

    // Privacy Settings Property Changed Handlers
    partial void OnAllowFriendRequestsChanged(bool value)
    {
        _settingsService.Settings.AllowFriendRequests = value;
        SaveSettings();
    }

    partial void OnAllowDirectMessagesChanged(bool value)
    {
        _settingsService.Settings.AllowDirectMessages = value;
        SaveSettings();
    }

    partial void OnShowOnlineStatusChanged(bool value)
    {
        _settingsService.Settings.ShowOnlineStatus = value;
        SaveSettings();
    }

    partial void OnShowActivityStatusChanged(bool value)
    {
        _settingsService.Settings.ShowActivityStatus = value;
        SaveSettings();
    }

    // Appearance Settings Property Changed Handlers
    partial void OnThemeChanged(string value)
    {
        _settingsService.Settings.Theme = value;
        SaveSettings();
    }

    partial void OnAccentColorChanged(string value)
    {
        _settingsService.Settings.AccentColor = value;
        SaveSettings();
    }

    partial void OnFontScaleChanged(double value)
    {
        _settingsService.Settings.FontScale = value;
        SaveSettings();
    }

    partial void OnCompactModeChanged(bool value)
    {
        _settingsService.Settings.CompactMode = value;
        SaveSettings();
    }

    partial void OnAnimationsEnabledChanged(bool value)
    {
        _settingsService.Settings.AnimationsEnabled = value;
        SaveSettings();
    }

    // Notification Settings Property Changed Handlers
    partial void OnDesktopNotificationsChanged(bool value)
    {
        _settingsService.Settings.DesktopNotifications = value;
        SaveSettings();
    }

    partial void OnSoundNotificationsChanged(bool value)
    {
        _settingsService.Settings.SoundNotifications = value;
        SaveSettings();
    }

    partial void OnBadgeNotificationsChanged(bool value)
    {
        _settingsService.Settings.BadgeNotifications = value;
        SaveSettings();
    }

    partial void OnMentionNotificationsChanged(bool value)
    {
        _settingsService.Settings.MentionNotifications = value;
        SaveSettings();
    }

    partial void OnDmNotificationsChanged(bool value)
    {
        _settingsService.Settings.DMNotifications = value;
        SaveSettings();
    }

    partial void OnNotificationSoundChanged(string value)
    {
        _settingsService.Settings.NotificationSound = value;
        SaveSettings();
    }

    // Language Settings Property Changed Handlers
    partial void OnPreferredLanguageChanged(string value)
    {
        _settingsService.Settings.PreferredLanguage = value;
        SaveSettings();
    }

    partial void OnAutoTranslateChanged(bool value)
    {
        _settingsService.Settings.AutoTranslateMessages = value;
        SaveSettings();
    }

    private void LoadAvailableLanguages()
    {
        AvailableLanguages = new List<LanguageOption>
        {
            new() { Code = "en", Name = "English", NativeName = "English" },
            new() { Code = "es", Name = "Spanish", NativeName = "Espanol" },
            new() { Code = "fr", Name = "French", NativeName = "Francais" },
            new() { Code = "de", Name = "German", NativeName = "Deutsch" },
            new() { Code = "it", Name = "Italian", NativeName = "Italiano" },
            new() { Code = "pt", Name = "Portuguese", NativeName = "Portugues" },
            new() { Code = "ru", Name = "Russian", NativeName = "Русский" },
            new() { Code = "zh", Name = "Chinese", NativeName = "中文" },
            new() { Code = "ja", Name = "Japanese", NativeName = "日本語" },
            new() { Code = "ko", Name = "Korean", NativeName = "한국어" },
            new() { Code = "ar", Name = "Arabic", NativeName = "العربية" },
            new() { Code = "hi", Name = "Hindi", NativeName = "हिन्दी" },
            new() { Code = "nl", Name = "Dutch", NativeName = "Nederlands" },
            new() { Code = "pl", Name = "Polish", NativeName = "Polski" },
            new() { Code = "tr", Name = "Turkish", NativeName = "Turkce" },
            new() { Code = "vi", Name = "Vietnamese", NativeName = "Tieng Viet" },
            new() { Code = "th", Name = "Thai", NativeName = "ไทย" },
            new() { Code = "sv", Name = "Swedish", NativeName = "Svenska" },
            new() { Code = "uk", Name = "Ukrainian", NativeName = "Українська" }
        };
    }

    [RelayCommand]
    private void StartRecordingPttKey()
    {
        IsRecordingPttKey = true;
        PushToTalkKeyDisplay = "Press a key...";
    }

    public void SetPushToTalkKey(Key key)
    {
        if (!IsRecordingPttKey) return;

        PushToTalkKey = key;
        PushToTalkKeyDisplay = key.ToString();
        _settingsService.Settings.PushToTalkKey = key.ToString();
        _voiceService.PushToTalkKey = key;
        IsRecordingPttKey = false;
        SaveSettings();
    }

    [RelayCommand]
    private void TestMicrophone()
    {
        IsTesting = !IsTesting;
        // Audio level events from VoiceService will update TestAudioLevel
    }

    private void SaveSettings()
    {
        _settingsService.SaveSettings();
    }
}

public class AudioDeviceItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DeviceNumber { get; set; }
    public bool IsDefault { get; set; }

    public override string ToString() => IsDefault ? $"{Name} (Default)" : Name;
}

public class LanguageOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;

    public override string ToString() => $"{Name} ({NativeName})";
}
