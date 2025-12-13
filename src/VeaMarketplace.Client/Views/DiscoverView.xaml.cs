using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class DiscoverView : UserControl
{
    private readonly INavigationService? _navigationService;
    private readonly IApiService? _apiService;

    public DiscoverView()
    {
        InitializeComponent();
        Loaded += DiscoverView_Loaded;
    }

    public DiscoverView(INavigationService navigationService, IApiService apiService) : this()
    {
        _navigationService = navigationService;
        _apiService = apiService;
    }

    private async void DiscoverView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDiscoverDataAsync();
    }

    private async Task LoadDiscoverDataAsync()
    {
        try
        {
            // Load trending products
            if (_apiService != null)
            {
                var products = await _apiService.GetProductsAsync(1, null, null);
                if (products.Products != null && products.Products.Count > 0)
                {
                    // Take first 4 as trending
                    TrendingProducts.ItemsSource = products.Products.Take(4).ToList();
                    // Take next 4 as new
                    NewProducts.ItemsSource = products.Products.Skip(4).Take(4).ToList();
                }
            }

            // For now, use placeholder data for sellers
            var placeholderSellers = new[]
            {
                new { Username = "TopSeller", AvatarUrl = "", Rating = 4.9, ReviewCount = 128, ProductCount = 45 },
                new { Username = "DigitalArts", AvatarUrl = "", Rating = 4.8, ReviewCount = 89, ProductCount = 32 },
                new { Username = "CodeMaster", AvatarUrl = "", Rating = 4.7, ReviewCount = 156, ProductCount = 67 },
                new { Username = "ProDesigns", AvatarUrl = "", Rating = 4.9, ReviewCount = 203, ProductCount = 89 }
            };
            TopSellers.ItemsSource = placeholderSellers;
        }
        catch
        {
            // Handle errors silently for discovery page
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void MarketplaceButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void FeaturedBanner_Click(object sender, MouseButtonEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string category)
        {
            // Navigate to marketplace with category filter
            _navigationService?.NavigateToMarketplace();
        }
    }

    private void SeeAllTrending_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void SeeAllNew_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToMarketplace();
    }

    private void ViewAllSellers_Click(object sender, RoutedEventArgs e)
    {
        // Could navigate to a dedicated sellers page
        _navigationService?.NavigateToMarketplace();
    }

    private void ProductCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext != null)
        {
            var product = element.DataContext;
            var idProperty = product.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                var productId = idProperty.GetValue(product)?.ToString();
                if (!string.IsNullOrEmpty(productId))
                {
                    _navigationService?.NavigateToProduct(productId);
                }
            }
        }
    }

    private void SellerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext != null)
        {
            var seller = element.DataContext;
            var usernameProperty = seller.GetType().GetProperty("Username");
            if (usernameProperty != null)
            {
                var username = usernameProperty.GetValue(seller)?.ToString();
                // Navigate to seller profile
            }
        }
    }
}
