using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class NotificationCenterView : UserControl
{
    public NotificationCenterView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(NotificationCenterViewModel));
    }

    private void NotificationItem_MouseEnter(object sender, MouseEventArgs e)
    {
        // Optional: Add hover effects
    }

    private void NotificationItem_MouseLeave(object sender, MouseEventArgs e)
    {
        // Optional: Remove hover effects
    }
}
