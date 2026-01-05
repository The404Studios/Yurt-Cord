using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Views;

/// <summary>
/// Transparent overlay window for flying messages and notifications
/// that appear on top of all applications
/// </summary>
public partial class OverseerOverlay : Window
{
    // Win32 constants for click-through window
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private readonly Random _random = new();
    private static OverseerOverlay? _instance;

    public static OverseerOverlay Instance => _instance ??= new OverseerOverlay();

    public OverseerOverlay()
    {
        InitializeComponent();
        _instance = this;

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Make window click-through
        MakeClickThrough();
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set window to cover entire screen
        Left = 0;
        Top = 0;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    /// <summary>
    /// Spawns a flying message that crosses the screen
    /// </summary>
    public void SpawnFlyingMessage(string sender, string content, string? avatarUrl = null)
    {
        Dispatcher.Invoke(() =>
        {
            var element = CreateFlyingMessageElement(sender, content, avatarUrl);
            AnimateFlyingMessage(element);
        });
    }

    /// <summary>
    /// Spawns a flying notification that crosses the screen
    /// </summary>
    public void SpawnFlyingNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        Dispatcher.Invoke(() =>
        {
            var element = CreateFlyingNotificationElement(title, message, type);
            AnimateFlyingMessage(element);
        });
    }

    /// <summary>
    /// Spawns an envelope that floats across the screen
    /// </summary>
    public void SpawnFlyingEnvelope(string sender)
    {
        Dispatcher.Invoke(() =>
        {
            var element = CreateFlyingEnvelopeElement(sender);
            AnimateFlyingMessage(element, isEnvelope: true);
        });
    }

    private FrameworkElement CreateFlyingMessageElement(string sender, string content, string? avatarUrl)
    {
        var container = new Border
        {
            CornerRadius = new CornerRadius(25),
            Padding = new Thickness(16, 12, 16, 12),
            MaxWidth = 400,
            BorderThickness = new Thickness(2),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 25,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };

        // Neon border
        container.BorderBrush = new LinearGradientBrush(
            Color.FromRgb(0, 255, 136),
            Color.FromRgb(0, 204, 255),
            45);

        // Dark semi-transparent background
        container.Background = new SolidColorBrush(Color.FromArgb(230, 15, 15, 30));

        var stack = new StackPanel();

        // Sender name with glow
        var senderText = new TextBlock
        {
            Text = sender,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };

        // Rainbow text for sender
        senderText.Foreground = new LinearGradientBrush(
            Color.FromRgb(0, 255, 136),
            Color.FromRgb(0, 204, 255),
            0);

        stack.Children.Add(senderText);

        // Message content with rainbow effect
        var contentText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            MaxWidth = 350
        };

        // Apply rainbow colors to content
        ApplyRainbowText(contentText, content);
        stack.Children.Add(contentText);

        container.Child = stack;

        // Animate the glow
        AnimateGlow(container);

