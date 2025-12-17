using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class ProductDetailView : UserControl
{
    private readonly ProductDetailViewModel? _viewModel;
    private readonly INavigationService? _navigationService;

    public ProductDetailView()
    {
        InitializeComponent();
    }

    public ProductDetailView(ProductDetailViewModel viewModel, INavigationService navigationService) : this()
    {
        _viewModel = viewModel;
        _navigationService = navigationService;
        DataContext = viewModel;
    }

    public async Task InitializeAsync(string productId)
    {
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync(productId);
        }
    }

    private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && _viewModel?.Product != null)
        {
            var imageUrl = element.DataContext as string;
            if (imageUrl != null)
            {
                var index = _viewModel.Product.ImageUrls.IndexOf(imageUrl);
                if (index >= 0)
                {
                    _viewModel.SelectImageCommand.Execute(index);
                }
            }
        }
    }

    private void SellerCard_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.ViewSellerProfileCommand.Execute(null);
    }

    private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.Quantity > 1)
        {
            _viewModel.Quantity--;
        }
    }

    private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Quantity++;
        }
    }
}
