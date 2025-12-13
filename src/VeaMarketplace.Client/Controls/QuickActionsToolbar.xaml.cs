using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Controls;

public partial class QuickActionsToolbar : UserControl
{
    private bool _isExpanded;

    public event RoutedEventHandler? ScrollTopRequested;
    public event RoutedEventHandler? RefreshRequested;
    public event RoutedEventHandler? FilterRequested;
    public event RoutedEventHandler? CreateListingRequested;

    public QuickActionsToolbar()
    {
        InitializeComponent();
    }

    private void MainFab_Click(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        AnimateExpansion(_isExpanded);
    }

    private void AnimateExpansion(bool expand)
    {
        var duration = TimeSpan.FromMilliseconds(250);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Animate mini actions opacity and position
        var opacityAnimation = new DoubleAnimation
        {
            To = expand ? 1 : 0,
            Duration = duration,
            EasingFunction = easing
        };
        MiniActions.BeginAnimation(OpacityProperty, opacityAnimation);

        var translateAnimation = new DoubleAnimation
        {
            To = expand ? 0 : 20,
            Duration = duration,
            EasingFunction = easing
        };
        var transform = MiniActions.RenderTransform as System.Windows.Media.TranslateTransform;
        transform?.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnimation);

        // Rotate FAB icon
        var rotateAnimation = new DoubleAnimation
        {
            To = expand ? 45 : 0,
            Duration = duration,
            EasingFunction = easing
        };
        FabIconRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotateAnimation);

        // Update FAB background color
        MainFab.Background = expand
            ? (System.Windows.Media.Brush)FindResource("AccentRedBrush")
            : (System.Windows.Media.Brush)FindResource("AccentBlurpleBrush");
    }

    private void ScrollTop_Click(object sender, RoutedEventArgs e)
    {
        ScrollTopRequested?.Invoke(this, e);
        CollapseMenu();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, e);
        CollapseMenu();
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        FilterRequested?.Invoke(this, e);
        CollapseMenu();
    }

    private void CreateListing_Click(object sender, RoutedEventArgs e)
    {
        CreateListingRequested?.Invoke(this, e);
        CollapseMenu();
    }

    private void CollapseMenu()
    {
        if (_isExpanded)
        {
            _isExpanded = false;
            AnimateExpansion(false);
        }
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        BeginAnimation(OpacityProperty, animation);
    }

    public void Hide()
    {
        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        animation.Completed += (s, e) => Visibility = Visibility.Collapsed;
        BeginAnimation(OpacityProperty, animation);
    }
}
