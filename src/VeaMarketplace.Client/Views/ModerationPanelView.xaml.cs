using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class ModerationPanelView : UserControl
{
    private readonly ModerationPanelViewModel? _viewModel;

    public ModerationPanelView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ModerationPanelViewModel?)App.ServiceProvider.GetService(typeof(ModerationPanelViewModel));
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
            System.Diagnostics.Debug.WriteLine($"ModerationPanelView: Failed to load data: {ex.Message}");
        }
    }
}
