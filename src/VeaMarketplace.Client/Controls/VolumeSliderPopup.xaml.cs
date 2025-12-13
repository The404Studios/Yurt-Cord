using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VeaMarketplace.Client.Controls;

public partial class VolumeSliderPopup : UserControl
{
    private string _userId = string.Empty;
    private bool _isMuted;
    private double _previousVolume = 100;

    public event EventHandler<double>? VolumeChanged;
    public event EventHandler<bool>? MuteToggled;
    public event EventHandler? CloseRequested;

    public VolumeSliderPopup()
    {
        InitializeComponent();
    }

    public void SetUser(string userId, string username, string avatarUrl, string? status = null)
    {
        _userId = userId;
        UsernameText.Text = username;
        StatusText.Text = status ?? "In call";

        try
        {
            UserAvatarBrush.ImageSource = new BitmapImage(new Uri(avatarUrl, UriKind.RelativeOrAbsolute));
        }
        catch
        {
            // Default avatar fallback
        }
    }

    public void SetVolume(double volume)
    {
        VolumeSlider.Value = volume;
        UpdateVolumeDisplay(volume);
    }

    public void SetMuted(bool isMuted)
    {
        _isMuted = isMuted;
        UpdateMuteState();
    }

    public void UpdateAudioLevel(double level)
    {
        // Apply volume adjustment to the preview
        var adjustedLevel = level * (VolumeSlider.Value / 100.0);
        var width = Math.Max(0, Math.Min(248, adjustedLevel * 248)); // 248 = container width - padding
        AudioLevelPreview.Width = width;

        // Color based on level
        if (adjustedLevel > 0.8)
        {
            AudioLevelPreview.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69));
        }
        else if (adjustedLevel > 0.5)
        {
            AudioLevelPreview.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(250, 166, 26));
        }
        else
        {
            AudioLevelPreview.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(67, 181, 129));
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateVolumeDisplay(e.NewValue);
        VolumeChanged?.Invoke(this, e.NewValue);

        // Auto-unmute when adjusting volume from 0
        if (_isMuted && e.NewValue > 0)
        {
            _isMuted = false;
            UpdateMuteState();
            MuteToggled?.Invoke(this, false);
        }
    }

    private void UpdateVolumeDisplay(double volume)
    {
        VolumePercentText.Text = $"{(int)volume}%";

        // Visual feedback for boost (>100%)
        if (volume > 100)
        {
            VolumePercentText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(250, 166, 26));
        }
        else if (volume == 0)
        {
            VolumePercentText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69));
        }
        else
        {
            VolumePercentText.Foreground = System.Windows.Media.Brushes.White;
        }
    }

    private void UpdateMuteState()
    {
        if (_isMuted)
        {
            MuteIcon.Text = "🔇";
            MuteText.Text = "Unmute";
        }
        else
        {
            MuteIcon.Text = "🔊";
            MuteText.Text = "Mute";
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (_isMuted)
        {
            // Unmute - restore previous volume
            _isMuted = false;
            VolumeSlider.Value = _previousVolume;
        }
        else
        {
            // Mute - save current volume and set to 0
            _previousVolume = VolumeSlider.Value > 0 ? VolumeSlider.Value : 100;
            _isMuted = true;
            VolumeSlider.Value = 0;
        }

        UpdateMuteState();
        MuteToggled?.Invoke(this, _isMuted);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        VolumeSlider.Value = 100;
        _isMuted = false;
        UpdateMuteState();

        VolumeChanged?.Invoke(this, 100);
        MuteToggled?.Invoke(this, false);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public string GetUserId() => _userId;
    public double GetVolume() => VolumeSlider.Value;
    public bool IsMuted() => _isMuted;
}
