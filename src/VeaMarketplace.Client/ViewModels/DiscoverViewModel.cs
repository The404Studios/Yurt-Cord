using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public partial class DiscoverViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _featuredProducts = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _trendingProducts = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _newArrivals = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _recommendedProducts = [];

    [ObservableProperty]
    private ObservableCollection<SellerProfileDto> _topSellers = [];

    [ObservableProperty]
    private ObservableCollection<CategoryCountDto> _categories = [];

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private SellerProfileDto? _selectedSeller;

    [ObservableProperty]
    private bool _showProductDetail;

    [ObservableProperty]
    private bool _showSellerProfile;

    [ObservableProperty]
    private string _currentSection = "Featured";

    [ObservableProperty]
    private bool _isRefreshing;

    public bool HasFeaturedProducts => FeaturedProducts.Count > 0;
    public bool HasTrendingProducts => TrendingProducts.Count > 0;
    public bool HasNewArrivals => NewArrivals.Count > 0;
    public bool HasRecommendedProducts => RecommendedProducts.Count > 0;
    public bool HasTopSellers => TopSellers.Count > 0;

    public DiscoverViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        ClearError();

        try
        {
            await Task.WhenAll(
                LoadFeaturedProductsAsync(),
                LoadTrendingProductsAsync(),
                LoadNewArrivalsAsync(),
                LoadRecommendedProductsAsync(),
                LoadTopSellersAsync()
            );
        }
        catch (Exception ex)
        {
            SetError($"Failed to load discover content: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            NotifyCollectionChanges();
        }
    }

    private void NotifyCollectionChanges()
    {
        OnPropertyChanged(nameof(HasFeaturedProducts));
        OnPropertyChanged(nameof(HasTrendingProducts));
        OnPropertyChanged(nameof(HasNewArrivals));
        OnPropertyChanged(nameof(HasRecommendedProducts));
        OnPropertyChanged(nameof(HasTopSellers));
    }

    private async Task LoadFeaturedProductsAsync()
    {
        try
        {
            var products = await _apiService.GetFeaturedProductsAsync(10);
            FeaturedProducts.Clear();
            foreach (var product in products)
            {
                FeaturedProducts.Add(product);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load featured products: {ex.Message}");
        }
    }

    private async Task LoadTrendingProductsAsync()
    {
        try
        {
            var products = await _apiService.GetTrendingProductsAsync(12);
            TrendingProducts.Clear();
            foreach (var product in products)
            {
                TrendingProducts.Add(product);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load trending products: {ex.Message}");
        }
    }

    private async Task LoadNewArrivalsAsync()
    {
        try
        {
            var products = await _apiService.GetNewArrivalsAsync(12);
            NewArrivals.Clear();
            foreach (var product in products)
            {
                NewArrivals.Add(product);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load new arrivals: {ex.Message}");
        }
    }

    private async Task LoadRecommendedProductsAsync()
    {
        try
        {
            var products = await _apiService.GetRecommendedProductsAsync(12);
            RecommendedProducts.Clear();
            foreach (var product in products)
            {
                RecommendedProducts.Add(product);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recommended products: {ex.Message}");
        }
    }

    private async Task LoadTopSellersAsync()
    {
        try
        {
            var sellers = await _apiService.GetTopSellersAsync(8);
            TopSellers.Clear();
            foreach (var seller in sellers)
            {
                TopSellers.Add(seller);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load top sellers: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshAll()
    {
        IsRefreshing = true;
        ClearError();

        try
        {
            await Task.WhenAll(
                LoadFeaturedProductsAsync(),
                LoadTrendingProductsAsync(),
                LoadNewArrivalsAsync(),
                LoadRecommendedProductsAsync(),
                LoadTopSellersAsync()
            );
        }
        catch (Exception ex)
        {
            SetError($"Failed to refresh: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            NotifyCollectionChanges();
        }
    }

    [RelayCommand]
    private async Task RefreshSection(string section)
    {
        IsRefreshing = true;

        try
        {
            switch (section.ToLower())
            {
                case "featured":
                    await LoadFeaturedProductsAsync();
                    break;
                case "trending":
                    await LoadTrendingProductsAsync();
                    break;
                case "new":
                    await LoadNewArrivalsAsync();
                    break;
                case "recommended":
                    await LoadRecommendedProductsAsync();
                    break;
                case "sellers":
                    await LoadTopSellersAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh {section}: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            NotifyCollectionChanges();
        }
    }

    [RelayCommand]
    private async Task ViewProduct(ProductDto product)
    {
        if (product == null) return;

        var fullProduct = await _apiService.GetProductAsync(product.Id);
        if (fullProduct != null)
        {
            SelectedProduct = fullProduct;
            ShowProductDetail = true;
        }
    }

    [RelayCommand]
    private void CloseProductDetail()
    {
        SelectedProduct = null;
        ShowProductDetail = false;
    }

    [RelayCommand]
    private void ViewSeller(SellerProfileDto seller)
    {
        if (seller == null) return;
        _navigationService.NavigateToProfile(seller.UserId);
    }

    [RelayCommand]
    private void ViewCategory(ProductCategory category)
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private async Task AddToCart(ProductDto product)
    {
        if (product == null) return;

        try
        {
            await _apiService.AddToCartAsync(product.Id, 1);
        }
        catch (Exception ex)
        {
            SetError($"Failed to add to cart: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddToWishlist(ProductDto product)
    {
        if (product == null) return;

        try
        {
            await _apiService.AddToWishlistAsync(product.Id);
        }
        catch (Exception ex)
        {
            SetError($"Failed to add to wishlist: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NavigateToSection(string section)
    {
        CurrentSection = section;
    }

    [RelayCommand]
    private void ViewAllTrending()
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private void ViewAllNew()
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private void ViewAllSellers()
    {
        _navigationService.NavigateToMarketplace();
    }

    public string GetRankBadge(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "Legend",
            UserRank.Elite => "Elite",
            UserRank.Diamond => "Diamond",
            UserRank.Platinum => "Platinum",
            UserRank.Gold => "Gold",
            UserRank.Silver => "Silver",
            UserRank.Bronze => "Bronze",
            _ => "Newcomer"
        };
    }

    public string GetRankColor(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "#FFD700",
            UserRank.Elite => "#FF4500",
            UserRank.Diamond => "#00BFFF",
            UserRank.Platinum => "#E5E4E2",
            UserRank.Gold => "#FFD700",
            UserRank.Silver => "#C0C0C0",
            UserRank.Bronze => "#CD7F32",
            _ => "#808080"
        };
    }
}

public class CategoryCountDto
{
    public ProductCategory Category { get; set; }
    public int Count { get; set; }
    public string DisplayName => Category.ToString();
}
