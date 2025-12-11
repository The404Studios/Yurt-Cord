using System.Windows;

namespace VeaMarketplace.Client.Views;

public partial class WriteReviewDialog : Window
{
    public WriteReviewDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string rating)
        {
            // Update rating in ViewModel
            if (DataContext is ViewModels.WriteReviewViewModel vm)
            {
                vm.SelectedRating = int.Parse(rating);
            }
        }
    }
}
