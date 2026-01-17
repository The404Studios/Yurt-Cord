using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Client.Controls;

/// <summary>
/// Animated message bubble for Overseer with flying animation, envelope transformation, and rainbow text
/// </summary>
public class OverseerMessageBubble : UserControl
{
    private Border _container = null!;
    private Border _avatarBorder = null!;
    private TextBlock _usernameText = null!;
    private TextBlock _timestampText = null!;
    private TextBlock _messageText = null!;
    private Canvas _envelopeCanvas = null!;
    private Grid _mainGrid = null!;
    private StackPanel _contentStack = null!;
    private StackPanel _actionsPanel = null!;
    private DispatcherTimer? _transformTimer;
    private bool _hasTransformed;
    private ChatMessageDto? _currentMessage;

    public static readonly DependencyProperty EnableAnimationsProperty =
        DependencyProperty.Register(nameof(EnableAnimations), typeof(bool), typeof(OverseerMessageBubble),
            new PropertyMetadata(true));

    public bool EnableAnimations
    {
        get => (bool)GetValue(EnableAnimationsProperty);
        set => SetValue(EnableAnimationsProperty, value);
    }

    // Events for parent to handle
    public static readonly RoutedEvent ReplyRequestedEvent = EventManager.RegisterRoutedEvent(
        "ReplyRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OverseerMessageBubble));

    public event RoutedEventHandler ReplyRequested
    {
        add => AddHandler(ReplyRequestedEvent, value);
        remove => RemoveHandler(ReplyRequestedEvent, value);
    }

    public OverseerMessageBubble()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OverseerMessageBubble_Loaded;
        Unloaded += OverseerMessageBubble_Unloaded;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    private void InitializeComponent()
    {
        _mainGrid = new Grid();
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Create avatar
        _avatarBorder = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 204, 255)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 204, 255),
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
        _avatarBorder.Background = new LinearGradientBrush(
            Color.FromRgb(0, 255, 136),
            Color.FromRgb(0, 204, 255),
            45);
        Grid.SetColumn(_avatarBorder, 0);

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
            MaxWidth = 500,
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.4
            }
        };

        // Neon border gradient
        _container.BorderBrush = new LinearGradientBrush(
            Color.FromArgb(80, 0, 255, 136),
            Color.FromArgb(80, 0, 204, 255),
            45);

        // Dark background with subtle gradient
        _container.Background = new LinearGradientBrush(
            Color.FromArgb(220, 20, 20, 35),
            Color.FromArgb(220, 25, 25, 45),
            135);

        _contentStack = new StackPanel();

        // Header row (username + timestamp)
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _usernameText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
        Grid.SetColumn(_usernameText, 0);
        headerGrid.Children.Add(_usernameText);

        _timestampText = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 140)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_timestampText, 2);
        headerGrid.Children.Add(_timestampText);

        _contentStack.Children.Add(headerGrid);

        // Message text with rainbow effect
        _messageText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Arial"),
            FontSize = 14,
            Margin = new Thickness(0, 6, 0, 0)
        };
        _contentStack.Children.Add(_messageText);

        // Actions panel (reply, react, etc.) - shown on hover
        _actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        CreateActionButtons();
        _contentStack.Children.Add(_actionsPanel);

        _container.Child = _contentStack;

        // Add to main grid
        var messageContainer = new Grid();
        messageContainer.Children.Add(_envelopeCanvas);
        messageContainer.Children.Add(_container);
        Grid.SetColumn(messageContainer, 1);

        _mainGrid.Children.Add(_avatarBorder);
        _mainGrid.Children.Add(messageContainer);

        Content = _mainGrid;
    }

    private void CreateActionButtons()
    {
        var buttonStyle = CreateActionButtonStyle();

        var replyButton = new Button
        {
            Content = new TextBlock { Text = ">", FontSize = 10 },
            ToolTip = "Reply",
            Width = 28,
            Height = 28,
            Style = buttonStyle,
            Margin = new Thickness(0, 0, 4, 0)
        };
        replyButton.Click += (s, e) =>
        {
            if (_currentMessage != null)
                RaiseEvent(new RoutedEventArgs(ReplyRequestedEvent, _currentMessage));
        };

        var reactButton = new Button
        {
            Content = new TextBlock { Text = "+", FontSize = 10 },
            ToolTip = "React",
            Width = 28,
            Height = 28,
            Style = buttonStyle,
            Margin = new Thickness(0, 0, 4, 0)
        };
        reactButton.Click += ShowReactionPicker;

        var copyButton = new Button
        {
            Content = new TextBlock { Text = "[]", FontSize = 9 },
            ToolTip = "Copy",
            Width = 28,
            Height = 28,
            Style = buttonStyle
        };
        copyButton.Click += (s, e) =>
        {
            if (_currentMessage != null)
            {
                Clipboard.SetText(_currentMessage.Content);
                var toast = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
                toast?.ShowInfo("COPIED", "Message copied to clipboard");
            }
        };

        _actionsPanel.Children.Add(replyButton);
        _actionsPanel.Children.Add(reactButton);
        _actionsPanel.Children.Add(copyButton);
    }

    private Style CreateActionButtonStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 255, 136))));
        style.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(Color.FromArgb(100, 0, 255, 136))));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(CursorProperty, System.Windows.Input.Cursors.Hand));

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;
        style.Setters.Add(new Setter(TemplateProperty, template));

        return style;
    }

    private void ShowReactionPicker(object sender, RoutedEventArgs e)
    {
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = (Button)sender,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(240, 20, 20, 35)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 136)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var emojis = new[] { "👍", "❤️", "😂", "😮", "😢", "🎉", "🔥", "👀" };

        foreach (var emoji in emojis)
        {
            var btn = new Button
            {
                Content = emoji,
                FontSize = 16,
                Width = 32,
                Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += async (s, ev) =>
            {
                popup.IsOpen = false;
                if (_currentMessage != null)
                {
                    var chatService = (IChatService?)App.ServiceProvider.GetService(typeof(IChatService));
                    if (chatService != null)
                    {
                        await chatService.AddReactionAsync(_currentMessage.Id, emoji);
                    }
                }
            };
            panel.Children.Add(btn);
        }

        border.Child = panel;
        popup.Child = border;
        popup.IsOpen = true;
    }

    private void CreateEnvelopeGraphics()
    {
        // Envelope body with neon gradient
        var body = new Rectangle
        {
            Width = 50,
            Height = 35,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0, 255, 136),
                Color.FromRgb(0, 204, 255),
                45),
            RadiusX = 5,
            RadiusY = 5,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 136),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.8
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
                new Point(30, 22),
                new Point(55, 5)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(0, 255, 200),
                Color.FromRgb(0, 180, 255),
                90)
        };
        _envelopeCanvas.Children.Add(flap);

        // Digital seal
        var seal = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new RadialGradientBrush(
                Color.FromRgb(255, 0, 255),
                Color.FromRgb(150, 0, 255)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 0, 255),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };
        Canvas.SetLeft(seal, 23);
        Canvas.SetTop(seal, 13);
        _envelopeCanvas.Children.Add(seal);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ChatMessageDto message)
        {
            _currentMessage = message;
            UpdateUI(message);
        }
    }

    private void UpdateUI(ChatMessageDto message)
    {
        // Set avatar
        if (!string.IsNullOrEmpty(message.SenderAvatarUrl))
        {
            try
            {
                var imageBrush = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(message.SenderAvatarUrl)),
                    Stretch = Stretch.UniformToFill
                };
                _avatarBorder.Background = imageBrush;
            }
            catch
            {
                _avatarBorder.Background = new LinearGradientBrush(
                    GetRoleColor(message.SenderRole),
                    Color.FromRgb(0, 204, 255),
                    45);
            }
        }

        // Set username with role color
        _usernameText.Text = message.SenderUsername;
        var roleColor = GetRoleColor(message.SenderRole);
        _usernameText.Foreground = new SolidColorBrush(roleColor);
        if (_usernameText.Effect is DropShadowEffect shadow)
        {
            shadow.Color = roleColor;
        }

        // Update avatar border color to match role
        _avatarBorder.BorderBrush = new SolidColorBrush(roleColor);
        if (_avatarBorder.Effect is DropShadowEffect avatarShadow)
        {
            avatarShadow.Color = roleColor;
        }

        // Set timestamp
        _timestampText.Text = FormatTimestamp(message.Timestamp);

        // Handle system messages differently
        if (message.Type != MessageType.Text)
        {
            HandleSystemMessage(message);
            return;
        }

        // Apply rainbow text effect for normal messages
        ApplyRainbowText(message.Content);
    }

    private void HandleSystemMessage(ChatMessageDto message)
    {
        _messageText.Inlines.Clear();
        _messageText.FontStyle = FontStyles.Italic;
        _avatarBorder.Visibility = Visibility.Collapsed;

        switch (message.Type)
        {
            case MessageType.Join:
                _messageText.Text = $">> {message.Content}";
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136));
                break;
            case MessageType.Leave:
                _messageText.Text = $"<< {message.Content}";
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                break;
            case MessageType.Announcement:
                _messageText.FontWeight = FontWeights.Bold;
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                _messageText.Text = $"[BROADCAST] {message.Content}";
                break;
            default:
                _messageText.Text = message.Content;
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 200));
                break;
        }
    }

    private void ApplyRainbowText(string content)
    {
        _messageText.Inlines.Clear();
        _messageText.FontStyle = FontStyles.Normal;
        _avatarBorder.Visibility = Visibility.Visible;

        if (string.IsNullOrEmpty(content)) return;

        // Rainbow colors for Overseer theme
        var colors = new[]
        {
            Color.FromRgb(0, 255, 136),    // Neon Green
            Color.FromRgb(0, 230, 180),    // Teal
            Color.FromRgb(0, 204, 255),    // Cyan
            Color.FromRgb(100, 150, 255),  // Light Blue
            Color.FromRgb(180, 100, 255),  // Purple
            Color.FromRgb(255, 0, 255),    // Magenta
            Color.FromRgb(255, 100, 200),  // Pink
            Color.FromRgb(255, 150, 150),  // Light Red
        };

        for (int i = 0; i < content.Length; i++)
        {
            var colorIndex = (i / 3) % colors.Length; // Change color every 3 characters
            var color = colors[colorIndex];

            var run = new System.Windows.Documents.Run(content[i].ToString())
            {
                Foreground = new SolidColorBrush(color)
            };

            _messageText.Inlines.Add(run);
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _actionsPanel.Visibility = Visibility.Visible;

        // Intensify glow on hover
        if (_container.Effect is DropShadowEffect shadow)
        {
            shadow.Opacity = 0.7;
            shadow.BlurRadius = 20;
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _actionsPanel.Visibility = Visibility.Collapsed;

        // Restore normal glow
        if (_container.Effect is DropShadowEffect shadow)
        {
            shadow.Opacity = 0.4;
            shadow.BlurRadius = 15;
        }
    }

    private void OverseerMessageBubble_Loaded(object sender, RoutedEventArgs e)
    {
        if (EnableAnimations && _currentMessage != null)
        {
            PlayEntryAnimation();
            StartTransformTimer();
        }

        // Animate the glow effect
        AnimateGlowColors();
    }

    private void OverseerMessageBubble_Unloaded(object sender, RoutedEventArgs e)
    {
        _transformTimer?.Stop();
        DataContextChanged -= OnDataContextChanged;
        MouseEnter -= OnMouseEnter;
        MouseLeave -= OnMouseLeave;
    }

    private void AnimateGlowColors()
    {
        if (_container.Effect is DropShadowEffect shadow)
        {
            var hueShift = new ColorAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(6),
                RepeatBehavior = RepeatBehavior.Forever
            };

            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 255, 136), KeyTime.FromPercent(0)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 204, 255), KeyTime.FromPercent(0.33)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(255, 0, 255), KeyTime.FromPercent(0.66)));
            hueShift.KeyFrames.Add(new LinearColorKeyFrame(Color.FromRgb(0, 255, 136), KeyTime.FromPercent(1)));

            shadow.BeginAnimation(DropShadowEffect.ColorProperty, hueShift);
        }
    }

    private void PlayEntryAnimation()
    {
        var translateTransform = new TranslateTransform(400, 0);
        var scaleTransform = new ScaleTransform(0.7, 0.7);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translateTransform);
        _container.RenderTransform = transformGroup;
        _container.RenderTransformOrigin = new Point(0.5, 0.5);
        _container.Opacity = 0;

        // Fly in animation
        var flyIn = new DoubleAnimation
        {
            From = 400,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 4 }
        };

        // Fade in
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(350)
        };

        // Scale up
        var scaleUp = new DoubleAnimation
        {
            From = 0.7,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
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
        var scaleTransform = _container.RenderTransform as TransformGroup;
        ScaleTransform? st = null;

        if (scaleTransform != null && scaleTransform.Children.Count > 0)
        {
            st = scaleTransform.Children[0] as ScaleTransform;
        }

        if (st == null)
        {
            st = new ScaleTransform(1, 1);
            _container.RenderTransform = st;
            _container.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var shrinkScale = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        shrinkScale.Completed += (s, e) =>
        {
            _container.Visibility = Visibility.Collapsed;
            _avatarBorder.Visibility = Visibility.Collapsed;
            _envelopeCanvas.Visibility = Visibility.Visible;
            PlayEnvelopeAnimation();
        };

        st.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkScale);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkScale);
    }

    private void PlayEnvelopeAnimation()
    {
        _envelopeCanvas.Opacity = 0;
        var envelopeScale = new ScaleTransform(0.5, 0.5);
        var floatTranslate = new TranslateTransform();
        var tg = new TransformGroup();
        tg.Children.Add(envelopeScale);
        tg.Children.Add(floatTranslate);
        _envelopeCanvas.RenderTransform = tg;
        _envelopeCanvas.RenderTransformOrigin = new Point(0.5, 0.5);

        var popIn = new DoubleAnimation
        {
            From = 0.5,
            To = 1.1,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
        };

        var settleDown = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(150),
            BeginTime = TimeSpan.FromMilliseconds(200)
        };

        var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150) };

        var floatUp = new DoubleAnimation
        {
            From = 0,
            To = -5,
            Duration = TimeSpan.FromMilliseconds(1200),
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase()
        };

        _envelopeCanvas.BeginAnimation(OpacityProperty, fadeIn);
        envelopeScale.BeginAnimation(ScaleTransform.ScaleXProperty, popIn);
        envelopeScale.BeginAnimation(ScaleTransform.ScaleYProperty, popIn);

        popIn.Completed += (s, e) =>
        {
            floatTranslate.BeginAnimation(TranslateTransform.YProperty, floatUp);

            // Click handler to open envelope
            _envelopeCanvas.MouseLeftButtonDown -= OnEnvelopeClick;
            _envelopeCanvas.MouseLeftButtonDown += OnEnvelopeClick;
            _envelopeCanvas.Cursor = System.Windows.Input.Cursors.Hand;
        };
    }

    private void OnEnvelopeClick(object sender, MouseButtonEventArgs e)
    {
        OpenEnvelopeAndShowMessage();
    }

    private void OpenEnvelopeAndShowMessage()
    {
        _envelopeCanvas.MouseLeftButtonDown -= OnEnvelopeClick;

        var fadeOutEnvelope = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        fadeOutEnvelope.Completed += (s, e) =>
        {
            _envelopeCanvas.Visibility = Visibility.Collapsed;
            _container.Visibility = Visibility.Visible;
            _avatarBorder.Visibility = Visibility.Visible;

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

            // Re-enable glow animation
            AnimateGlowColors();
        };

        _envelopeCanvas.BeginAnimation(OpacityProperty, fadeOutEnvelope);
    }

    private static Color GetRoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.FromRgb(255, 215, 0),      // Gold
            UserRole.Admin => Color.FromRgb(255, 68, 68),      // Red
            UserRole.Moderator => Color.FromRgb(180, 100, 255), // Purple
            UserRole.VIP => Color.FromRgb(0, 255, 136),        // Neon Green
            UserRole.Verified => Color.FromRgb(0, 204, 255),   // Cyan
            _ => Color.FromRgb(200, 200, 220)                  // Default gray
        };
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var now = DateTime.Now;
        var local = timestamp.ToLocalTime();

        if (local.Date == now.Date)
            return $"Today {local:HH:mm}";

        if (local.Date == now.Date.AddDays(-1))
            return $"Yesterday {local:HH:mm}";

        return local.ToString("MM/dd HH:mm");
    }
}
