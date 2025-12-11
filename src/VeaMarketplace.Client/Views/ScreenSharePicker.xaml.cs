using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class ScreenSharePicker : Window
{
    private readonly IVoiceService _voiceService;
    private readonly ObservableCollection<SelectableDisplayInfo> _displays = new();

    public DisplayInfo? SelectedDisplay { get; private set; }
    public int SelectedResolution { get; private set; } = 1080;
    public int SelectedFrameRate { get; private set; } = 30;
    public bool ShareAudio { get; private set; }

    public ScreenSharePicker(IVoiceService voiceService)
    {
        InitializeComponent();
        _voiceService = voiceService;

        // Add BoolToVis converter to resources
        Resources["BoolToVis"] = new BooleanToVisibilityConverter();

        DisplaysItemsControl.ItemsSource = _displays;
        LoadDisplays();
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
/// Wrapper for DisplayInfo with selection support
/// </summary>
public class SelectableDisplayInfo : INotifyPropertyChanged
{
    private bool _isSelected;

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
