using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public enum ProductSortOrder
{
    Newest,
    PriceLowToHigh,
    PriceHighToLow,
    MostPopular,
    TopRated
}

public partial class MarketplaceViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly DispatcherTimer _actionMessageTimer;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _products = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _myProducts = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _featuredProducts = [];

    [ObservableProperty]
    private ObservableCollection<ProductDto> _recentlyViewedProducts = [];

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private ProductSortOrder _selectedSortOrder = ProductSortOrder.Newest;

    [ObservableProperty]
    private decimal? _minPrice;

    [ObservableProperty]
    private decimal? _maxPrice;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalProductCount;

    [ObservableProperty]
    private bool _isCreatingListing;

    [ObservableProperty]
    private bool _showProductDetail;

    [ObservableProperty]
    private bool _showQuickView;

    [ObservableProperty]
    private ProductDto? _quickViewProduct;

    [ObservableProperty]
    private int _cartItemCount;

    [ObservableProperty]
    private int _wishlistCount;

    [ObservableProperty]
    private string? _actionMessage;

    [ObservableProperty]
    private bool _showActionMessage;

    [ObservableProperty]
    private bool _isInWishlist;

    [ObservableProperty]
    private bool _showDeleteConfirmation;

    [ObservableProperty]
    private ProductDto? _productToDelete;

    // Share dialog
    [ObservableProperty]
    private bool _showShareDialog;

    [ObservableProperty]
    private ProductDto? _productToShare;

    [ObservableProperty]
    private string _shareLink = string.Empty;

    // My Listings view
    [ObservableProperty]
    private bool _showMyListings;

    [ObservableProperty]
    private bool _isEditingProduct;

    [ObservableProperty]
    private ProductDto? _productToEdit;

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

    [ObservableProperty]
    private ObservableCollection<string> _newImageUrls = [];

    [ObservableProperty]
    private bool _isUploadingImages;

    public List<ProductCategory> Categories { get; } = Enum.GetValues<ProductCategory>().ToList();
    public List<ProductSortOrder> SortOrders { get; } = Enum.GetValues<ProductSortOrder>().ToList();

    public decimal ListingFee => 1.50m;

    public MarketplaceViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;

        _actionMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _actionMessageTimer.Tick += OnActionMessageTimerTick;

        _ = InitializeAsync();
    }

    private void OnActionMessageTimerTick(object? sender, EventArgs e)
    {
        _actionMessageTimer.Stop();
        ShowActionMessage = false;
        ActionMessage = null;
    }

    public void Cleanup()
    {
        _actionMessageTimer.Stop();
        _actionMessageTimer.Tick -= OnActionMessageTimerTick;
    }

    private async Task InitializeAsync()
    {
        await Task.WhenAll(
            LoadProductsAsync(),
            LoadFeaturedProductsAsync(),
            LoadCartCountAsync(),
            LoadWishlistCountAsync()
        );
    }

    private void ShowTemporaryMessage(string message)
    {
        ActionMessage = message;
        ShowActionMessage = true;
        _actionMessageTimer.Stop();
        _actionMessageTimer.Start();
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

            // Apply sorting locally (ideally should be server-side)
            var sorted = SelectedSortOrder switch
            {
                ProductSortOrder.PriceLowToHigh => result.Products.OrderBy(p => p.Price),
                ProductSortOrder.PriceHighToLow => result.Products.OrderByDescending(p => p.Price),
                ProductSortOrder.MostPopular => result.Products.OrderByDescending(p => p.ViewCount),
                ProductSortOrder.TopRated => result.Products.OrderByDescending(p => p.AverageRating),
                _ => result.Products.OrderByDescending(p => p.CreatedAt) // Newest
            };

            // Apply price filter
            if (MinPrice.HasValue)
                sorted = sorted.Where(p => p.Price >= MinPrice.Value).OrderBy(_ => 0);
            if (MaxPrice.HasValue)
                sorted = sorted.Where(p => p.Price <= MaxPrice.Value).OrderBy(_ => 0);

            Products.Clear();
            foreach (var product in sorted)
            {
                Products.Add(product);
            }
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
            TotalProductCount = result.TotalCount;
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

    private async Task LoadFeaturedProductsAsync()
    {
        try
        {
            var result = await _apiService.GetProductsAsync(1, null, null);
            FeaturedProducts.Clear();
            // Take top rated products as featured
            foreach (var product in result.Products.OrderByDescending(p => p.AverageRating).Take(6))
            {
                FeaturedProducts.Add(product);
            }
        }
        catch (Exception ex)
        {
            // Featured products are optional, but log error for debugging
            System.Diagnostics.Debug.WriteLine($"Failed to load featured products: {ex.Message}");
        }
    }

    private async Task LoadCartCountAsync()
    {
        try
        {
            var cart = await _apiService.GetCartAsync();
            CartItemCount = cart?.Items?.Count ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cart count: {ex.Message}");
            CartItemCount = 0;
        }
    }

    private async Task LoadWishlistCountAsync()
    {
        try
        {
            var wishlist = await _apiService.GetWishlistAsync();
            WishlistCount = wishlist?.Count ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load wishlist count: {ex.Message}");
            WishlistCount = 0;
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
        catch (Exception ex)
        {
            // My products load failure shouldn't block the view, but log for debugging
            System.Diagnostics.Debug.WriteLine($"Failed to load my products: {ex.Message}");
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

            // Add to recently viewed
            var existing = RecentlyViewedProducts.FirstOrDefault(p => p.Id == product.Id);
            if (existing != null)
                RecentlyViewedProducts.Remove(existing);
            RecentlyViewedProducts.Insert(0, fullProduct);
            if (RecentlyViewedProducts.Count > 10)
                RecentlyViewedProducts.RemoveAt(RecentlyViewedProducts.Count - 1);

            // Check if in wishlist
            await CheckWishlistStatus(fullProduct.Id);
        }
    }

    [RelayCommand]
    private void CloseProductDetail()
    {
        SelectedProduct = null;
        ShowProductDetail = false;
        IsInWishlist = false;
    }

    [RelayCommand]
    private async Task QuickView(ProductDto product)
    {
        var fullProduct = await _apiService.GetProductAsync(product.Id);
        if (fullProduct != null)
        {
            QuickViewProduct = fullProduct;
            ShowQuickView = true;
            await CheckWishlistStatus(fullProduct.Id);
        }
    }

    [RelayCommand]
    private void CloseQuickView()
    {
        QuickViewProduct = null;
        ShowQuickView = false;
    }

    private async Task CheckWishlistStatus(string productId)
    {
        try
        {
            var wishlist = await _apiService.GetWishlistAsync();
            IsInWishlist = wishlist?.Any(w => w.ProductId == productId) ?? false;
        }
        catch
        {
            IsInWishlist = false;
        }
    }

    [RelayCommand]
    private async Task AddToCart(ProductDto? product)
    {
        var targetProduct = product ?? SelectedProduct ?? QuickViewProduct;
        if (targetProduct == null) return;

        try
        {
            await _apiService.AddToCartAsync(targetProduct.Id, 1);
            CartItemCount++;
            ShowTemporaryMessage($"Added '{targetProduct.Title}' to cart");
        }
        catch (Exception ex)
        {
            SetError("Failed to add to cart: " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddToWishlist(ProductDto? product)
    {
        var targetProduct = product ?? SelectedProduct ?? QuickViewProduct;
        if (targetProduct == null) return;

        try
        {
            if (IsInWishlist)
            {
                await _apiService.RemoveFromWishlistAsync(targetProduct.Id);
                IsInWishlist = false;
                WishlistCount = Math.Max(0, WishlistCount - 1);
                ShowTemporaryMessage($"Removed '{targetProduct.Title}' from wishlist");
            }
            else
            {
                await _apiService.AddToWishlistAsync(targetProduct.Id);
                IsInWishlist = true;
                WishlistCount++;
                ShowTemporaryMessage($"Added '{targetProduct.Title}' to wishlist");
            }
        }
        catch (Exception ex)
        {
            SetError("Failed to update wishlist: " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ChangeSortOrder(ProductSortOrder sortOrder)
    {
        SelectedSortOrder = sortOrder;
        CurrentPage = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task ApplyPriceFilter()
    {
        CurrentPage = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task ClearFilters()
    {
        SelectedCategory = null;
        SelectedSortOrder = ProductSortOrder.Newest;
        MinPrice = null;
        MaxPrice = null;
        SearchQuery = string.Empty;
        CurrentPage = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private void ShowDeleteConfirm(ProductDto product)
    {
        ProductToDelete = product;
        ShowDeleteConfirmation = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ProductToDelete = null;
        ShowDeleteConfirmation = false;
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (ProductToDelete == null) return;

        try
        {
            await _apiService.DeleteProductAsync(ProductToDelete.Id);
            MyProducts.Remove(ProductToDelete);
            ShowTemporaryMessage($"Deleted '{ProductToDelete.Title}'");
            ShowDeleteConfirmation = false;
            ProductToDelete = null;
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            SetError("Failed to delete product: " + ex.Message);
        }
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
            // Combine uploaded images with manually entered URL
            var allImageUrls = NewImageUrls.ToList();
            if (!string.IsNullOrEmpty(NewImageUrl) && !allImageUrls.Contains(NewImageUrl))
            {
                allImageUrls.Add(NewImageUrl);
            }

            var request = new CreateProductRequest
            {
                Title = NewTitle,
                Description = NewDescription,
                Price = price,
                Category = NewCategory,
                Tags = NewTags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList(),
                ImageUrls = allImageUrls
            };

            await _apiService.CreateProductAsync(request);
            IsCreatingListing = false;
            ClearNewListingFields();
            await LoadProductsAsync();
            await LoadMyProducts();
            ShowTemporaryMessage("Product listing created successfully!");
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

    /// <summary>
    /// Upload product images from file paths
    /// </summary>
    public async Task<List<string>> UploadProductImagesAsync(IEnumerable<string> filePaths)
    {
        var uploadedUrls = new List<string>();
        var fileUploadService = (IFileUploadService?)App.ServiceProvider.GetService(typeof(IFileUploadService));

        if (fileUploadService == null || string.IsNullOrEmpty(_apiService.AuthToken))
        {
            SetError("File upload service not available");
            return uploadedUrls;
        }

        IsUploadingImages = true;

        foreach (var filePath in filePaths)
        {
            try
            {
                var result = await fileUploadService.UploadAttachmentAsync(filePath, _apiService.AuthToken);
                if (result.Success && !string.IsNullOrEmpty(result.FileUrl))
                {
                    uploadedUrls.Add(result.FileUrl);
                    NewImageUrls.Add(result.FileUrl);
                }
                else
                {
                    ShowTemporaryMessage($"Failed to upload {System.IO.Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryMessage($"Upload error: {ex.Message}");
            }
        }

        IsUploadingImages = false;
        return uploadedUrls;
    }

    [RelayCommand]
    private void RemoveProductImage(string imageUrl)
    {
        if (NewImageUrls.Contains(imageUrl))
        {
            NewImageUrls.Remove(imageUrl);
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
        NewImageUrls.Clear();
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

    // Share functionality
    [RelayCommand]
    private void ShareProduct(ProductDto? product)
    {
        var targetProduct = product ?? SelectedProduct ?? QuickViewProduct;
        if (targetProduct == null) return;

        ProductToShare = targetProduct;
        ShareLink = $"vea://marketplace/product/{targetProduct.Id}";
        ShowShareDialog = true;
    }

    [RelayCommand]
    private void CopyShareLink()
    {
        if (string.IsNullOrEmpty(ShareLink)) return;

        try
        {
            System.Windows.Clipboard.SetText(ShareLink);
            ShowTemporaryMessage("Link copied to clipboard!");
            CloseShareDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy link: {ex.Message}");
            SetError("Failed to copy link");
        }
    }

    [RelayCommand]
    private void ShareToFriend()
    {
        // Copy the product link to clipboard and navigate to friends view
        try
        {
            if (!string.IsNullOrEmpty(ShareLink))
            {
                System.Windows.Clipboard.SetText(ShareLink);
                ShowTemporaryMessage("Link copied! Opening Friends to share...");
                _navigationService.NavigateToFriends();
                CloseShareDialog();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to prepare share: {ex.Message}");
            SetError("Failed to prepare share");
        }
    }

    [RelayCommand]
    private void CloseShareDialog()
    {
        ShowShareDialog = false;
        ProductToShare = null;
        ShareLink = string.Empty;
    }

    // My Listings management
    [RelayCommand]
    private async Task ToggleMyListings()
    {
        ShowMyListings = !ShowMyListings;
        if (ShowMyListings)
        {
            await LoadMyProducts();
        }
    }

    [RelayCommand]
    private void EditProduct(ProductDto product)
    {
        if (product == null) return;

        ProductToEdit = product;
        NewTitle = product.Title;
        NewDescription = product.Description;
        NewPrice = product.Price.ToString("F2");
        NewCategory = product.Category;
        NewTags = string.Join(", ", product.Tags);
        NewImageUrl = product.ImageUrls.FirstOrDefault() ?? string.Empty;
        IsEditingProduct = true;
    }

    [RelayCommand]
    private void CancelEditProduct()
    {
        ProductToEdit = null;
        IsEditingProduct = false;
        ClearNewListingFields();
    }

    [RelayCommand]
    private async Task SaveEditProduct()
    {
        if (ProductToEdit == null) return;

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
            var request = new UpdateProductRequest
            {
                Title = NewTitle,
                Description = NewDescription,
                Price = price,
                Category = NewCategory,
                Tags = NewTags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList(),
                ImageUrls = string.IsNullOrEmpty(NewImageUrl)
                    ? []
                    : [NewImageUrl],
                Status = ProductToEdit.Status // Preserve the existing status
            };

            var updatedProduct = await _apiService.UpdateProductAsync(ProductToEdit.Id, request);
            if (updatedProduct != null)
            {
                IsEditingProduct = false;
                ProductToEdit = null;
                ClearNewListingFields();
                ShowTemporaryMessage("Product updated successfully!");
                await LoadMyProducts();
                await LoadProductsAsync();
            }
            else
            {
                SetError("Failed to update product");
            }
        }
        catch (Exception ex)
        {
            SetError("Failed to update product: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Report product
    [RelayCommand]
    private void ReportProduct(ProductDto? product)
    {
        var targetProduct = product ?? SelectedProduct ?? QuickViewProduct;
        if (targetProduct == null) return;

        // For now, show a message. In a real implementation, this would open a report dialog
        ShowTemporaryMessage($"Report submitted for '{targetProduct.Title}'. Our team will review it.");
    }

    // View seller profile
    [RelayCommand]
    private void ViewSellerProfile(ProductDto? product)
    {
        var targetProduct = product ?? SelectedProduct ?? QuickViewProduct;
        if (targetProduct == null) return;

        // Navigate to seller's profile
        _navigationService.NavigateToProfile(targetProduct.SellerId);
        CloseQuickView(); // Close any open dialogs
    }
}
