using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class MarketplaceViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _products = new();

    [ObservableProperty]
    private ObservableCollection<ProductDto> _featuredProducts = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private bool _hasMorePages;

    public MarketplaceViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        CurrentPage = 1;
        Products.Clear();

        try
        {
            var result = await _apiService.GetProductsAsync(CurrentPage, SelectedCategory, SearchQuery);
            if (result.Products != null)
            {
                foreach (var product in result.Products)
                {
                    Products.Add(product);
                }
            }
            HasMorePages = result.HasNextPage;

            // Load featured separately
            var featured = await _apiService.GetFeaturedProductsAsync(5);
            FeaturedProducts.Clear();
            foreach (var product in featured)
            {
                FeaturedProducts.Add(product);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMorePages || IsLoading) return;

        IsLoading = true;
        CurrentPage++;

        try
        {
            var result = await _apiService.GetProductsAsync(CurrentPage, SelectedCategory, SearchQuery);
            if (result.Products != null)
            {
                foreach (var product in result.Products)
                {
                    Products.Add(product);
                }
            }
            HasMorePages = result.HasNextPage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        Products.Clear();
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task SelectCategoryAsync(string category)
    {
        SelectedCategory = category;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task ViewProductAsync(ProductDto product)
    {
        await _navigationService.NavigateToAsync("marketplace/product", new Dictionary<string, object>
        {
            { "productId", product.Id }
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadProductsAsync();
        IsRefreshing = false;
    }
}
