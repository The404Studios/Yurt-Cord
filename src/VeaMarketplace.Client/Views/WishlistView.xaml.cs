using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class WishlistView : UserControl
{
    private readonly WishlistViewModel? _viewModel;

    public WishlistView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (WishlistViewModel?)App.ServiceProvider.GetService(typeof(WishlistViewModel));
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
            System.Diagnostics.Debug.WriteLine($"WishlistView: Failed to load data: {ex.Message}");
        }
    }
}
