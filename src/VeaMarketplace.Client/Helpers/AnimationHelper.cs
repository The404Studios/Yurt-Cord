using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides reusable animation utilities for smooth UI transitions
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Animates a view transition with fade and slide effect
    /// </summary>
    public static async Task AnimateViewTransition(
        UIElement outgoingView,
        UIElement incomingView,
        TransitionDirection direction = TransitionDirection.Right,
        double duration = 250)
    {
        var ms = TimeSpan.FromMilliseconds(duration);
        var slideDistance = 30.0;

        // Ensure incoming view starts invisible
        incomingView.Opacity = 0;
        incomingView.Visibility = Visibility.Visible;

        // Set up transforms
        EnsureTranslateTransform(outgoingView);
        EnsureTranslateTransform(incomingView);

        var outTransform = GetTranslateTransform(outgoingView);
        var inTransform = GetTranslateTransform(incomingView);

        // Initial position for incoming view
        var slideX = direction switch
        {
            TransitionDirection.Left => -slideDistance,
            TransitionDirection.Right => slideDistance,
            _ => 0
        };
        var slideY = direction switch
        {
            TransitionDirection.Up => -slideDistance,
            TransitionDirection.Down => slideDistance,
            _ => 0
        };

        inTransform.X = slideX;
        inTransform.Y = slideY;

        // Fade out + slide out animation for outgoing
        var fadeOut = new DoubleAnimation(1, 0, ms)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Fade in + slide in animation for incoming
        var fadeIn = new DoubleAnimation(0, 1, ms)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slideIn = new DoubleAnimation(slideX, 0, ms)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideInY = new DoubleAnimation(slideY, 0, ms)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Start animations
        outgoingView.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        incomingView.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        if (slideX != 0)
            inTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        if (slideY != 0)
            inTransform.BeginAnimation(TranslateTransform.YProperty, slideInY);

        await Task.Delay((int)duration);

        outgoingView.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Animates element appearance with fade and optional scale/slide
    /// </summary>
    public static void AnimateIn(
        UIElement element,
        double duration = 300,
        bool withScale = false,
        bool withSlide = false,
        double slideDistance = 20)
    {
        var ms = TimeSpan.FromMilliseconds(duration);
        element.Opacity = 0;
        element.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, ms)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        if (withSlide)
        {
            EnsureTranslateTransform(element);
            var transform = GetTranslateTransform(element);
            transform.Y = slideDistance;
            var slideIn = new DoubleAnimation(slideDistance, 0, ms)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        if (withScale && element.RenderTransform is ScaleTransform scale)
        {
            var scaleAnim = new DoubleAnimation(0.95, 1, ms)
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }
    }

    /// <summary>
    /// Animates element disappearance with fade
    /// </summary>
    public static async Task AnimateOut(UIElement element, double duration = 200)
    {
        var ms = TimeSpan.FromMilliseconds(duration);

        var fadeOut = new DoubleAnimation(1, 0, ms)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        var tcs = new TaskCompletionSource();
        fadeOut.Completed += (s, e) =>
        {
            element.Visibility = Visibility.Collapsed;
            tcs.SetResult();
        };

        element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        await tcs.Task;
    }

    /// <summary>
    /// Creates a shake animation for error feedback
    /// </summary>
    public static void Shake(UIElement element, double intensity = 10, double duration = 350)
    {
        EnsureTranslateTransform(element);
        var transform = GetTranslateTransform(element);

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(duration)
        };

        var factor = intensity;
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(factor, KeyTime.FromPercent(0.1)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-factor, KeyTime.FromPercent(0.2)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(factor * 0.8, KeyTime.FromPercent(0.35)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-factor * 0.8, KeyTime.FromPercent(0.5)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(factor * 0.5, KeyTime.FromPercent(0.65)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-factor * 0.5, KeyTime.FromPercent(0.8)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>
    /// Creates a pulse/pop animation for emphasis
    /// </summary>
    public static void Pulse(UIElement element, double scale = 1.1, double duration = 200)
    {
        EnsureScaleTransform(element);
        var transform = element.RenderTransform as ScaleTransform ??
            (element.RenderTransform as TransformGroup)?.Children.OfType<ScaleTransform>().FirstOrDefault();

        if (transform == null) return;

        var ms = TimeSpan.FromMilliseconds(duration);
        var animation = new DoubleAnimation(1, scale, ms)
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    /// <summary>
    /// Creates a bounce animation for interactive feedback
    /// </summary>
    public static void Bounce(UIElement element, double distance = 5, double duration = 300)
    {
        EnsureTranslateTransform(element);
        var transform = GetTranslateTransform(element);

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(duration)
        };

        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-distance, KeyTime.FromPercent(0.3),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.6),
            new BounceEase { Bounces = 2, Bounciness = 4 }));

        transform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    /// <summary>
    /// Animates height change smoothly
    /// </summary>
    public static void AnimateHeight(FrameworkElement element, double targetHeight, double duration = 300)
    {
        var animation = new DoubleAnimation(element.ActualHeight, targetHeight, TimeSpan.FromMilliseconds(duration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        element.BeginAnimation(FrameworkElement.HeightProperty, animation);
    }

    private static void EnsureTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform) return;

        if (element.RenderTransform is TransformGroup group)
        {
            if (!group.Children.OfType<TranslateTransform>().Any())
                group.Children.Add(new TranslateTransform());
        }
        else if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
        {
            element.RenderTransform = new TranslateTransform();
        }
    }

    private static void EnsureScaleTransform(UIElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        if (element.RenderTransform is ScaleTransform) return;

        if (element.RenderTransform is TransformGroup group)
        {
            if (!group.Children.OfType<ScaleTransform>().Any())
                group.Children.Add(new ScaleTransform());
        }
        else if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
        {
            element.RenderTransform = new ScaleTransform();
        }
    }

    private static TranslateTransform GetTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform tt) return tt;

        if (element.RenderTransform is TransformGroup group)
        {
            var transform = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (transform != null) return transform;
        }

        // Create and add a TranslateTransform if not found
        var newTransform = new TranslateTransform();
        if (element.RenderTransform is TransformGroup existingGroup)
        {
            existingGroup.Children.Add(newTransform);
        }
        else
        {
            element.RenderTransform = newTransform;
        }
        return newTransform;
    }
}

public enum TransitionDirection
{
    Left,
    Right,
    Up,
    Down
}
