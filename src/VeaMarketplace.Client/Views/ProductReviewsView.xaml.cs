using System.ComponentModel;
using System.Windows.Controls;

namespace VeaMarketplace.Client.Views;

public partial class ProductReviewsView : UserControl
{
    public ProductReviewsView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(ViewModels.ProductReviewsViewModel));
    }
}