        return container;
    }

    private FrameworkElement CreateFlyingNotificationElement(string title, string message, NotificationType type)
    {
        var (primaryColor, secondaryColor) = type switch
        {
            NotificationType.Success => (Color.FromRgb(0, 255, 136), Color.FromRgb(0, 200, 100)),
            NotificationType.Warning => (Color.FromRgb(255, 215, 0), Color.FromRgb(255, 180, 0)),
            NotificationType.Error => (Color.FromRgb(255, 68, 68), Color.FromRgb(255, 0, 100)),
            _ => (Color.FromRgb(0, 204, 255), Color.FromRgb(100, 150, 255))
        };

        var container = new Border
        {
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18, 14, 18, 14),
            MaxWidth = 350,
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(primaryColor),
            Background = new SolidColorBrush(Color.FromArgb(230, 15, 15, 30)),
            Effect = new DropShadowEffect
            {
                Color = primaryColor,
                BlurRadius = 25,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };

        var stack = new StackPanel();

        // Icon based on type
        var icon = type switch
        {
            NotificationType.Success => "✓",
            NotificationType.Warning => "⚠",
            NotificationType.Error => "✕",
            NotificationType.FriendRequest => "👥",
            NotificationType.Message => "💬",
            _ => "ℹ"
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(iconText);

        var titleText = new TextBlock
        {
            Text = title.ToUpper(),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(primaryColor),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = primaryColor,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
        header.Children.Add(titleText);

        stack.Children.Add(header);

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220)),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
        stack.Children.Add(messageText);

        container.Child = stack;

        return container;
    }

    private FrameworkElement CreateFlyingEnvelopeElement(string sender)
    {
        var container = new Canvas
        {
            Width = 80,
            Height = 60
        };

        // Envelope body
        var body = new Rectangle
        {
            Width = 70,
            Height = 50,
            RadiusX = 6,
            RadiusY = 6,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0, 255, 136),
                Color.FromRgb(0, 204, 255),
                45),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };
        Canvas.SetLeft(body, 5);
        Canvas.SetTop(body, 5);
        container.Children.Add(body);

        // Envelope flap
        var flap = new Polygon
        {
            Points = new PointCollection
            {
                new Point(5, 8),
                new Point(40, 30),
                new Point(75, 8)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(0, 255, 200),
                Color.FromRgb(0, 180, 255),
                90)
        };
        container.Children.Add(flap);

        // Seal
        var seal = new Ellipse
        {
            Width = 18,
            Height = 18,
            Fill = new RadialGradientBrush(
                Color.FromRgb(255, 0, 255),
                Color.FromRgb(150, 0, 255)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 0, 255),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.9
            }
        };
        Canvas.SetLeft(seal, 31);
        Canvas.SetTop(seal, 21);
        container.Children.Add(seal);

        // Sender label
        var senderLabel = new TextBlock
        {
            Text = $"From: {sender}",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 5,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };
        Canvas.SetLeft(senderLabel, 0);
        Canvas.SetTop(senderLabel, 58);
        container.Children.Add(senderLabel);

        return container;
    }

    private void ApplyRainbowText(TextBlock textBlock, string content)
    {
        var colors = new[]
        {
            Color.FromRgb(0, 255, 136),
            Color.FromRgb(0, 230, 180),
            Color.FromRgb(0, 204, 255),
            Color.FromRgb(100, 150, 255),
            Color.FromRgb(180, 100, 255),
            Color.FromRgb(255, 0, 255),
            Color.FromRgb(255, 100, 200),
        };

        textBlock.Inlines.Clear();

        for (int i = 0; i < content.Length; i++)
        {
            var colorIndex = (i / 3) % colors.Length;
            var run = new System.Windows.Documents.Run(content[i].ToString())
            {
                Foreground = new SolidColorBrush(colors[colorIndex])
            };
            textBlock.Inlines.Add(run);
        }
    }

    private void AnimateFlyingMessage(FrameworkElement element, bool isEnvelope = false)
    {
        OverlayCanvas.Children.Add(element);

        // Random vertical position (avoid edges)
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var minY = screenHeight * 0.1;
        var maxY = screenHeight * 0.7;
        var startY = _random.NextDouble() * (maxY - minY) + minY;

        // Start from right side, fly to left
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var startX = screenWidth + 50;
        var endX = -500;

        Canvas.SetLeft(element, startX);
        Canvas.SetTop(element, startY);

        // Duration varies for envelopes (slower float) vs messages
        var duration = isEnvelope ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(4);

        // Create animations
        var flyAnimation = new DoubleAnimation
        {
            From = startX,
            To = endX,
            Duration = duration,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        // Add gentle floating motion for envelopes
        if (isEnvelope)
        {
            var floatAnimation = new DoubleAnimation
            {
                From = startY,
                To = startY - 30,
                Duration = TimeSpan.FromSeconds(1.5),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2)
            };
            element.BeginAnimation(Canvas.TopProperty, floatAnimation);
        }

        // Fade in
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(400)
        };

        // Fade out at the end
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(500),
            BeginTime = duration - TimeSpan.FromMilliseconds(500)
        };

        flyAnimation.Completed += (s, e) =>
        {
            OverlayCanvas.Children.Remove(element);
        };

        element.BeginAnimation(Canvas.LeftProperty, flyAnimation);
        element.BeginAnimation(OpacityProperty, fadeIn);

        // Start fade out animation separately
        var fadeTimer = new DispatcherTimer
        {
            Interval = duration - TimeSpan.FromMilliseconds(600)
        };
        fadeTimer.Tick += (s, e) =>
        {
            fadeTimer.Stop();
            element.BeginAnimation(OpacityProperty, fadeOut);
        };
        fadeTimer.Start();
    }

    private void AnimateGlow(Border container)
    {
        if (container.Effect is DropShadowEffect shadow)
        {
            var colorAnimation = new ColorAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(3),
                RepeatBehavior = RepeatBehavior.Forever
            };

            colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 255, 136), KeyTime.FromPercent(0)));
            colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 204, 255), KeyTime.FromPercent(0.33)));
            colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(255, 0, 255), KeyTime.FromPercent(0.66)));
            colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 255, 136), KeyTime.FromPercent(1)));

            shadow.BeginAnimation(DropShadowEffect.ColorProperty, colorAnimation);
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        FriendRequest,
        Message
    }
}
