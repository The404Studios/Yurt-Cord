using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class ScreenSharePicker : Window
{
    private readonly IVoiceService _voiceService;
    private readonly ObservableCollection<SelectableDisplayInfo> _displays = new();
    private readonly DispatcherTimer _previewTimer;

    public DisplayInfo? SelectedDisplay { get; private set; }
    public int SelectedResolution { get; private set; } = 1080;
    public int SelectedFrameRate { get; private set; } = 60;
    public bool ShareAudio { get; private set; }

    /// <summary>
    /// Gets the ScreenShareSettings based on user selections
    /// </summary>
    public ScreenShareSettings GetSettings()
    {
        var settings = new ScreenShareSettings
        {
            TargetFps = SelectedFrameRate,
            ShareAudio = ShareAudio,
            AdaptiveQuality = true
        };

        // Set resolution based on selection
        switch (SelectedResolution)
        {
            case 720:
                settings.TargetWidth = 1280;
                settings.TargetHeight = 720;
                settings.JpegQuality = 50;
                settings.MaxFrameSizeKb = 75;
                break;
            case 1080:
                settings.TargetWidth = 1920;
                settings.TargetHeight = 1080;
                settings.JpegQuality = 55;
                settings.MaxFrameSizeKb = 120;
                break;
            case 0: // Source/Native
                if (SelectedDisplay != null)
                {
                    settings.TargetWidth = SelectedDisplay.Width;
                    settings.TargetHeight = SelectedDisplay.Height;
                }
                else
                {
                    settings.TargetWidth = 1920;
                    settings.TargetHeight = 1080;
                }
                settings.JpegQuality = 60;
                settings.MaxFrameSizeKb = 150;
                break;
            default:
                settings.TargetWidth = 1280;
                settings.TargetHeight = 720;
                settings.JpegQuality = 50;
                settings.MaxFrameSizeKb = 100;
                break;
        }

        // Adjust quality based on frame rate
        if (SelectedFrameRate >= 60)
        {
            // Higher FPS = more frames = need smaller frames
            settings.JpegQuality = Math.Max(35, settings.JpegQuality - 10);
            settings.MaxFrameSizeKb = Math.Max(50, settings.MaxFrameSizeKb - 30);
        }
        else if (SelectedFrameRate <= 15)
        {
            // Lower FPS = can afford higher quality per frame
            settings.JpegQuality = Math.Min(75, settings.JpegQuality + 15);
            settings.MaxFrameSizeKb += 50;
        }

        return settings;
    }

    public ScreenSharePicker(IVoiceService voiceService)
    {
        InitializeComponent();
        _voiceService = voiceService;

        DisplaysItemsControl.ItemsSource = _displays;
        LoadDisplays();

        // Set up timer for refreshing previews
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update every 500ms
        };
        _previewTimer.Tick += PreviewTimer_Tick;
        _previewTimer.Start();

        // Capture initial previews
        CaptureAllPreviews();

        // Clean up when window closes
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        _previewTimer.Tick -= PreviewTimer_Tick;
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        CaptureAllPreviews();
    }

    private void CaptureAllPreviews()
    {
        foreach (var display in _displays)
        {
            try
            {
                var preview = CaptureDisplayPreview(display);
                if (preview != null)
                {
                    display.PreviewImageSource = preview;
                }
            }
            catch
            {
                // Ignore capture errors
            }
        }
    }

    private BitmapSource? CaptureDisplayPreview(SelectableDisplayInfo display)
    {
        try
        {
            // Capture at reduced resolution for preview (200x112 aspect ratio matches 16:9)
            const int previewWidth = 200;
            const int previewHeight = 112;

            using var bitmap = new System.Drawing.Bitmap(display.Width, display.Height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);

            // Capture the screen region
            graphics.CopyFromScreen(display.Left, display.Top, 0, 0,
                new System.Drawing.Size(display.Width, display.Height));

            // Resize to preview size
            using var resized = new System.Drawing.Bitmap(previewWidth, previewHeight);
            using var resizeGraphics = System.Drawing.Graphics.FromImage(resized);
            resizeGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            resizeGraphics.DrawImage(bitmap, 0, 0, previewWidth, previewHeight);

            // Convert to BitmapSource for WPF
            using var ms = new MemoryStream();
            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // Make it thread-safe

            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    private void LoadDisplays()
    {
        _displays.Clear();
        var displays = _voiceService.GetAvailableDisplays();

        foreach (var display in displays)
        {
            var selectable = new SelectableDisplayInfo(display);
            // Select primary by default
            if (display.IsPrimary)
            {
                selectable.IsSelected = true;
                SelectedDisplay = display;
            }
            _displays.Add(selectable);
        }
    }

    private void Display_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is SelectableDisplayInfo clickedDisplay)
        {
            // Deselect all
            foreach (var display in _displays)
            {
                display.IsSelected = false;
            }

            // Select clicked
            clickedDisplay.IsSelected = true;
            SelectedDisplay = clickedDisplay.ToDisplayInfo();
        }
    }

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDisplay == null)
        {
            MessageBox.Show("Please select a screen to share.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Get quality settings
        if (ResolutionCombo.SelectedItem is ComboBoxItem resItem && resItem.Tag is string resTag)
        {
            SelectedResolution = int.TryParse(resTag, out var res) ? res : 1080;
        }

        if (FrameRateCombo.SelectedItem is ComboBoxItem fpsItem && fpsItem.Tag is string fpsTag)
        {
            SelectedFrameRate = int.TryParse(fpsTag, out var fps) ? fps : 30;
        }

        ShareAudio = ShareAudioCheckbox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Wrapper for DisplayInfo with selection support and live preview
/// </summary>
public class SelectableDisplayInfo : INotifyPropertyChanged
{
    private bool _isSelected;
    private System.Windows.Media.ImageSource? _previewImageSource;

    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public int Index { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public System.Windows.Media.ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        set
        {
            _previewImageSource = value;
            OnPropertyChanged(nameof(PreviewImageSource));
            OnPropertyChanged(nameof(HasNoPreview));
        }
    }

    public bool HasNoPreview => _previewImageSource == null;

    public SelectableDisplayInfo() { }

    public SelectableDisplayInfo(DisplayInfo display)
    {
        DeviceName = display.DeviceName;
        FriendlyName = display.FriendlyName;
        Left = display.Left;
        Top = display.Top;
        Width = display.Width;
        Height = display.Height;
        IsPrimary = display.IsPrimary;
        Index = display.Index;
    }

    public DisplayInfo ToDisplayInfo() => new()
    {
        DeviceName = DeviceName,
        FriendlyName = FriendlyName,
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height,
        IsPrimary = IsPrimary,
        Index = Index
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
