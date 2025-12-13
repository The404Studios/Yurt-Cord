using System.ComponentModel;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class OrderHistoryView : UserControl
{
    public OrderHistoryView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(OrderHistoryViewModel));
    }
}
