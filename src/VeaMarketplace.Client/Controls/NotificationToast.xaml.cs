using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Controls;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    FriendRequest,
    Message
}

public partial class NotificationToast : UserControl
{
    private Storyboard? _slideOutAnimation;
    public event EventHandler? Closed;

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public string Message
    {
        get => MessageText.Text;
        set => MessageText.Text = value;
    }

    public NotificationToast()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var slideInAnimation = (Storyboard)FindResource("SlideInAnimation");
        _slideOutAnimation = (Storyboard)FindResource("SlideOutAnimation");

        // Clone to avoid sharing issues
        slideInAnimation = slideInAnimation.Clone();
        _slideOutAnimation = _slideOutAnimation.Clone();

        _slideOutAnimation.Completed += (s, args) => Closed?.Invoke(this, EventArgs.Empty);
        slideInAnimation.Begin(this);
    }

    public void SetNotificationType(NotificationType type)
    {
        var (icon, color) = type switch
        {
            NotificationType.Success => ("\uE73E", Color.FromRgb(87, 242, 135)),     // Checkmark, Green
            NotificationType.Warning => ("\uE7BA", Color.FromRgb(254, 231, 92)),     // Warning, Yellow
            NotificationType.Error => ("\uE711", Color.FromRgb(237, 66, 69)),        // X, Red
            NotificationType.FriendRequest => ("\uE8FA", Color.FromRgb(88, 101, 242)), // Person, Blurple
            NotificationType.Message => ("\uE8BD", Color.FromRgb(0, 175, 244)),      // Chat, Cyan
            _ => ("\uE946", Color.FromRgb(88, 101, 242))                              // Info, Blurple
        };

        IconText.Text = icon;
        var brush = new SolidColorBrush(color);
        IconBorder.Background = brush;
        AccentBar.Background = brush;
    }

    public void Close()
    {
        _slideOutAnimation?.Begin(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
