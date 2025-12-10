using System.ComponentModel;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class FriendsView : UserControl
{
    public FriendsView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(FriendsViewModel));
    }
}
