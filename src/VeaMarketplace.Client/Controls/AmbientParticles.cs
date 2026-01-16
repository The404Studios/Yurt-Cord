using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls
{
    /// <summary>
    /// Ambient floating particle system that creates a subtle, atmospheric background effect.
    /// Features glowing orbs that drift slowly across the screen with varying sizes and opacities.
    /// </summary>
    public class AmbientParticles : Canvas
    {
        #region Dependency Properties

        public static readonly DependencyProperty ParticleCountProperty =
            DependencyProperty.Register(nameof(ParticleCount), typeof(int), typeof(AmbientParticles),
                new PropertyMetadata(30, OnParticleCountChanged));

        public static readonly DependencyProperty ParticleColorsProperty =
            DependencyProperty.Register(nameof(ParticleColors), typeof(List<Color>), typeof(AmbientParticles),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MinSizeProperty =
            DependencyProperty.Register(nameof(MinSize), typeof(double), typeof(AmbientParticles),
                new PropertyMetadata(2.0));

        public static readonly DependencyProperty MaxSizeProperty =
            DependencyProperty.Register(nameof(MaxSize), typeof(double), typeof(AmbientParticles),
                new PropertyMetadata(8.0));

        public static readonly DependencyProperty SpeedFactorProperty =
            DependencyProperty.Register(nameof(SpeedFactor), typeof(double), typeof(AmbientParticles),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty GlowIntensityProperty =
            DependencyProperty.Register(nameof(GlowIntensity), typeof(double), typeof(AmbientParticles),
                new PropertyMetadata(0.6));

        public static readonly DependencyProperty IsAnimatingProperty =
            DependencyProperty.Register(nameof(IsAnimating), typeof(bool), typeof(AmbientParticles),
                new PropertyMetadata(true, OnIsAnimatingChanged));

        #endregion

        #region Properties

        public int ParticleCount
        {
            get => (int)GetValue(ParticleCountProperty);
            set => SetValue(ParticleCountProperty, value);
        }

        public List<Color> ParticleColors
        {
            get => (List<Color>)GetValue(ParticleColorsProperty);
            set => SetValue(ParticleColorsProperty, value);
        }

        public double MinSize
        {
            get => (double)GetValue(MinSizeProperty);
            set => SetValue(MinSizeProperty, value);
        }

        public double MaxSize
        {
            get => (double)GetValue(MaxSizeProperty);
            set => SetValue(MaxSizeProperty, value);
        }

        public double SpeedFactor
        {
            get => (double)GetValue(SpeedFactorProperty);
            set => SetValue(SpeedFactorProperty, value);
        }

        public double GlowIntensity
        {
            get => (double)GetValue(GlowIntensityProperty);
            set => SetValue(GlowIntensityProperty, value);
        }

        public bool IsAnimating
        {
            get => (bool)GetValue(IsAnimatingProperty);
            set => SetValue(IsAnimatingProperty, value);
        }

        #endregion

        private readonly List<Particle> _particles = new();
        private readonly Random _random = new();
        private DispatcherTimer _updateTimer;

        private class Particle
        {
            public Ellipse Visual { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double VelocityX { get; set; }
            public double VelocityY { get; set; }
            public double Size { get; set; }
            public double BaseOpacity { get; set; }
            public double OpacityPhase { get; set; }
            public double OpacitySpeed { get; set; }
        }

        public AmbientParticles()
        {
            ClipToBounds = true;
            Background = Brushes.Transparent;
            IsHitTestVisible = false;

            // Default colors - neon palette
            ParticleColors = new List<Color>
            {
                Color.FromArgb(255, 0, 255, 159),   // Neon Green
                Color.FromArgb(255, 0, 229, 255),   // Neon Cyan
                Color.FromArgb(255, 179, 102, 255), // Neon Purple
                Color.FromArgb(255, 255, 102, 178), // Neon Pink
                Color.FromArgb(255, 77, 159, 255),  // Neon Blue
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeParticles();
            if (IsAnimating)
            {
                StartAnimation();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Redistribute particles within new bounds
            foreach (var particle in _particles)
            {
                if (particle.X > ActualWidth)
                    particle.X = _random.NextDouble() * ActualWidth;
                if (particle.Y > ActualHeight)
                    particle.Y = _random.NextDouble() * ActualHeight;
            }
        }

        private static void OnParticleCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AmbientParticles particles && particles.IsLoaded)
            {
                particles.InitializeParticles();
            }
        }

        private static void OnIsAnimatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AmbientParticles particles)
            {
                if ((bool)e.NewValue)
                {
                    particles.StartAnimation();
                }
                else
                {
                    particles.StopAnimation();
                }
            }
        }

        private void InitializeParticles()
        {
            // Clear existing
            Children.Clear();
            _particles.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            for (int i = 0; i < ParticleCount; i++)
            {
                CreateParticle();
            }
        }

        private void CreateParticle()
        {
            var size = MinSize + _random.NextDouble() * (MaxSize - MinSize);
            var color = ParticleColors[_random.Next(ParticleColors.Count)];
            var baseOpacity = 0.2 + _random.NextDouble() * 0.4;

            // Create gradient brush for soft glow effect
            var gradientBrush = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * GlowIntensity), color.R, color.G, color.B), 0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(128 * GlowIntensity), color.R, color.G, color.B), 0.5));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));

            var ellipse = new Ellipse
            {
                Width = size * 3, // Multiply for soft glow area
                Height = size * 3,
                Fill = gradientBrush,
                Opacity = baseOpacity
            };

            var particle = new Particle
            {
                Visual = ellipse,
                X = _random.NextDouble() * Math.Max(1, ActualWidth),
                Y = _random.NextDouble() * Math.Max(1, ActualHeight),
                VelocityX = (_random.NextDouble() - 0.5) * 0.3 * SpeedFactor,
                VelocityY = (_random.NextDouble() - 0.5) * 0.3 * SpeedFactor,
                Size = size,
                BaseOpacity = baseOpacity,
                OpacityPhase = _random.NextDouble() * Math.PI * 2,
                OpacitySpeed = 0.5 + _random.NextDouble() * 1.5
            };

            Canvas.SetLeft(ellipse, particle.X - size * 1.5);
            Canvas.SetTop(ellipse, particle.Y - size * 1.5);

            Children.Add(ellipse);
            _particles.Add(particle);
        }

        private void StartAnimation()
        {
            if (_updateTimer != null) return;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _updateTimer.Tick += UpdateParticles;
            _updateTimer.Start();
        }

        private void StopAnimation()
        {
            _updateTimer?.Stop();
            _updateTimer = null;
        }

        private void UpdateParticles(object sender, EventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            var time = DateTime.Now.TimeOfDay.TotalSeconds;

            foreach (var particle in _particles)
            {
                // Update position
                particle.X += particle.VelocityX;
                particle.Y += particle.VelocityY;

                // Add slight drift/wobble
                particle.X += Math.Sin(time * particle.OpacitySpeed + particle.OpacityPhase) * 0.1;
                particle.Y += Math.Cos(time * particle.OpacitySpeed * 0.7 + particle.OpacityPhase) * 0.1;

                // Wrap around edges
                if (particle.X < -particle.Size * 2)
                    particle.X = ActualWidth + particle.Size;
                else if (particle.X > ActualWidth + particle.Size * 2)
                    particle.X = -particle.Size;

                if (particle.Y < -particle.Size * 2)
                    particle.Y = ActualHeight + particle.Size;
                else if (particle.Y > ActualHeight + particle.Size * 2)
                    particle.Y = -particle.Size;

                // Update visual position
                Canvas.SetLeft(particle.Visual, particle.X - particle.Size * 1.5);
                Canvas.SetTop(particle.Visual, particle.Y - particle.Size * 1.5);

                // Subtle opacity pulsing
                var opacityWave = Math.Sin(time * particle.OpacitySpeed + particle.OpacityPhase);
                particle.Visual.Opacity = particle.BaseOpacity + opacityWave * 0.15;
            }
        }

        /// <summary>
        /// Creates a burst of particles from a specific point
        /// </summary>
        public void Burst(Point origin, int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                var size = MinSize + _random.NextDouble() * (MaxSize - MinSize);
                var color = ParticleColors[_random.Next(ParticleColors.Count)];
                var angle = _random.NextDouble() * Math.PI * 2;
                var speed = 2 + _random.NextDouble() * 3;

                var gradientBrush = new RadialGradientBrush();
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, color.R, color.G, color.B), 0));
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));

                var ellipse = new Ellipse
                {
                    Width = size * 4,
                    Height = size * 4,
                    Fill = gradientBrush,
                    Opacity = 1
                };

                Canvas.SetLeft(ellipse, origin.X - size * 2);
                Canvas.SetTop(ellipse, origin.Y - size * 2);
                Children.Add(ellipse);

                // Animate burst particle
                var duration = TimeSpan.FromMilliseconds(600 + _random.NextDouble() * 400);

                var xAnim = new DoubleAnimation
                {
                    To = origin.X + Math.Cos(angle) * 100 * speed - size * 2,
                    Duration = duration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var yAnim = new DoubleAnimation
                {
                    To = origin.Y + Math.Sin(angle) * 100 * speed - size * 2,
                    Duration = duration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var fadeAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = duration
                };

                var scaleAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = duration
                };

                fadeAnim.Completed += (s, e) => Children.Remove(ellipse);

                ellipse.BeginAnimation(Canvas.LeftProperty, xAnim);
                ellipse.BeginAnimation(Canvas.TopProperty, yAnim);
                ellipse.BeginAnimation(OpacityProperty, fadeAnim);
            }
        }

        /// <summary>
        /// Temporarily increases particle glow intensity
        /// </summary>
        public void PulseGlow()
        {
            foreach (var particle in _particles)
            {
                var pulseAnim = new DoubleAnimation
                {
                    To = Math.Min(particle.BaseOpacity * 2, 1),
                    Duration = TimeSpan.FromMilliseconds(200),
                    AutoReverse = true
                };
                particle.Visual.BeginAnimation(OpacityProperty, pulseAnim);
            }
        }
    }
}
