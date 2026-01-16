using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace VeaMarketplace.Client.Controls
{
    /// <summary>
    /// A border control with animated neon glow effects.
    /// Supports pulsing glow, color cycling, and intensity animations.
    /// </summary>
    public class GlowingBorder : Border
    {
        static GlowingBorder()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GlowingBorder),
                new FrameworkPropertyMetadata(typeof(GlowingBorder)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(GlowingBorder),
                new PropertyMetadata(Color.FromArgb(255, 0, 255, 159), OnGlowColorChanged));

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.Register(nameof(GlowIntensity), typeof(double), typeof(GlowingBorder),
                new PropertyMetadata(0.6, OnGlowIntensityChanged));

        public static readonly DependencyProperty GlowRadiusProperty =
            DependencyProperty.Register(nameof(GlowRadius), typeof(double), typeof(GlowingBorder),
                new PropertyMetadata(20.0, OnGlowRadiusChanged));

        public static readonly DependencyProperty IsPulsingProperty =
            DependencyProperty.Register(nameof(IsPulsing), typeof(bool), typeof(GlowingBorder),
                new PropertyMetadata(false, OnIsPulsingChanged));

        public static readonly DependencyProperty PulseDurationProperty =
            DependencyProperty.Register(nameof(PulseDuration), typeof(TimeSpan), typeof(GlowingBorder),
                new PropertyMetadata(TimeSpan.FromSeconds(2)));

        public static readonly DependencyProperty IsRainbowProperty =
            DependencyProperty.Register(nameof(IsRainbow), typeof(bool), typeof(GlowingBorder),
                new PropertyMetadata(false, OnIsRainbowChanged));

        public static readonly DependencyProperty RainbowDurationProperty =
            DependencyProperty.Register(nameof(RainbowDuration), typeof(TimeSpan), typeof(GlowingBorder),
                new PropertyMetadata(TimeSpan.FromSeconds(5)));

        #endregion

        #region Properties

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public double GlowIntensity
        {
            get => (double)GetValue(GlowIntensityProperty);
            set => SetValue(GlowIntensityProperty, value);
        }

        public double GlowRadius
        {
            get => (double)GetValue(GlowRadiusProperty);
            set => SetValue(GlowRadiusProperty, value);
        }

        public bool IsPulsing
        {
            get => (bool)GetValue(IsPulsingProperty);
            set => SetValue(IsPulsingProperty, value);
        }

        public TimeSpan PulseDuration
        {
            get => (TimeSpan)GetValue(PulseDurationProperty);
            set => SetValue(PulseDurationProperty, value);
        }

        public bool IsRainbow
        {
            get => (bool)GetValue(IsRainbowProperty);
            set => SetValue(IsRainbowProperty, value);
        }

        public TimeSpan RainbowDuration
        {
            get => (TimeSpan)GetValue(RainbowDurationProperty);
            set => SetValue(RainbowDurationProperty, value);
        }

        #endregion

        private DropShadowEffect _glowEffect;
        private Storyboard _pulseStoryboard;
        private Storyboard _rainbowStoryboard;

        public GlowingBorder()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeGlow();

            if (IsPulsing)
            {
                StartPulseAnimation();
            }

            if (IsRainbow)
            {
                StartRainbowAnimation();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAllAnimations();
        }

        private void InitializeGlow()
        {
            _glowEffect = new DropShadowEffect
            {
                Color = GlowColor,
                BlurRadius = GlowRadius,
                ShadowDepth = 0,
                Opacity = GlowIntensity
            };
            Effect = _glowEffect;
        }

        private static void OnGlowColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowingBorder border && border._glowEffect != null)
            {
                border._glowEffect.Color = (Color)e.NewValue;
            }
        }

        private static void OnGlowIntensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowingBorder border && border._glowEffect != null)
            {
                border._glowEffect.Opacity = (double)e.NewValue;
            }
        }

        private static void OnGlowRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowingBorder border && border._glowEffect != null)
            {
                border._glowEffect.BlurRadius = (double)e.NewValue;
            }
        }

        private static void OnIsPulsingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowingBorder border)
            {
                if ((bool)e.NewValue)
                {
                    border.StartPulseAnimation();
                }
                else
                {
                    border.StopPulseAnimation();
                }
            }
        }

        private static void OnIsRainbowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowingBorder border)
            {
                if ((bool)e.NewValue)
                {
                    border.StartRainbowAnimation();
                }
                else
                {
                    border.StopRainbowAnimation();
                }
            }
        }

        private void StartPulseAnimation()
        {
            if (_glowEffect == null) return;

            _pulseStoryboard = new Storyboard();

            var radiusAnim = new DoubleAnimationUsingKeyFrames();
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius * 1.8, KeyTime.FromTimeSpan(TimeSpan.FromTicks(PulseDuration.Ticks / 2)))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius, KeyTime.FromTimeSpan(PulseDuration))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            Storyboard.SetTarget(radiusAnim, this);
            Storyboard.SetTargetProperty(radiusAnim, new PropertyPath("Effect.BlurRadius"));

            var opacityAnim = new DoubleAnimationUsingKeyFrames();
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowIntensity, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(Math.Min(GlowIntensity * 1.5, 1.0), KeyTime.FromTimeSpan(TimeSpan.FromTicks(PulseDuration.Ticks / 2))));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowIntensity, KeyTime.FromTimeSpan(PulseDuration)));
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Effect.Opacity"));

            _pulseStoryboard.Children.Add(radiusAnim);
            _pulseStoryboard.Children.Add(opacityAnim);
            _pulseStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _pulseStoryboard.Begin();
        }

        private void StopPulseAnimation()
        {
            _pulseStoryboard?.Stop();
            _pulseStoryboard = null;

            if (_glowEffect != null)
            {
                _glowEffect.BlurRadius = GlowRadius;
                _glowEffect.Opacity = GlowIntensity;
            }
        }

        private void StartRainbowAnimation()
        {
            if (_glowEffect == null) return;

            _rainbowStoryboard = new Storyboard();

            var colorAnim = new ColorAnimationUsingKeyFrames();
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(0, 255, 159), KeyTime.FromTimeSpan(TimeSpan.Zero)));
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(0, 229, 255), KeyTime.FromTimeSpan(TimeSpan.FromTicks(RainbowDuration.Ticks / 5))));
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(179, 102, 255), KeyTime.FromTimeSpan(TimeSpan.FromTicks(RainbowDuration.Ticks * 2 / 5))));
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(255, 102, 178), KeyTime.FromTimeSpan(TimeSpan.FromTicks(RainbowDuration.Ticks * 3 / 5))));
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(255, 153, 51), KeyTime.FromTimeSpan(TimeSpan.FromTicks(RainbowDuration.Ticks * 4 / 5))));
            colorAnim.KeyFrames.Add(new EasingColorKeyFrame(Color.FromRgb(0, 255, 159), KeyTime.FromTimeSpan(RainbowDuration)));
            Storyboard.SetTarget(colorAnim, this);
            Storyboard.SetTargetProperty(colorAnim, new PropertyPath("Effect.Color"));

            _rainbowStoryboard.Children.Add(colorAnim);
            _rainbowStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _rainbowStoryboard.Begin();
        }

        private void StopRainbowAnimation()
        {
            _rainbowStoryboard?.Stop();
            _rainbowStoryboard = null;

            if (_glowEffect != null)
            {
                _glowEffect.Color = GlowColor;
            }
        }

        private void StopAllAnimations()
        {
            StopPulseAnimation();
            StopRainbowAnimation();
        }

        /// <summary>
        /// Triggers a flash effect for attention
        /// </summary>
        public void Flash()
        {
            if (_glowEffect == null) return;

            var flashStoryboard = new Storyboard();

            var radiusAnim = new DoubleAnimationUsingKeyFrames();
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius * 3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius * 1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
            radiusAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowRadius, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
            Storyboard.SetTarget(radiusAnim, this);
            Storyboard.SetTargetProperty(radiusAnim, new PropertyPath("Effect.BlurRadius"));

            var opacityAnim = new DoubleAnimationUsingKeyFrames();
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowIntensity, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(GlowIntensity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Effect.Opacity"));

            flashStoryboard.Children.Add(radiusAnim);
            flashStoryboard.Children.Add(opacityAnim);
            flashStoryboard.Begin();
        }

        /// <summary>
        /// Intensifies the glow temporarily
        /// </summary>
        public void Intensify(double multiplier = 1.5, TimeSpan? duration = null)
        {
            if (_glowEffect == null) return;

            var actualDuration = duration ?? TimeSpan.FromMilliseconds(300);

            var radiusAnim = new DoubleAnimation
            {
                To = GlowRadius * multiplier,
                Duration = actualDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = Math.Min(GlowIntensity * multiplier, 1.0),
                Duration = actualDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, radiusAnim);
            _glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnim);
        }

        /// <summary>
        /// Returns glow to normal intensity
        /// </summary>
        public void Normalize(TimeSpan? duration = null)
        {
            if (_glowEffect == null) return;

            var actualDuration = duration ?? TimeSpan.FromMilliseconds(200);

            var radiusAnim = new DoubleAnimation
            {
                To = GlowRadius,
                Duration = actualDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var opacityAnim = new DoubleAnimation
            {
                To = GlowIntensity,
                Duration = actualDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, radiusAnim);
            _glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnim);
        }
    }
}
