using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace VeaMarketplace.Client.Controls
{
    /// <summary>
    /// Origami-inspired card control with 3D-like pop-out effects on hover.
    /// Features depth shadows, scale animations, and neon glow borders.
    /// </summary>
    public class PopOutCard : ContentControl
    {
        static PopOutCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PopOutCard),
                new FrameworkPropertyMetadata(typeof(PopOutCard)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty ElevationProperty =
            DependencyProperty.Register(nameof(Elevation), typeof(double), typeof(PopOutCard),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty HoverElevationProperty =
            DependencyProperty.Register(nameof(HoverElevation), typeof(double), typeof(PopOutCard),
                new PropertyMetadata(3.0));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(PopOutCard),
                new PropertyMetadata(new CornerRadius(16)));

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(PopOutCard),
                new PropertyMetadata(Color.FromArgb(255, 0, 255, 159)));

        public static readonly DependencyProperty IsInteractiveProperty =
            DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(PopOutCard),
                new PropertyMetadata(true));

        public static readonly DependencyProperty PopScaleProperty =
            DependencyProperty.Register(nameof(PopScale), typeof(double), typeof(PopOutCard),
                new PropertyMetadata(1.03));

        public static readonly DependencyProperty PopOffsetYProperty =
            DependencyProperty.Register(nameof(PopOffsetY), typeof(double), typeof(PopOutCard),
                new PropertyMetadata(-8.0));

        #endregion

        #region Properties

        public double Elevation
        {
            get => (double)GetValue(ElevationProperty);
            set => SetValue(ElevationProperty, value);
        }

        public double HoverElevation
        {
            get => (double)GetValue(HoverElevationProperty);
            set => SetValue(HoverElevationProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public double PopScale
        {
            get => (double)GetValue(PopScaleProperty);
            set => SetValue(PopScaleProperty, value);
        }

        public double PopOffsetY
        {
            get => (double)GetValue(PopOffsetYProperty);
            set => SetValue(PopOffsetYProperty, value);
        }

        #endregion

        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;

        public PopOutCard()
        {
            Loaded += OnLoaded;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            PreviewMouseDown += OnPreviewMouseDown;
            PreviewMouseUp += OnPreviewMouseUp;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetupTransforms();
            ApplyCardStyle();
        }

        private void SetupTransforms()
        {
            _scaleTransform = new ScaleTransform(1, 1);
            _translateTransform = new TranslateTransform(0, 0);

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(_scaleTransform);
            transformGroup.Children.Add(_translateTransform);
            RenderTransform = transformGroup;
            RenderTransformOrigin = new Point(0.5, 0.5);
        }

        private void ApplyCardStyle()
        {
            // Apply gradient background
            var bgGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 32, 45, 61), 0));
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 21, 32, 40), 0.6));
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 16, 24, 32), 1));
            Background = bgGradient;

            // Apply border gradient
            var borderGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            var glowColorLight = Color.FromArgb(80, GlowColor.R, GlowColor.G, GlowColor.B);
            borderGradient.GradientStops.Add(new GradientStop(glowColorLight, 0));
            borderGradient.GradientStops.Add(new GradientStop(Color.FromArgb(48, 0, 229, 255), 0.5));
            borderGradient.GradientStops.Add(new GradientStop(Color.FromArgb(80, 179, 102, 255), 1));
            BorderBrush = borderGradient;
            BorderThickness = new Thickness(1);

            // Apply depth shadow
            UpdateShadow(Elevation);

            if (IsInteractive)
            {
                Cursor = Cursors.Hand;
            }
        }

        private void UpdateShadow(double elevation)
        {
            var shadowDepth = 4 + (elevation * 4);
            var blurRadius = 10 + (elevation * 8);
            var opacity = 0.3 + (elevation * 0.1);

            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = blurRadius,
                ShadowDepth = shadowDepth,
                Opacity = Math.Min(opacity, 0.7),
                Direction = 270
            };
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsInteractive) return;

            // Animate scale up
            var scaleXAnim = new DoubleAnimation
            {
                To = PopScale,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            var scaleYAnim = new DoubleAnimation
            {
                To = PopScale,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            // Animate translate up
            var translateAnim = new DoubleAnimation
            {
                To = PopOffsetY,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };

            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            _translateTransform?.BeginAnimation(TranslateTransform.YProperty, translateAnim);

            // Update shadow for elevated state
            AnimateShadow(HoverElevation);

            // Add glow effect
            AnimateGlow(true);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsInteractive) return;

            // Animate scale back
            var scaleXAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleYAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Animate translate back
            var translateAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            _translateTransform?.BeginAnimation(TranslateTransform.YProperty, translateAnim);

            // Reset shadow
            AnimateShadow(Elevation);

            // Remove glow effect
            AnimateGlow(false);
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive) return;

            var scaleAnim = new DoubleAnimation
            {
                To = 0.97,
                Duration = TimeSpan.FromMilliseconds(60)
            };
            var translateAnim = new DoubleAnimation
            {
                To = 2,
                Duration = TimeSpan.FromMilliseconds(60)
            };

            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _translateTransform?.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractive) return;

            var scaleAnim = new DoubleAnimation
            {
                To = PopScale,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };
            var translateAnim = new DoubleAnimation
            {
                To = PopOffsetY,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };

            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _translateTransform?.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        }

        private void AnimateShadow(double targetElevation)
        {
            if (Effect is DropShadowEffect shadow)
            {
                var targetDepth = 4 + (targetElevation * 4);
                var targetBlur = 10 + (targetElevation * 8);
                var targetOpacity = 0.3 + (targetElevation * 0.1);

                var depthAnim = new DoubleAnimation
                {
                    To = targetDepth,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                var blurAnim = new DoubleAnimation
                {
                    To = targetBlur,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                var opacityAnim = new DoubleAnimation
                {
                    To = Math.Min(targetOpacity, 0.7),
                    Duration = TimeSpan.FromMilliseconds(200)
                };

                shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, depthAnim);
                shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnim);
                shadow.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnim);
            }
        }

        private void AnimateGlow(bool show)
        {
            // Update border for glow effect
            if (BorderBrush is LinearGradientBrush brush)
            {
                var targetOpacity = show ? (byte)255 : (byte)80;
                var newBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                if (show)
                {
                    newBrush.GradientStops.Add(new GradientStop(GlowColor, 0));
                    newBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 229, 255), 0.5));
                    newBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 179, 102, 255), 1));
                }
                else
                {
                    newBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, GlowColor.R, GlowColor.G, GlowColor.B), 0));
                    newBrush.GradientStops.Add(new GradientStop(Color.FromArgb(48, 0, 229, 255), 0.5));
                    newBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 179, 102, 255), 1));
                }

                BorderBrush = newBrush;
            }
        }

        /// <summary>
        /// Triggers a pop animation for attention
        /// </summary>
        public void Pop()
        {
            var scaleUpAnim = new DoubleAnimationUsingKeyFrames();
            scaleUpAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            });
            scaleUpAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));

            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnim);
            _scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpAnim);
        }
    }
}
