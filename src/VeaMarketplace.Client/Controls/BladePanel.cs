using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace VeaMarketplace.Client.Controls
{
    /// <summary>
    /// Xbox 360 Guide-inspired blade panel with slide animations and neon glow effects.
    /// Panels slide in from the left with depth shadow and gradient borders.
    /// </summary>
    public class BladePanel : ContentControl
    {
        static BladePanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BladePanel),
                new FrameworkPropertyMetadata(typeof(BladePanel)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(BladePanel),
                new PropertyMetadata(false, OnIsOpenChanged));

        public static readonly DependencyProperty BladeColorProperty =
            DependencyProperty.Register(nameof(BladeColor), typeof(Color), typeof(BladePanel),
                new PropertyMetadata(Color.FromArgb(255, 0, 255, 159)));

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(object), typeof(BladePanel),
                new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderIconProperty =
            DependencyProperty.Register(nameof(HeaderIcon), typeof(string), typeof(BladePanel),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SlideDirectionProperty =
            DependencyProperty.Register(nameof(SlideDirection), typeof(SlideDirection), typeof(BladePanel),
                new PropertyMetadata(SlideDirection.Left));

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.Register(nameof(GlowIntensity), typeof(double), typeof(BladePanel),
                new PropertyMetadata(0.6));

        public static readonly DependencyProperty DepthShadowProperty =
            DependencyProperty.Register(nameof(DepthShadow), typeof(double), typeof(BladePanel),
                new PropertyMetadata(15.0));

        #endregion

        #region Properties

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public Color BladeColor
        {
            get => (Color)GetValue(BladeColorProperty);
            set => SetValue(BladeColorProperty, value);
        }

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string HeaderIcon
        {
            get => (string)GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        public SlideDirection SlideDirection
        {
            get => (SlideDirection)GetValue(SlideDirectionProperty);
            set => SetValue(SlideDirectionProperty, value);
        }

        public double GlowIntensity
        {
            get => (double)GetValue(GlowIntensityProperty);
            set => SetValue(GlowIntensityProperty, value);
        }

        public double DepthShadow
        {
            get => (double)GetValue(DepthShadowProperty);
            set => SetValue(DepthShadowProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler Opened;
        public event EventHandler Closed;

        #endregion

        public BladePanel()
        {
            Loaded += OnLoaded;
            RenderTransform = new TranslateTransform();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyBladeStyle();
            if (IsOpen)
            {
                AnimateOpen(false);
            }
            else
            {
                SetClosedState();
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BladePanel panel)
            {
                if ((bool)e.NewValue)
                {
                    panel.AnimateOpen(true);
                }
                else
                {
                    panel.AnimateClose();
                }
            }
        }

        private void ApplyBladeStyle()
        {
            // Apply gradient background
            var bgGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 21, 32, 45), 0));
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 26, 40, 56), 0.5));
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 15, 24, 32), 1));
            Background = bgGradient;

            // Apply border gradient with blade color
            var borderGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            var glowColor = Color.FromArgb(100, BladeColor.R, BladeColor.G, BladeColor.B);
            borderGradient.GradientStops.Add(new GradientStop(glowColor, 0));
            borderGradient.GradientStops.Add(new GradientStop(Color.FromArgb(75, 0, 229, 255), 0.5));
            borderGradient.GradientStops.Add(new GradientStop(glowColor, 1));
            BorderBrush = borderGradient;
            BorderThickness = new Thickness(0, 1, 2, 1);

            // Apply drop shadow
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 30,
                ShadowDepth = DepthShadow,
                Opacity = 0.7,
                Direction = 270
            };
        }

        private void AnimateOpen(bool animate)
        {
            Visibility = Visibility.Visible;

            if (!animate)
            {
                Opacity = 1;
                if (RenderTransform is TranslateTransform translate)
                {
                    translate.X = 0;
                }
                Opened?.Invoke(this, EventArgs.Empty);
                return;
            }

            var slideDistance = SlideDirection == SlideDirection.Left ? -300 : 300;

            var slideAnim = new DoubleAnimation
            {
                From = slideDistance,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250)
            };

            slideAnim.Completed += (s, e) => Opened?.Invoke(this, EventArgs.Empty);

            if (RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
            BeginAnimation(OpacityProperty, fadeAnim);

            // Animate glow intensify
            if (Effect is DropShadowEffect shadow)
            {
                var glowAnim = new DoubleAnimation
                {
                    To = 35,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, glowAnim);
            }
        }

        private void AnimateClose()
        {
            var slideDistance = SlideDirection == SlideDirection.Left ? -300 : 300;

            var slideAnim = new DoubleAnimation
            {
                To = slideDistance,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            slideAnim.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                Closed?.Invoke(this, EventArgs.Empty);
            };

            if (RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
            BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void SetClosedState()
        {
            Opacity = 0;
            var slideDistance = SlideDirection == SlideDirection.Left ? -300 : 300;
            if (RenderTransform is TranslateTransform translate)
            {
                translate.X = slideDistance;
            }
            Visibility = Visibility.Collapsed;
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
        }
    }

    public enum SlideDirection
    {
        Left,
        Right
    }
}
