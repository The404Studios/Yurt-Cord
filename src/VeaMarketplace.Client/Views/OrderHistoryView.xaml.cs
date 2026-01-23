using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class OrderHistoryView : UserControl
{
    private readonly OrderHistoryViewModel? _viewModel;

    public OrderHistoryView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (OrderHistoryViewModel?)App.ServiceProvider.GetService(typeof(OrderHistoryViewModel));
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
            System.Diagnostics.Debug.WriteLine($"OrderHistoryView: Failed to load data: {ex.Message}");
        }
    }
}
