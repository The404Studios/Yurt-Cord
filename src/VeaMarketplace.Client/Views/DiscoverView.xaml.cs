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
        // Don't load data if user is not authenticated
        if (_apiService == null || !_apiService.IsAuthenticated)
        {
            Debug.WriteLine("[DiscoverView] Skipping load - user not authenticated");
            return;
        }

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
                // Show empty state when no sellers found
                TopSellers.ItemsSource = GetEmptyStateMessage();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverView] Error loading discover data: {ex.Message}");

            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowWarning("Connection Issue", "Could not load marketplace data. Please check your connection.");

            // Show empty state instead of fake data
            TopSellers.ItemsSource = GetEmptyStateMessage();
        }
    }

    private static object[] GetEmptyStateMessage()
    {
        // Return empty to show "no sellers found" UI state
        return Array.Empty<object>();
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
