using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class ServerAdminView : UserControl
{
    private readonly ServerAdminViewModel? _viewModel;

    public ServerAdminView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ServerAdminViewModel?)App.ServiceProvider.GetService(typeof(ServerAdminViewModel));
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
            System.Diagnostics.Debug.WriteLine($"ServerAdminView: Failed to load data: {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop the auto-refresh timer when view is unloaded
        _viewModel?.Cleanup();
    }
}
