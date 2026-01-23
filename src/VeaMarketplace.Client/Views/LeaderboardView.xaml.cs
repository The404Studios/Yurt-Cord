using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class LeaderboardView : UserControl
{
    private readonly LeaderboardViewModel? _viewModel;

    public LeaderboardView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = App.ServiceProvider.GetService<LeaderboardViewModel>();
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
            System.Diagnostics.Debug.WriteLine($"LeaderboardView: Failed to load data: {ex.Message}");
        }
    }
}
