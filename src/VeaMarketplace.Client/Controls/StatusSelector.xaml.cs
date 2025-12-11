using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VeaMarketplace.Client.Controls;

public enum UserOnlineStatus
{
    Online,
    Idle,
    DoNotDisturb,
    Invisible
}

public partial class StatusSelector : UserControl
{
    private UserOnlineStatus _selectedStatus = UserOnlineStatus.Online;

    public UserOnlineStatus SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            _selectedStatus = value;
            UpdateSelection();
            StatusChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<UserOnlineStatus>? StatusChanged;
    public event EventHandler? CustomStatusRequested;

    public StatusSelector()
    {
        InitializeComponent();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        OnlineCheck.Visibility = _selectedStatus == UserOnlineStatus.Online ? Visibility.Visible : Visibility.Collapsed;
        IdleCheck.Visibility = _selectedStatus == UserOnlineStatus.Idle ? Visibility.Visible : Visibility.Collapsed;
        DndCheck.Visibility = _selectedStatus == UserOnlineStatus.DoNotDisturb ? Visibility.Visible : Visibility.Collapsed;
        InvisibleCheck.Visibility = _selectedStatus == UserOnlineStatus.Invisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Status_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = (Brush)FindResource("QuaternaryDarkBrush");
        }
    }

    private void Status_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
        }
    }

    private void StatusOnline_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedStatus = UserOnlineStatus.Online;
    }

    private void StatusIdle_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedStatus = UserOnlineStatus.Idle;
    }

    private void StatusDnd_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedStatus = UserOnlineStatus.DoNotDisturb;
    }

    private void StatusInvisible_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedStatus = UserOnlineStatus.Invisible;
    }

    private void SetCustomStatus_Click(object sender, MouseButtonEventArgs e)
    {
        CustomStatusRequested?.Invoke(this, EventArgs.Empty);
    }

    public static Brush GetStatusColor(UserOnlineStatus status)
    {
        return status switch
        {
            UserOnlineStatus.Online => new SolidColorBrush(Color.FromRgb(87, 242, 135)),
            UserOnlineStatus.Idle => new SolidColorBrush(Color.FromRgb(254, 231, 92)),
            UserOnlineStatus.DoNotDisturb => new SolidColorBrush(Color.FromRgb(237, 66, 69)),
            UserOnlineStatus.Invisible => new SolidColorBrush(Color.FromRgb(114, 118, 125)),
            _ => new SolidColorBrush(Color.FromRgb(114, 118, 125))
        };
    }
}
