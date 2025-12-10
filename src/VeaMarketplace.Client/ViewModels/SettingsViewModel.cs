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

        // Subscribe to local audio level for testing
        _voiceService.OnLocalAudioLevel += level =>
        {
            TestAudioLevel = level;
        };
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
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
