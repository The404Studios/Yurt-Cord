using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (SettingsViewModel)App.ServiceProvider.GetService(typeof(SettingsViewModel))!;
        DataContext = _viewModel;
    }

    private void PttKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.StartRecordingPttKeyCommand.Execute(null);
        Focus();
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.IsRecordingPttKey == true)
        {
            _viewModel.SetPushToTalkKey(e.Key);
            e.Handled = true;
        }
    }
}
