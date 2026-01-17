using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Cursors = System.Windows.Input.Cursors;

namespace VeaMarketplace.Client.Controls;

/// <summary>
/// Origami-styled message card with 3D pop-out effects, blade-style neon accents,
/// and Xbox 360 Guide Menu-inspired animations
/// </summary>
public class OrigamiMessageCard : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ElevationProperty =
        DependencyProperty.Register(nameof(Elevation), typeof(double), typeof(OrigamiMessageCard),
            new PropertyMetadata(2.0, OnElevationChanged));

    public static readonly DependencyProperty IsOwnMessageProperty =
        DependencyProperty.Register(nameof(IsOwnMessage), typeof(bool), typeof(OrigamiMessageCard),
            new PropertyMetadata(false, OnIsOwnMessageChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(OrigamiMessageCard),
            new PropertyMetadata(Color.FromRgb(0, 255, 159), OnAccentColorChanged));

    public double Elevation
    {
        get => (double)GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    public bool IsOwnMessage
    {
        get => (bool)GetValue(IsOwnMessageProperty);
        set => SetValue(IsOwnMessageProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    #endregion

    private Border _cardContainer = null!;
    private Border _avatarContainer = null!;
    private Border _accentStripe = null!;
    private TextBlock _usernameText = null!;
    private TextBlock _timestampText = null!;
    private TextBlock _messageText = null!;
    private StackPanel _actionsPanel = null!;
    private Grid _mainGrid = null!;
    private Border _foldCorner = null!;
    private ChatMessageDto? _currentMessage;

    // Transform for 3D pop-out effect
    private ScaleTransform _scaleTransform = null!;
    private TranslateTransform _translateTransform = null!;
    private DropShadowEffect _shadowEffect = null!;

    public static readonly RoutedEvent ReplyRequestedEvent = EventManager.RegisterRoutedEvent(
        "ReplyRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OrigamiMessageCard));

    public event RoutedEventHandler ReplyRequested
    {
        add => AddHandler(ReplyRequestedEvent, value);
        remove => RemoveHandler(ReplyRequestedEvent, value);
    }

    public OrigamiMessageCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        _mainGrid = new Grid
        {
            Margin = new Thickness(0, 4, 0, 4)
        };
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Initialize transforms for 3D effect
        _scaleTransform = new ScaleTransform(1, 1);
        _translateTransform = new TranslateTransform(0, 0);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_scaleTransform);
        transformGroup.Children.Add(_translateTransform);

        // Shadow effect for depth
        _shadowEffect = new DropShadowEffect
        {
            Color = Color.FromRgb(0, 255, 159),
            BlurRadius = 12,
            ShadowDepth = 4,
            Direction = 315,
            Opacity = 0.5
        };

        // Avatar container with blade-style glow
        _avatarContainer = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            BorderThickness = new Thickness(2),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        _avatarContainer.BorderBrush = new LinearGradientBrush(
            Color.FromRgb(0, 255, 159),
            Color.FromRgb(0, 229, 255),
            45);
        _avatarContainer.Background = new LinearGradientBrush(
            Color.FromRgb(0, 255, 159),
            Color.FromRgb(0, 229, 255),
            45);
        _avatarContainer.Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(0, 255, 159),
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.6
        };
        Grid.SetColumn(_avatarContainer, 0);

        // Card container with origami styling
        _cardContainer = new Border
        {
            CornerRadius = new CornerRadius(4, 16, 16, 16),
            Padding = new Thickness(0),
            MaxWidth = 560,
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = transformGroup,
            RenderTransformOrigin = new Point(0, 0.5),
            Effect = _shadowEffect,
            ClipToBounds = false
        };

        // Dark gradient background
        _cardContainer.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(245, 8, 12, 24), 0),
                new GradientStop(Color.FromArgb(245, 12, 18, 32), 0.5),
                new GradientStop(Color.FromArgb(245, 8, 12, 24), 1)
            }
        };

        // Neon border
        _cardContainer.BorderThickness = new Thickness(1);
        _cardContainer.BorderBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(120, 0, 255, 159), 0),
                new GradientStop(Color.FromArgb(60, 0, 229, 255), 0.5),
                new GradientStop(Color.FromArgb(120, 179, 102, 255), 1)
            }
        };

        // Inner content grid
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Accent stripe (blade-style left edge)
        _accentStripe = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(4, 0, 0, 4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0, 255, 159), 0),
                    new GradientStop(Color.FromRgb(0, 229, 255), 0.5),
                    new GradientStop(Color.FromRgb(179, 102, 255), 1)
                }
            }
        };
        Grid.SetColumn(_accentStripe, 0);
        Grid.SetRowSpan(_accentStripe, 3);
        contentGrid.Children.Add(_accentStripe);

        // Content wrapper
        var contentWrapper = new StackPanel
        {
            Margin = new Thickness(14, 12, 16, 12)
        };
        Grid.SetColumn(contentWrapper, 1);
        Grid.SetRowSpan(contentWrapper, 3);

        // Header row
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _usernameText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 159)),
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 159),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };
        Grid.SetColumn(_usernameText, 0);
        headerGrid.Children.Add(_usernameText);

        _timestampText = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 140)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(_timestampText, 2);
        headerGrid.Children.Add(_timestampText);

        contentWrapper.Children.Add(headerGrid);

        // Message text with gradient effect
        _messageText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 225, 240)),
            Margin = new Thickness(0, 8, 0, 0),
            LineHeight = 22
        };
        contentWrapper.Children.Add(_messageText);

        // Actions panel (shown on hover)
        _actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            Opacity = 0
        };
        CreateActionButtons();
        contentWrapper.Children.Add(_actionsPanel);

        contentGrid.Children.Add(contentWrapper);

        // Origami fold corner
        _foldCorner = new Border
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -1, -1, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(200, 0, 255, 159), 0.5),
                    new GradientStop(Color.FromArgb(255, 0, 229, 255), 1)
                }
            },
            CornerRadius = new CornerRadius(0, 16, 0, 12),
            Opacity = 0.8
        };
        Grid.SetColumn(_foldCorner, 1);
        contentGrid.Children.Add(_foldCorner);

        _cardContainer.Child = contentGrid;

        // Add to main grid
        var cardWrapper = new Grid();
        cardWrapper.Children.Add(_cardContainer);
        Grid.SetColumn(cardWrapper, 1);

        _mainGrid.Children.Add(_avatarContainer);
        _mainGrid.Children.Add(cardWrapper);

        // Wire up mouse events for 3D effect
        _cardContainer.MouseEnter += OnCardMouseEnter;
        _cardContainer.MouseLeave += OnCardMouseLeave;

        Content = _mainGrid;
    }

    private void CreateActionButtons()
    {
        var buttons = new[]
        {
            ("↩", "Reply", (Action<object, RoutedEventArgs>)((s, e) => {
                if (_currentMessage != null)
                    RaiseEvent(new RoutedEventArgs(ReplyRequestedEvent, _currentMessage));
            })),
            ("+", "React", (Action<object, RoutedEventArgs>)ShowReactionPicker),
            ("⎘", "Copy", (Action<object, RoutedEventArgs>)((s, e) => {
                if (_currentMessage != null)
                {
                    Clipboard.SetText(_currentMessage.Content);
                    var toast = (IToastNotificationService?)App.ServiceProvider?.GetService(typeof(IToastNotificationService));
                    toast?.ShowInfo("COPIED", "Message copied");
                }
            }))
        };

        foreach (var (icon, tooltip, handler) in buttons)
        {
            var btn = CreateBladeActionButton(icon, tooltip);
            btn.Click += new RoutedEventHandler(handler);
            _actionsPanel.Children.Add(btn);
        }
    }

    private Button CreateBladeActionButton(string icon, string tooltip)
    {
        var btn = new Button
        {
            Width = 30,
            Height = 30,
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 255, 159)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 159)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 255, 159)),
            BorderThickness = new Thickness(1)
        };

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;
        btn.Template = template;
        btn.Content = new TextBlock { Text = icon, FontSize = 12 };

        return btn;
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
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(250, 8, 12, 24)),
            BorderBrush = new LinearGradientBrush(
                Color.FromRgb(0, 255, 159),
                Color.FromRgb(179, 102, 255),
                45),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 159),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var emojis = new[] { "👍", "❤️", "😂", "😮", "😢", "🎉", "🔥", "👀" };

        foreach (var emoji in emojis)
        {
            var btn = new Button
            {
                Content = emoji,
                FontSize = 18,
                Width = 36,
                Height = 36,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2)
            };

            btn.MouseEnter += (s, ev) => btn.Background = new SolidColorBrush(Color.FromArgb(40, 0, 255, 159));
            btn.MouseLeave += (s, ev) => btn.Background = Brushes.Transparent;

            btn.Click += async (s, ev) =>
            {
                popup.IsOpen = false;
                if (_currentMessage != null)
                {
                    var chatService = (IChatService?)App.ServiceProvider?.GetService(typeof(IChatService));
                    if (chatService != null)
                        await chatService.AddReactionAsync(_currentMessage.Id, emoji);
                }
            };
            panel.Children.Add(btn);
        }

        border.Child = panel;
        popup.Child = border;
        popup.IsOpen = true;
    }

    private void OnCardMouseEnter(object sender, MouseEventArgs e)
    {
        // Pop forward effect
        var scaleAnim = new DoubleAnimation
        {
            To = 1.02,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var translateAnim = new DoubleAnimation
        {
            To = -3,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var shadowBlurAnim = new DoubleAnimation
        {
            To = 24,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        var shadowDepthAnim = new DoubleAnimation
        {
            To = 8,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        var actionsOpacityAnim = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        _translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        _shadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlurAnim);
        _shadowEffect.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepthAnim);
        _actionsPanel.BeginAnimation(OpacityProperty, actionsOpacityAnim);
    }

    private void OnCardMouseLeave(object sender, MouseEventArgs e)
    {
        // Return to normal
        var scaleAnim = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var translateAnim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var shadowBlurAnim = new DoubleAnimation
        {
            To = 12,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        var shadowDepthAnim = new DoubleAnimation
        {
            To = 4,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        var actionsOpacityAnim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        _translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
        _shadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlurAnim);
        _shadowEffect.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepthAnim);
        _actionsPanel.BeginAnimation(OpacityProperty, actionsOpacityAnim);
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
                _avatarContainer.Background = imageBrush;
            }
            catch
            {
                SetDefaultAvatar(message.SenderRole);
            }
        }
        else
        {
            SetDefaultAvatar(message.SenderRole);
        }

        // Set role color
        var roleColor = GetRoleColor(message.SenderRole);
        AccentColor = roleColor;
        UpdateAccentColors(roleColor);

        // Set username
        _usernameText.Text = message.SenderUsername;
        _usernameText.Foreground = new SolidColorBrush(roleColor);
        if (_usernameText.Effect is DropShadowEffect shadow)
            shadow.Color = roleColor;

        // Set timestamp
        _timestampText.Text = FormatTimestamp(message.Timestamp);

        // Handle system messages
        if (message.Type != MessageType.Text)
        {
            HandleSystemMessage(message);
            return;
        }

        // Set message text
        _messageText.Text = message.Content;
        _messageText.FontStyle = FontStyles.Normal;
        _avatarContainer.Visibility = Visibility.Visible;
    }

    private void SetDefaultAvatar(UserRole role)
    {
        var roleColor = GetRoleColor(role);
        _avatarContainer.Background = new LinearGradientBrush(
            roleColor,
            Color.FromRgb(0, 229, 255),
            45);
    }

    private void UpdateAccentColors(Color color)
    {
        // Update accent stripe
        _accentStripe.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(color, 0),
                new GradientStop(Color.FromRgb(0, 229, 255), 0.5),
                new GradientStop(Color.FromRgb(179, 102, 255), 1)
            }
        };

        // Update avatar border
        _avatarContainer.BorderBrush = new SolidColorBrush(color);
        if (_avatarContainer.Effect is DropShadowEffect avatarShadow)
            avatarShadow.Color = color;

        // Update card shadow
        _shadowEffect.Color = color;

        // Update fold corner
        _foldCorner.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(1, 0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(200, color.R, color.G, color.B), 0.5),
                new GradientStop(Color.FromArgb(255, 0, 229, 255), 1)
            }
        };
    }

    private void HandleSystemMessage(ChatMessageDto message)
    {
        _avatarContainer.Visibility = Visibility.Collapsed;
        _messageText.FontStyle = FontStyles.Italic;

        switch (message.Type)
        {
            case MessageType.Join:
                _messageText.Text = $"⬤ {message.Content}";
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 159));
                UpdateAccentColors(Color.FromRgb(0, 255, 159));
                break;
            case MessageType.Leave:
                _messageText.Text = $"○ {message.Content}";
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 178));
                UpdateAccentColors(Color.FromRgb(255, 102, 178));
                break;
            case MessageType.Announcement:
                _messageText.FontWeight = FontWeights.SemiBold;
                _messageText.Text = $"◆ {message.Content}";
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                UpdateAccentColors(Color.FromRgb(255, 215, 0));
                break;
            default:
                _messageText.Text = message.Content;
                _messageText.Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 200));
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlayEntryAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        _cardContainer.MouseEnter -= OnCardMouseEnter;
        _cardContainer.MouseLeave -= OnCardMouseLeave;
    }

    private void PlayEntryAnimation()
    {
        // Initial state - off screen and scaled down
        _mainGrid.Opacity = 0;
        _scaleTransform.ScaleX = 0.8;
        _scaleTransform.ScaleY = 0.8;
        _translateTransform.X = 50;

        // Slide in with origami unfold effect
        var slideIn = new DoubleAnimation
        {
            From = 50,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        var scaleIn = new DoubleAnimation
        {
            From = 0.8,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
        };

        _translateTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        _mainGrid.BeginAnimation(OpacityProperty, fadeIn);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
    }

    private static Color GetRoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.FromRgb(255, 215, 0),      // Gold
            UserRole.Admin => Color.FromRgb(255, 102, 102),    // Red
            UserRole.Moderator => Color.FromRgb(179, 102, 255), // Purple
            UserRole.VIP => Color.FromRgb(0, 255, 159),        // Neon Green
            UserRole.Verified => Color.FromRgb(0, 229, 255),   // Cyan
            _ => Color.FromRgb(200, 210, 230)                  // Default
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

        return local.ToString("MMM dd, HH:mm");
    }

    private static void OnElevationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OrigamiMessageCard card)
        {
            card._shadowEffect.ShadowDepth = (double)e.NewValue * 2;
            card._shadowEffect.BlurRadius = (double)e.NewValue * 6;
        }
    }

    private static void OnIsOwnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OrigamiMessageCard card)
        {
            card._cardContainer.HorizontalAlignment = (bool)e.NewValue
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
        }
    }

    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OrigamiMessageCard card)
        {
            card.UpdateAccentColors((Color)e.NewValue);
        }
    }
}
