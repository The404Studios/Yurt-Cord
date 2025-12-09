using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public partial class MarketplaceViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _products = new();

    [ObservableProperty]
    private ObservableCollection<ProductDto> _myProducts = new();

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _isCreatingListing;

    [ObservableProperty]
    private bool _showProductDetail;

    // New listing fields
    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private string _newDescription = string.Empty;

    [ObservableProperty]
    private string _newPrice = string.Empty;

    [ObservableProperty]
    private ProductCategory _newCategory = ProductCategory.Other;

    [ObservableProperty]
    private string _newTags = string.Empty;

    [ObservableProperty]
    private string _newImageUrl = string.Empty;

    public List<ProductCategory> Categories { get; } = Enum.GetValues<ProductCategory>().ToList();

    public decimal ListingFee => 1.50m;

    public MarketplaceViewModel(IApiService apiService)
    {
        _apiService = apiService;
        _ = LoadProductsAsync();
    }

    [RelayCommand]
    private async Task LoadProducts()
    {
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        ClearError();

        try
        {
            var result = await _apiService.GetProductsAsync(CurrentPage, SelectedCategory, SearchQuery);
            Products.Clear();
            foreach (var product in result.Products)
            {
                Products.Add(product);
            }
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
        }
        catch (Exception ex)
        {
            SetError("Failed to load products: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMyProducts()
    {
        try
        {
            var products = await _apiService.GetMyProductsAsync();
            MyProducts.Clear();
            foreach (var product in products)
            {
                MyProducts.Add(product);
            }
        }
        catch
        {
            // Ignore errors for my products
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        CurrentPage = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task FilterByCategory(ProductCategory? category)
    {
        SelectedCategory = category;
        CurrentPage = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadProductsAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadProductsAsync();
        }
    }

    [RelayCommand]
    private async Task ViewProduct(ProductDto product)
    {
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
    private async Task PurchaseProduct()
    {
        if (SelectedProduct == null) return;

        IsLoading = true;
        ClearError();

        try
        {
            var success = await _apiService.PurchaseProductAsync(SelectedProduct.Id);
            if (success)
            {
                CloseProductDetail();
                await LoadProductsAsync();
            }
            else
            {
                SetError("Purchase failed. Check your balance.");
            }
        }
        catch (Exception ex)
        {
            SetError("Purchase failed: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowCreateListing()
    {
        IsCreatingListing = true;
        ClearNewListingFields();
    }

    [RelayCommand]
    private void CancelCreateListing()
    {
        IsCreatingListing = false;
        ClearNewListingFields();
    }

    [RelayCommand]
    private async Task CreateListing()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            SetError("Title is required");
            return;
        }

        if (!decimal.TryParse(NewPrice, out var price) || price <= 0)
        {
            SetError("Please enter a valid price");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var request = new CreateProductRequest
            {
                Title = NewTitle,
                Description = NewDescription,
                Price = price,
                Category = NewCategory,
                Tags = NewTags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList(),
                ImageUrls = string.IsNullOrEmpty(NewImageUrl)
                    ? new List<string>()
                    : new List<string> { NewImageUrl }
            };

            await _apiService.CreateProductAsync(request);
            IsCreatingListing = false;
            ClearNewListingFields();
            await LoadProductsAsync();
            await LoadMyProducts();
        }
        catch (Exception ex)
        {
            SetError("Failed to create listing: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearNewListingFields()
    {
        NewTitle = string.Empty;
        NewDescription = string.Empty;
        NewPrice = string.Empty;
        NewCategory = ProductCategory.Other;
        NewTags = string.Empty;
        NewImageUrl = string.Empty;
        ClearError();
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Debounced search
        if (string.IsNullOrEmpty(value))
        {
            _ = LoadProductsAsync();
        }
    }
}
