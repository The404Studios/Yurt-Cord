using System.Windows.Controls;
using System.Windows.Input;

namespace VeaMarketplace.Client.Views;

public partial class NotificationCenterView : UserControl
{
    public NotificationCenterView()
    {
        InitializeComponent();
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
