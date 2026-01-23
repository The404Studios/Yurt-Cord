using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class NotificationCenterView : UserControl
{
    private readonly NotificationCenterViewModel? _viewModel;

    public NotificationCenterView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (NotificationCenterViewModel?)App.ServiceProvider.GetService(typeof(NotificationCenterViewModel));
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        try
        {
            await _viewModel.LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NotificationCenterView: Failed to load data: {ex.Message}");
        }
    }

    private void NotificationItem_MouseEnter(object sender, MouseEventArgs e)
    {
        // Optional: Add hover effects
    }

    private void NotificationItem_MouseLeave(object sender, MouseEventArgs e)
    {
        // Optional: Remove hover effects
    }
}
