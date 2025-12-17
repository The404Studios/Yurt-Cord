using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class DiscoverView : UserControl
{
    private readonly INavigationService? _navigationService;
    private readonly IApiService? _apiService;
    private bool _isLoading;

    public DiscoverView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        _apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));

        Loaded += DiscoverView_Loaded;
    }

    private async void DiscoverView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            await LoadDiscoverDataAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadDiscoverDataAsync()
    {
        if (_apiService == null) return;

        try
        {
            // Load all data in parallel for better performance
            var trendingTask = _apiService.GetTrendingProductsAsync(4);
            var newProductsTask = _apiService.GetProductsAsync(1, null, null);
            var topSellersTask = _apiService.GetTopSellersAsync(4);

            await Task.WhenAll(trendingTask, newProductsTask, topSellersTask);

            // Apply trending products
            var trending = await trendingTask;
            if (trending.Count > 0)
            {
                TrendingProducts.ItemsSource = trending;
            }

            // Apply new products
            var products = await newProductsTask;
            if (products.Products != null && products.Products.Count > 0)
            {
                NewProducts.ItemsSource = products.Products.Take(4).ToList();
            }

            // Apply top sellers
            var sellers = await topSellersTask;
            if (sellers.Count > 0)
            {
                TopSellers.ItemsSource = sellers.Select(s => new
                {
                    s.Username,
                    s.AvatarUrl,
                    Rating = s.AverageRating,
                    ReviewCount = s.TotalReviews,
                    ProductCount = s.ActiveListings
                }).ToList();
            }
            else
            {
                // Fallback to placeholder data if API returns empty
                TopSellers.ItemsSource = GetPlaceholderSellers();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverView] Error loading discover data: {ex.Message}");

            // Use fallback data on error
            TopSellers.ItemsSource = GetPlaceholderSellers();
        }
    }

    private static object[] GetPlaceholderSellers()
    {
        return new object[]
        {
            new { Username = "TopSeller", AvatarUrl = "", Rating = 4.9, ReviewCount = 128, ProductCount = 45 },
            new { Username = "DigitalArts", AvatarUrl = "", Rating = 4.8, ReviewCount = 89, ProductCount = 32 },
            new { Username = "CodeMaster", AvatarUrl = "", Rating = 4.7, ReviewCount = 156, ProductCount = 67 },
            new { Username = "ProDesigns", AvatarUrl = "", Rating = 4.9, ReviewCount = 203, ProductCount = 89 }
        };
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
        if (sender is FrameworkElement element && element.DataContext is ProductDto product)
        {
            _navigationService?.NavigateToProduct(product.Id);
        }
        else if (sender is FrameworkElement el && el.DataContext != null)
        {
            // Fallback for anonymous types
            var dataContext = el.DataContext;
            var idProperty = dataContext.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                var productId = idProperty.GetValue(dataContext)?.ToString();
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
                if (!string.IsNullOrEmpty(username))
                {
                    _navigationService?.NavigateToProfile(username);
                }
            }
        }
    }
}
