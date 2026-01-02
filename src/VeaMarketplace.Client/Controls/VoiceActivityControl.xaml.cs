using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VeaMarketplace.Client.Controls;

public partial class VoiceActivityControl : UserControl
{
    private readonly Rectangle[] _audioBars = new Rectangle[5];
    private readonly Storyboard _pulseStoryboard;
    private bool _isSpeaking;

    public static readonly DependencyProperty AvatarUrlProperty =
        DependencyProperty.Register(nameof(AvatarUrl), typeof(string), typeof(VoiceActivityControl),
            new PropertyMetadata(null, OnAvatarUrlChanged));

    public static readonly DependencyProperty IsSpeakingProperty =
        DependencyProperty.Register(nameof(IsSpeaking), typeof(bool), typeof(VoiceActivityControl),
            new PropertyMetadata(false, OnIsSpeakingChanged));

    public static readonly DependencyProperty AudioLevelProperty =
        DependencyProperty.Register(nameof(AudioLevel), typeof(double), typeof(VoiceActivityControl),
            new PropertyMetadata(0.0, OnAudioLevelChanged));

    public string AvatarUrl
    {
        get => (string)GetValue(AvatarUrlProperty);
        set => SetValue(AvatarUrlProperty, value);
    }

    public bool IsSpeaking
    {
        get => (bool)GetValue(IsSpeakingProperty);
        set => SetValue(IsSpeakingProperty, value);
    }

    public double AudioLevel
    {
        get => (double)GetValue(AudioLevelProperty);
        set => SetValue(AudioLevelProperty, value);
    }

    public VoiceActivityControl()
    {
        InitializeComponent();

        // Create audio visualization bars
        var greenBrush = new SolidColorBrush(Color.FromRgb(87, 242, 135));
        for (int i = 0; i < 5; i++)
        {
            var bar = new Rectangle
            {
                Width = 3,
                Height = 8,
                Fill = greenBrush,
                RadiusX = 1.5,
                RadiusY = 1.5,
                RenderTransformOrigin = new Point(0.5, 1)
            };
            Canvas.SetLeft(bar, 10 + i * 7);
            Canvas.SetBottom(bar, 4);
            bar.RenderTransform = new ScaleTransform(1, 1);
            _audioBars[i] = bar;
            AudioVisualizerCanvas.Children.Add(bar);
        }

        // Create pulse animation storyboard
        _pulseStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var scaleXAnimation = new DoubleAnimation
        {
            From = 1,
            To = 1.15,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var scaleYAnimation = new DoubleAnimation
        {
            From = 1,
            To = 1.15,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(scaleXAnimation, RingScale);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("ScaleX"));
        Storyboard.SetTarget(scaleYAnimation, RingScale);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));
        _pulseStoryboard.Children.Add(scaleXAnimation);
        _pulseStoryboard.Children.Add(scaleYAnimation);
    }

    private static void OnAvatarUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VoiceActivityControl control && e.NewValue is string url)
        {
            try
            {
                control.AvatarBrush.ImageSource = new BitmapImage(new Uri(url));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VoiceActivityControl: Failed to load avatar from {url}: {ex.Message}");
            }
        }
    }

    private static void OnIsSpeakingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VoiceActivityControl control)
        {
            control._isSpeaking = (bool)e.NewValue;
            control.UpdateVisualState();
        }
    }

    private static void OnAudioLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VoiceActivityControl control && control._isSpeaking)
        {
            control.UpdateAudioVisualization((double)e.NewValue);
        }
    }

    private void UpdateVisualState()
    {
        if (_isSpeaking)
        {
            VoiceRing.Visibility = Visibility.Visible;
            AudioVisualizerCanvas.Visibility = Visibility.Visible;
            _pulseStoryboard.Begin();
        }
        else
        {
            VoiceRing.Visibility = Visibility.Collapsed;
            AudioVisualizerCanvas.Visibility = Visibility.Collapsed;
            _pulseStoryboard.Stop();

            // Reset audio bars
            foreach (var bar in _audioBars)
            {
                if (bar.RenderTransform is ScaleTransform scale)
                {
                    scale.ScaleY = 1;
                }
            }
        }
    }

    private void UpdateAudioVisualization(double level)
    {
        var random = new Random();
        for (int i = 0; i < _audioBars.Length; i++)
        {
            if (_audioBars[i].RenderTransform is ScaleTransform scale)
            {
                // Vary height based on audio level with some randomization
                var targetHeight = Math.Max(1, level * 3 + random.NextDouble() * 0.5);
                scale.ScaleY = targetHeight;
            }
        }
    }
}
