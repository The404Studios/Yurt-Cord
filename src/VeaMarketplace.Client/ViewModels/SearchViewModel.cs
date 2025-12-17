using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public partial class SearchViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private decimal? _minPrice;

    [ObservableProperty]
    private decimal? _maxPrice;

    [ObservableProperty]
    private string _sortBy = "newest";

    [ObservableProperty]
    private int _minRating;

    [ObservableProperty]
    private bool _verifiedSellersOnly;

    [ObservableProperty]
    private bool _featuredOnly;

    [ObservableProperty]
    private bool _inStockOnly = true;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _searchResults = new();

    [ObservableProperty]
    private ObservableCollection<SellerProfileDto> _sellerResults = new();

    [ObservableProperty]
    private int _totalResults;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private bool _hasMoreResults;

    [ObservableProperty]
    private string _searchType = "products"; // "products" or "sellers"

    [ObservableProperty]
    private ObservableCollection<string> _recentSearches = new();

    [ObservableProperty]
    private ObservableCollection<ProductDto> _trendingProducts = new();

    [ObservableProperty]
    private bool _showFilters;

    public ObservableCollection<ProductCategory> Categories { get; } = new(Enum.GetValues<ProductCategory>());

    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "newest",
        "oldest",
        "price_low",
        "price_high",
        "rating",
        "popular"
    };

    public SearchViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    public async Task InitializeAsync()
    {
        await LoadTrendingProductsAsync();
        LoadRecentSearches();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) && !SelectedCategory.HasValue)
        {
            SetStatus("Please enter a search term or select a category");
            return;
        }

        await ExecuteAsync(async () =>
        {
            CurrentPage = 1;
            SearchResults.Clear();
            SellerResults.Clear();

            if (SearchType == "products")
            {
                await SearchProductsAsync();
            }
            else
            {
                await SearchSellersAsync();
            }

            // Save to recent searches
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                AddToRecentSearches(SearchQuery);
            }
        }, "Search failed");
    }

    private async Task SearchProductsAsync()
    {
        var result = await _apiService.GetProductsAsync(
            CurrentPage,
            SelectedCategory,
            SearchQuery);

        if (result.Products != null)
        {
            foreach (var product in result.Products)
            {
                // Apply client-side filters
                if (MinPrice.HasValue && product.Price < MinPrice.Value) continue;
                if (MaxPrice.HasValue && product.Price > MaxPrice.Value) continue;
                if (MinRating > 0 && product.AverageRating < MinRating) continue;
                if (FeaturedOnly && !product.IsFeatured) continue;

                SearchResults.Add(product);
            }

            TotalResults = result.TotalCount;
            TotalPages = (int)Math.Ceiling(result.TotalCount / 20.0);
            HasMoreResults = CurrentPage < TotalPages;
        }

        // Sort results
        ApplySort();
    }

    private async Task SearchSellersAsync()
    {
        // Search for sellers by username
        var users = await _apiService.SearchUsersAsync(SearchQuery);
        var sellers = await _apiService.GetTopSellersAsync(50);

        // Filter sellers that match the search
        var matchingSellers = sellers
            .Where(s => string.IsNullOrEmpty(SearchQuery) ||
                       s.Username.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (VerifiedSellersOnly)
        {
            matchingSellers = matchingSellers.Where(s => s.IsVerified).ToList();
        }

        foreach (var seller in matchingSellers)
        {
            SellerResults.Add(seller);
        }

        TotalResults = matchingSellers.Count;
    }

    private void ApplySort()
    {
        var sorted = SortBy switch
        {
            "newest" => SearchResults.OrderByDescending(p => p.CreatedAt),
            "oldest" => SearchResults.OrderBy(p => p.CreatedAt),
            "price_low" => SearchResults.OrderBy(p => p.Price),
            "price_high" => SearchResults.OrderByDescending(p => p.Price),
            "rating" => SearchResults.OrderByDescending(p => p.AverageRating),
            "popular" => SearchResults.OrderByDescending(p => p.ViewCount),
            _ => SearchResults.OrderByDescending(p => p.CreatedAt)
        };

        var sortedList = sorted.ToList();
        SearchResults.Clear();
        foreach (var item in sortedList)
        {
            SearchResults.Add(item);
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMoreResults || IsLoading) return;

        await ExecuteAsync(async () =>
        {
            CurrentPage++;
            await SearchProductsAsync();
        }, "Failed to load more results");
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedCategory = null;
        MinPrice = null;
        MaxPrice = null;
        MinRating = 0;
        VerifiedSellersOnly = false;
        FeaturedOnly = false;
        InStockOnly = true;
        SortBy = "newest";
    }

    [RelayCommand]
    private void ToggleFilters()
    {
        ShowFilters = !ShowFilters;
    }

    [RelayCommand]
    private void SelectCategory(ProductCategory category)
    {
        SelectedCategory = category;
        _ = SearchAsync();
    }

    [RelayCommand]
    private void UseRecentSearch(string query)
    {
        SearchQuery = query;
        _ = SearchAsync();
    }

    [RelayCommand]
    private void ClearRecentSearches()
    {
        RecentSearches.Clear();
        SaveRecentSearches();
    }

    [RelayCommand]
    private void ViewProduct(ProductDto product)
    {
        _navigationService.NavigateToProduct(product.Id);
    }

    [RelayCommand]
    private void ViewSeller(SellerProfileDto seller)
    {
        _navigationService.NavigateToProfile(seller.Username);
    }

    private async Task LoadTrendingProductsAsync()
    {
        try
        {
            var trending = await _apiService.GetTrendingProductsAsync(8);
            TrendingProducts.Clear();
            foreach (var product in trending)
            {
                TrendingProducts.Add(product);
            }
        }
        catch
        {
            // Ignore errors loading trending
        }
    }

    private void LoadRecentSearches()
    {
        // Load from settings/storage if implemented
        // For now, use an in-memory list
    }

    private void AddToRecentSearches(string query)
    {
        if (RecentSearches.Contains(query))
        {
            RecentSearches.Remove(query);
        }

        RecentSearches.Insert(0, query);

        // Keep only last 10 searches
        while (RecentSearches.Count > 10)
        {
            RecentSearches.RemoveAt(RecentSearches.Count - 1);
        }

        SaveRecentSearches();
    }

    private void SaveRecentSearches()
    {
        // Save to settings/storage if implemented
    }

    partial void OnSortByChanged(string value)
    {
        if (SearchResults.Count > 0)
        {
            ApplySort();
        }
    }

    partial void OnSearchTypeChanged(string value)
    {
        SearchResults.Clear();
        SellerResults.Clear();
        TotalResults = 0;
    }
}
