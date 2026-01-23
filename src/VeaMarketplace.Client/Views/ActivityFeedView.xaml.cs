using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class ActivityFeedView : UserControl
{
    private readonly ActivityFeedViewModel? _viewModel;

    public ActivityFeedView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ActivityFeedViewModel?)App.ServiceProvider.GetService(typeof(ActivityFeedViewModel));
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
            System.Diagnostics.Debug.WriteLine($"ActivityFeedView: Failed to load data: {ex.Message}");
        }
    }
}
