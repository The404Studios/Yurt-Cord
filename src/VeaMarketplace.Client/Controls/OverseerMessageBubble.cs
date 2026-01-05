using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

/// <summary>
/// Animated message bubble for Overseer with flying animation, envelope transformation, and rainbow text
/// </summary>
public class OverseerMessageBubble : UserControl
{
    private Border _container = null!;
    private TextBlock _messageText = null!;
    private Canvas _envelopeCanvas = null!;
    private Grid _mainGrid = null!;
    private DispatcherTimer? _transformTimer;
    private bool _hasTransformed;

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(OverseerMessageBubble),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty SenderProperty =
        DependencyProperty.Register(nameof(Sender), typeof(string), typeof(OverseerMessageBubble),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TimestampProperty =
        DependencyProperty.Register(nameof(Timestamp), typeof(DateTime), typeof(OverseerMessageBubble),
            new PropertyMetadata(DateTime.Now));

    public static readonly DependencyProperty IsOwnMessageProperty =
        DependencyProperty.Register(nameof(IsOwnMessage), typeof(bool), typeof(OverseerMessageBubble),
            new PropertyMetadata(false));

    public static readonly DependencyProperty EnableAnimationsProperty =
        DependencyProperty.Register(nameof(EnableAnimations), typeof(bool), typeof(OverseerMessageBubble),
            new PropertyMetadata(true));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Sender
    {
        get => (string)GetValue(SenderProperty);
        set => SetValue(SenderProperty, value);
    }

    public DateTime Timestamp
    {
        get => (DateTime)GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public bool IsOwnMessage
    {
        get => (bool)GetValue(IsOwnMessageProperty);
        set => SetValue(IsOwnMessageProperty, value);
    }

    public bool EnableAnimations
    {
        get => (bool)GetValue(EnableAnimationsProperty);
        set => SetValue(EnableAnimationsProperty, value);
    }

    public OverseerMessageBubble()
    {
        InitializeComponent();
        Loaded += OverseerMessageBubble_Loaded;
        Unloaded += OverseerMessageBubble_Unloaded;
    }

    private void InitializeComponent()
    {
        _mainGrid = new Grid();

        // Create envelope canvas (hidden initially)
        _envelopeCanvas = new Canvas
        {
            Width = 60,
            Height = 40,
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        CreateEnvelopeGraphics();

        // Create message container
        _container = new Border
        {
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(10, 5, 10, 5),
            MaxWidth = 400,
            Effect = new DropShadowEffect
            {
                Color = Colors.Cyan,
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };

        // Create rainbow gradient for background
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 30, 30, 50), 0));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 40, 40, 60), 1));
        _container.Background = gradientBrush;

        // Create text with rainbow effect
        _messageText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 14
        };

        var stackPanel = new StackPanel();

        // Sender name
        var senderText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 200)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        senderText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Sender)) { Source = this });
        stackPanel.Children.Add(senderText);

        stackPanel.Children.Add(_messageText);

        // Timestamp
        var timeText = new TextBlock
        {
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 140)),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        timeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Timestamp))
        {
            Source = this,
            StringFormat = "HH:mm"
        });
        stackPanel.Children.Add(timeText);

        _container.Child = stackPanel;

        _mainGrid.Children.Add(_envelopeCanvas);
        _mainGrid.Children.Add(_container);

        Content = _mainGrid;
    }

    private void CreateEnvelopeGraphics()
    {
        // Envelope body
        var body = new Rectangle
        {
            Width = 50,
            Height = 35,
            Fill = new LinearGradientBrush(
                Color.FromRgb(255, 215, 0),
                Color.FromRgb(255, 180, 0),
                45),
            RadiusX = 3,
            RadiusY = 3,
            Effect = new DropShadowEffect
            {
                Color = Colors.Gold,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };
        Canvas.SetLeft(body, 5);
        Canvas.SetTop(body, 2);
        _envelopeCanvas.Children.Add(body);

        // Envelope flap (triangle)
        var flap = new Polygon
        {
            Points = new PointCollection
            {
                new Point(5, 5),
                new Point(30, 20),
                new Point(55, 5)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(255, 235, 100),
                Color.FromRgb(255, 200, 50),
                90)
        };
        _envelopeCanvas.Children.Add(flap);

        // Seal
        var seal = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new RadialGradientBrush(Colors.Red, Colors.DarkRed)
        };
        Canvas.SetLeft(seal, 24);
        Canvas.SetTop(seal, 14);
        _envelopeCanvas.Children.Add(seal);
    }

    private void OverseerMessageBubble_Loaded(object sender, RoutedEventArgs e)
    {
        if (EnableAnimations)
        {
            PlayEntryAnimation();
            StartTransformTimer();
        }
        else
        {
            ApplyRainbowText();
        }
    }

    private void OverseerMessageBubble_Unloaded(object sender, RoutedEventArgs e)
    {
        _transformTimer?.Stop();
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OverseerMessageBubble bubble)
        {
            bubble.ApplyRainbowText();
        }
    }

    private void ApplyRainbowText()
    {
        _messageText.Inlines.Clear();

        if (string.IsNullOrEmpty(Message)) return;

        // Create rainbow gradient text effect
        var colors = new[]
        {
            Color.FromRgb(255, 100, 100),   // Red
            Color.FromRgb(255, 180, 100),   // Orange
            Color.FromRgb(255, 255, 100),   // Yellow
            Color.FromRgb(100, 255, 100),   // Green
            Color.FromRgb(100, 255, 255),   // Cyan
            Color.FromRgb(100, 150, 255),   // Blue
            Color.FromRgb(200, 100, 255),   // Purple
            Color.FromRgb(255, 100, 200)    // Pink
        };

        for (int i = 0; i < Message.Length; i++)
        {
            var colorIndex = i % colors.Length;
            var nextColorIndex = (i + 1) % colors.Length;

            // Interpolate between colors for smoother gradient
            var t = (i % 1.0);
            var color = colors[colorIndex];

            var run = new System.Windows.Documents.Run(Message[i].ToString())
            {
                Foreground = new SolidColorBrush(color)
            };

            _messageText.Inlines.Add(run);
        }

        // Animate the rainbow
        AnimateRainbowColors();
    }

    private void AnimateRainbowColors()
    {
        var colorAnimation = new ColorAnimation
        {
            Duration = TimeSpan.FromSeconds(3),
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };

        // Animate the glow effect
        if (_container.Effect is DropShadowEffect shadow)
        {
            var hueShift = new ColorAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(4),
                RepeatBehavior = RepeatBehavior.Forever
            };

            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Colors.Cyan, KeyTime.FromPercent(0)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Colors.Magenta, KeyTime.FromPercent(0.25)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Colors.Yellow, KeyTime.FromPercent(0.5)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Colors.Lime, KeyTime.FromPercent(0.75)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Colors.Cyan, KeyTime.FromPercent(1)));

            shadow.BeginAnimation(DropShadowEffect.ColorProperty, hueShift);
        }
    }

    private void PlayEntryAnimation()
    {
        // Start off-screen to the right
        var translateTransform = new TranslateTransform(ActualWidth > 0 ? ActualWidth + 200 : 800, 0);
        _container.RenderTransform = translateTransform;
        _container.Opacity = 0;

        // Scale transform for bounce effect
        var scaleTransform = new ScaleTransform(0.5, 0.5);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translateTransform);
        _container.RenderTransform = transformGroup;
        _container.RenderTransformOrigin = new Point(0.5, 0.5);

        // Fly in animation
        var flyIn = new DoubleAnimation
        {
            From = 600,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(800),
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 5 }
        };

        // Fade in
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(400)
        };

        // Scale up with bounce
        var scaleUp = new DoubleAnimation
        {
            From = 0.3,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
        };

        translateTransform.BeginAnimation(TranslateTransform.XProperty, flyIn);
        _container.BeginAnimation(OpacityProperty, fadeIn);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
    }

    private void StartTransformTimer()
    {
        _transformTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _transformTimer.Tick += (s, e) =>
        {
            if (!_hasTransformed)
            {
                PlayEnvelopeTransformation();
                _hasTransformed = true;
            }
            _transformTimer.Stop();
        };
        _transformTimer.Start();
    }

    private void PlayEnvelopeTransformation()
    {
        // First, shrink and transform to envelope
        var shrinkScale = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        shrinkScale.Completed += (s, e) =>
        {
            _container.Visibility = Visibility.Collapsed;
            _envelopeCanvas.Visibility = Visibility.Visible;

            // Animate envelope
            PlayEnvelopeAnimation();
        };

        if (_container.RenderTransform is TransformGroup tg && tg.Children.Count > 0)
        {
            if (tg.Children[0] is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkScale);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkScale);
            }
        }
        else
        {
            var st = new ScaleTransform(1, 1);
            _container.RenderTransform = st;
            _container.RenderTransformOrigin = new Point(0.5, 0.5);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkScale);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkScale);
        }
    }

    private void PlayEnvelopeAnimation()
    {
        _envelopeCanvas.Opacity = 0;
        var envelopeScale = new ScaleTransform(0.5, 0.5);
        _envelopeCanvas.RenderTransform = envelopeScale;
        _envelopeCanvas.RenderTransformOrigin = new Point(0.5, 0.5);

        var popIn = new DoubleAnimation
        {
            From = 0.5,
            To = 1.2,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
        };

        var settleDown = new DoubleAnimation
        {
            From = 1.2,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(150),
            BeginTime = TimeSpan.FromMilliseconds(200)
        };

        var fadeIn = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(150)
        };

        // Floating animation
        var floatUp = new DoubleAnimation
        {
            From = 0,
            To = -5,
            Duration = TimeSpan.FromMilliseconds(1000),
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase()
        };

        var floatTranslate = new TranslateTransform();
        var tg = new TransformGroup();
        tg.Children.Add(envelopeScale);
        tg.Children.Add(floatTranslate);
        _envelopeCanvas.RenderTransform = tg;

        _envelopeCanvas.BeginAnimation(OpacityProperty, fadeIn);
        envelopeScale.BeginAnimation(ScaleTransform.ScaleXProperty, popIn);
        envelopeScale.BeginAnimation(ScaleTransform.ScaleYProperty, popIn);

        popIn.Completed += (s, e) =>
        {
            floatTranslate.BeginAnimation(TranslateTransform.YProperty, floatUp);

            // After 2 seconds, open envelope and show message again
            var reopenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            reopenTimer.Tick += (s2, e2) =>
            {
                reopenTimer.Stop();
                OpenEnvelopeAndShowMessage();
            };
            reopenTimer.Start();
        };
    }

    private void OpenEnvelopeAndShowMessage()
    {
        // Envelope opening animation
        var openFlap = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        // Fade out envelope
        var fadeOutEnvelope = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            BeginTime = TimeSpan.FromMilliseconds(200)
        };

        fadeOutEnvelope.Completed += (s, e) =>
        {
            _envelopeCanvas.Visibility = Visibility.Collapsed;
            _container.Visibility = Visibility.Visible;

            // Pop in message with style
            var st = new ScaleTransform(0, 0);
            _container.RenderTransform = st;
            _container.RenderTransformOrigin = new Point(0.5, 0.5);
            _container.Opacity = 1;

            var popIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 3 }
            };

            st.BeginAnimation(ScaleTransform.ScaleXProperty, popIn);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, popIn);
        };

        _envelopeCanvas.BeginAnimation(OpacityProperty, fadeOutEnvelope);
    }
}
