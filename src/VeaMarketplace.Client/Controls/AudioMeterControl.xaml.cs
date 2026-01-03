using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public partial class AudioMeterControl : UserControl
{
    private const int SegmentCount = 20;
    private readonly Rectangle[] _segments = new Rectangle[SegmentCount];
    private double _peakLevel;
    private readonly DispatcherTimer _peakDecayTimer;

    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(AudioMeterControl),
            new PropertyMetadata(0.0, OnLevelChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(AudioMeterControl),
            new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public AudioMeterControl()
    {
        InitializeComponent();
        CreateSegments();

        _peakDecayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _peakDecayTimer.Tick += PeakDecayTimer_Tick;
        _peakDecayTimer.Start();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_peakDecayTimer != null)
        {
            _peakDecayTimer.Stop();
            _peakDecayTimer.Tick -= PeakDecayTimer_Tick;
        }
        Unloaded -= OnUnloaded;
    }

    private void CreateSegments()
    {
        LevelSegments.Children.Clear();

        var greenBrush = new SolidColorBrush(Color.FromRgb(87, 242, 135)); // Green
        var yellowBrush = new SolidColorBrush(Color.FromRgb(254, 231, 92)); // Yellow
        var redBrush = new SolidColorBrush(Color.FromRgb(237, 66, 69)); // Red
        var dimBrush = new SolidColorBrush(Color.FromArgb(60, 87, 242, 135)); // Dim green

        for (int i = SegmentCount - 1; i >= 0; i--)
        {
            // Color based on position (bottom green, middle yellow, top red)
            Brush activeBrush;
            if (i < SegmentCount * 0.6)
                activeBrush = greenBrush;
            else if (i < SegmentCount * 0.85)
                activeBrush = yellowBrush;
            else
                activeBrush = redBrush;

            var segment = new Rectangle
            {
                Width = 20,
                Height = 3,
                Margin = new Thickness(0, 1, 0, 1),
                RadiusX = 1,
                RadiusY = 1,
                Fill = dimBrush,
                Tag = activeBrush
            };

            _segments[i] = segment;
            LevelSegments.Children.Add(segment);
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioMeterControl control)
        {
            control.UpdateMeter((double)e.NewValue);
        }
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioMeterControl control)
        {
            control.LevelSegments.Orientation = (Orientation)e.NewValue;
            control.CreateSegments();
        }
    }

    private void UpdateMeter(double level)
    {
        // Clamp level between 0 and 1
        level = Math.Max(0, Math.Min(1, level));

        // Track peak
        if (level > _peakLevel)
        {
            _peakLevel = level;
        }

        // Calculate how many segments should be lit
        var activeSegments = (int)(level * SegmentCount);

        for (int i = 0; i < SegmentCount; i++)
        {
            var segment = _segments[i];
            if (segment?.Tag is Brush activeBrush)
            {
                segment.Fill = i < activeSegments
                    ? activeBrush
                    : new SolidColorBrush(Color.FromArgb(40, 87, 242, 135));
            }
        }

        // Update peak indicator
        if (_peakLevel > 0)
        {
            PeakIndicator.Visibility = Visibility.Visible;
            Canvas.SetBottom(PeakIndicator, 2 + (_peakLevel * (ActualHeight - 8)));
        }
    }

    private void PeakDecayTimer_Tick(object? sender, EventArgs e)
    {
        // Slowly decay the peak
        _peakLevel = Math.Max(0, _peakLevel - 0.02);

        if (_peakLevel <= 0)
        {
            PeakIndicator.Visibility = Visibility.Collapsed;
        }
    }
}
