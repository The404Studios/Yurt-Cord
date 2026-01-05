using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

/// <summary>
/// Matrix-style falling characters background effect for Overseer
/// </summary>
public class MatrixBackground : Canvas
{
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private readonly List<MatrixColumn> _columns = new();
    private readonly string _matrixChars = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ<>{}[]|\\/*-+=$@#%&";

    private int _columnCount;
    private bool _isInitialized;

    public static readonly DependencyProperty CharColorProperty =
        DependencyProperty.Register(nameof(CharColor), typeof(Color), typeof(MatrixBackground),
            new PropertyMetadata(Color.FromRgb(0, 255, 65)));

    public static readonly DependencyProperty GlowColorProperty =
        DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(MatrixBackground),
            new PropertyMetadata(Color.FromRgb(150, 255, 150)));

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(nameof(Speed), typeof(double), typeof(MatrixBackground),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty DensityProperty =
        DependencyProperty.Register(nameof(Density), typeof(double), typeof(MatrixBackground),
            new PropertyMetadata(0.8));

    public Color CharColor
    {
        get => (Color)GetValue(CharColorProperty);
        set => SetValue(CharColorProperty, value);
    }

    public Color GlowColor
    {
        get => (Color)GetValue(GlowColorProperty);
        set => SetValue(GlowColorProperty, value);
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public double Density
    {
        get => (double)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public MatrixBackground()
    {
        Background = new SolidColorBrush(Color.FromRgb(5, 5, 10));
        ClipToBounds = true;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += Timer_Tick;

        Loaded += MatrixBackground_Loaded;
        Unloaded += MatrixBackground_Unloaded;
        SizeChanged += MatrixBackground_SizeChanged;
    }

    private void MatrixBackground_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeColumns();
        _timer.Start();
    }

    private void MatrixBackground_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void MatrixBackground_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeColumns();
        }
    }

    private void InitializeColumns()
    {
        Children.Clear();
        _columns.Clear();

        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double columnWidth = 14;
        _columnCount = (int)(ActualWidth / columnWidth);

        for (int i = 0; i < _columnCount; i++)
        {
            if (_random.NextDouble() > Density) continue;

            var column = new MatrixColumn
            {
                X = i * columnWidth,
                Y = _random.Next(-(int)ActualHeight, 0),
                Speed = _random.Next(3, 12) * Speed,
                Length = _random.Next(8, 25),
                Characters = new List<TextBlock>()
            };

            _columns.Add(column);
        }

        _isInitialized = true;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        foreach (var column in _columns)
        {
            column.Y += column.Speed;

            // Reset column when it goes off screen
            if (column.Y > ActualHeight + (column.Length * 16))
            {
                column.Y = _random.Next(-(int)ActualHeight / 2, 0);
                column.Speed = _random.Next(3, 12) * Speed;
                column.Length = _random.Next(8, 25);

                // Remove old characters
                foreach (var tb in column.Characters)
                {
                    Children.Remove(tb);
                }
                column.Characters.Clear();
            }

            UpdateColumnCharacters(column);
        }
    }

    private void UpdateColumnCharacters(MatrixColumn column)
    {
        // Remove characters that are too old
        while (column.Characters.Count > column.Length)
        {
            var oldChar = column.Characters[0];
            Children.Remove(oldChar);
            column.Characters.RemoveAt(0);
        }

        // Add new character at head position
        if (_random.NextDouble() > 0.3) // Don't add every frame for variety
        {
            var newChar = CreateMatrixChar(column);
            column.Characters.Add(newChar);
            Children.Add(newChar);
        }

        // Update positions and opacity
        for (int i = 0; i < column.Characters.Count; i++)
        {
            var tb = column.Characters[i];
            double yPos = column.Y - (column.Characters.Count - i - 1) * 16;
            SetTop(tb, yPos);

            // Fade based on position in trail
            double opacity = (double)(i + 1) / column.Characters.Count;

            // Head character is brightest (white/glow)
            if (i == column.Characters.Count - 1)
            {
                tb.Foreground = new SolidColorBrush(GlowColor);
                tb.Opacity = 1.0;

                // Add glow effect to head
                tb.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = GlowColor,
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
            }
            else
            {
                tb.Foreground = new SolidColorBrush(CharColor);
                tb.Opacity = opacity * 0.7;
                tb.Effect = null;
            }

            // Randomly change character occasionally
            if (_random.NextDouble() > 0.95)
            {
                tb.Text = _matrixChars[_random.Next(_matrixChars.Length)].ToString();
            }
        }
    }

    private TextBlock CreateMatrixChar(MatrixColumn column)
    {
        var tb = new TextBlock
        {
            Text = _matrixChars[_random.Next(_matrixChars.Length)].ToString(),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(GlowColor)
        };

        SetLeft(tb, column.X);
        SetTop(tb, column.Y);

        return tb;
    }

    private class MatrixColumn
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Speed { get; set; }
        public int Length { get; set; }
        public List<TextBlock> Characters { get; set; } = new();
    }
}
