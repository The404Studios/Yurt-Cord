using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.Wave;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Controls;

public partial class VoiceSettingsPanel : UserControl
{
    private readonly IVoiceService? _voiceService;
    private WaveInEvent? _testWaveIn;
    private DispatcherTimer? _levelTimer;
    private float _currentLevel;
    private bool _isCapturingKeybind;

    public VoiceSettingsPanel()
    {
        InitializeComponent();

        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            _voiceService = (IVoiceService?)App.ServiceProvider?.GetService(typeof(IVoiceService));
            LoadDevices();
            StartLevelMonitoring();
        }
    }

    private void LoadDevices()
    {
        // Load input devices
        var inputDevices = new List<WaveInCapabilities>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            inputDevices.Add(WaveInEvent.GetCapabilities(i));
        }
        InputDeviceCombo.ItemsSource = inputDevices;
        if (inputDevices.Count > 0)
            InputDeviceCombo.SelectedIndex = 0;

        // Load output devices
        var outputDevices = new List<WaveOutCapabilities>();
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            outputDevices.Add(WaveOut.GetCapabilities(i));
        }
        OutputDeviceCombo.ItemsSource = outputDevices;
        if (outputDevices.Count > 0)
            OutputDeviceCombo.SelectedIndex = 0;
    }

    private void StartLevelMonitoring()
    {
        _levelTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _levelTimer.Tick += (s, e) =>
        {
            // Smooth the level display
            var targetWidth = _currentLevel * (InputLevelBar.Parent is FrameworkElement parent
                ? parent.ActualWidth
                : 200);
            InputLevelBar.Width = Math.Max(0, Math.Min(targetWidth, InputLevelBar.Width * 0.8 + targetWidth * 0.2));
        };
        _levelTimer.Start();
    }

    private void InputDevice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (InputDeviceCombo.SelectedIndex >= 0 && _voiceService != null)
        {
            _voiceService.SetInputDevice(InputDeviceCombo.SelectedIndex);
            System.Diagnostics.Debug.WriteLine($"Input device changed to: {InputDeviceCombo.SelectedIndex}");
        }
    }

    private void OutputDevice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo.SelectedIndex >= 0 && _voiceService != null)
        {
            _voiceService.SetOutputDevice(OutputDeviceCombo.SelectedIndex);
            System.Diagnostics.Debug.WriteLine($"Output device changed to: {OutputDeviceCombo.SelectedIndex}");
        }
    }

    private void InputVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Input volume is handled at capture time via mic gain in VoiceService
        // This slider provides visual feedback - actual gain is fixed for now
        System.Diagnostics.Debug.WriteLine($"Input volume: {e.NewValue}%");
    }

    private void OutputVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_voiceService != null)
        {
            // Convert percentage (0-100) to volume multiplier (0.0-2.0)
            _voiceService.MasterVolume = (float)(e.NewValue / 50.0);
        }
        System.Diagnostics.Debug.WriteLine($"Output volume: {e.NewValue}%");
    }

    private void InputMode_Changed(object sender, RoutedEventArgs e)
    {
        if (PushToTalkRadio?.IsChecked == true)
        {
            PushToTalkSettings.Visibility = Visibility.Visible;
            VoiceActivitySettings.Visibility = Visibility.Collapsed;
        }
        else
        {
            PushToTalkSettings.Visibility = Visibility.Collapsed;
            VoiceActivitySettings.Visibility = Visibility.Visible;
        }
    }

    private void SetKeybind_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingKeybind = true;
        KeybindButton.Content = "Press a key...";
        KeybindButton.Focus();
        KeybindButton.PreviewKeyDown += CaptureKeybind;
    }

    private void CaptureKeybind(object sender, KeyEventArgs e)
    {
        if (!_isCapturingKeybind) return;

        _isCapturingKeybind = false;
        KeybindButton.PreviewKeyDown -= CaptureKeybind;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        KeybindButton.Content = key.ToString();

        // Save the keybind to voice service
        if (_voiceService != null)
        {
            _voiceService.PushToTalkKey = key;
        }
        System.Diagnostics.Debug.WriteLine($"Push to talk key set to: {key}");

        e.Handled = true;
    }

    private void Sensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_voiceService != null)
        {
            // Convert dB slider (-60 to 0) to threshold (0.001 to 0.5)
            // -60dB = very sensitive (0.001), 0dB = least sensitive (0.5)
            var threshold = Math.Pow(10, e.NewValue / 20.0) * 0.5;
            _voiceService.SetVoiceActivityThreshold(threshold);
        }
        System.Diagnostics.Debug.WriteLine($"Sensitivity: {e.NewValue} dB");
    }

    private void EchoCancellation_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = EchoCancellationCheck?.IsChecked ?? false;
        // Echo cancellation is handled by the audio processing pipeline
        // This setting would be saved to user preferences for future implementation
        System.Diagnostics.Debug.WriteLine($"Echo cancellation: {enabled}");
    }

    private void NoiseSuppression_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = NoiseSuppressionCheck?.IsChecked ?? false;
        // Noise suppression is handled by the audio processing pipeline
        // This setting would be saved to user preferences for future implementation
        System.Diagnostics.Debug.WriteLine($"Noise suppression: {enabled}");
    }

    private void AutoGain_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = AutoGainCheck?.IsChecked ?? false;
        // Auto gain control is handled by the audio processing pipeline
        // This setting would be saved to user preferences for future implementation
        System.Diagnostics.Debug.WriteLine($"Auto gain control: {enabled}");
    }

    private async void TestMic_Click(object sender, RoutedEventArgs e)
    {
        if (_testWaveIn != null)
        {
            // Stop test
            _testWaveIn.StopRecording();
            _testWaveIn.Dispose();
            _testWaveIn = null;
            TestMicButton.Content = "Let's Check";
            return;
        }

        try
        {
            TestMicButton.Content = "Stop Test";

            _testWaveIn = new WaveInEvent
            {
                DeviceNumber = InputDeviceCombo.SelectedIndex >= 0 ? InputDeviceCombo.SelectedIndex : 0,
                WaveFormat = new WaveFormat(48000, 16, 1),
                BufferMilliseconds = 20
            };

            _testWaveIn.DataAvailable += (s, args) =>
            {
                // Calculate RMS level
                float max = 0;
                var buffer = new WaveBuffer(args.Buffer);
                for (int i = 0; i < args.BytesRecorded / 2; i++)
                {
                    var sample = Math.Abs(buffer.ShortBuffer[i] / 32768f);
                    if (sample > max) max = sample;
                }
                _currentLevel = max;
            };

            _testWaveIn.StartRecording();

            // Auto stop after 10 seconds
            await Task.Delay(10000);
            if (_testWaveIn != null)
            {
                Dispatcher.Invoke(() =>
                {
                    _testWaveIn?.StopRecording();
                    _testWaveIn?.Dispose();
                    _testWaveIn = null;
                    TestMicButton.Content = "Let's Check";
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start mic test: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            TestMicButton.Content = "Let's Check";
        }
    }

    public void Cleanup()
    {
        _levelTimer?.Stop();
        _testWaveIn?.StopRecording();
        _testWaveIn?.Dispose();
    }
}
