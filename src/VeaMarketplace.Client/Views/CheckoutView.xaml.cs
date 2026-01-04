using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class CheckoutView : UserControl
{
    private readonly CheckoutViewModel? _viewModel;
    private readonly INavigationService? _navigationService;

    public CheckoutView()
    {
        InitializeComponent();
    }

    public CheckoutView(CheckoutViewModel viewModel, INavigationService navigationService) : this()
    {
        _viewModel = viewModel;
        _navigationService = navigationService;
        DataContext = viewModel;

        Loaded += CheckoutView_Loaded;
        Unloaded += CheckoutView_Unloaded;
    }

    private void CheckoutView_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CheckoutView_Loaded;
        Unloaded -= CheckoutView_Unloaded;
    }

    private async void CheckoutView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize checkout view: {ex.Message}");

            // Show error to user and provide recovery option
            var result = MessageBox.Show(
                $"Failed to load checkout: {ex.Message}\n\nWould you like to try again?",
                "Checkout Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                // Retry initialization
                CheckoutView_Loaded(sender, e);
            }
            else
            {
                // Navigate back to cart
                _navigationService?.NavigateBack();
            }
        }
    }

    private void ProductImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CartItemDto item)
        {
            _navigationService?.NavigateToProduct(item.ProductId);
        }
    }
}
