using System.ComponentModel;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class ActivityFeedView : UserControl
{
    public ActivityFeedView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(ActivityFeedViewModel));
    }
}
