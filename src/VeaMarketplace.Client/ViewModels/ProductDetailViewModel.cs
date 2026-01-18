using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ProductDetailViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _productId = string.Empty;

    [ObservableProperty]
    private ProductDto? _product;

    [ObservableProperty]
    private SellerProfileDto? _seller;

    [ObservableProperty]
    private ProductReviewListDto? _reviews;

    [ObservableProperty]
    private ObservableCollection<ProductDto> _similarProducts = new();

    [ObservableProperty]
    private ObservableCollection<ProductDto> _sellerProducts = new();

    [ObservableProperty]
    private int _selectedImageIndex;

    [ObservableProperty]
    private string? _selectedImageUrl;

    [ObservableProperty]
    private bool _isInWishlist;

    [ObservableProperty]
    private bool _isInCart;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private bool _showFullDescription;

    [ObservableProperty]
    private string _shareUrl = string.Empty;

    public ProductDetailViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    public async Task InitializeAsync(string productId)
    {
        ProductId = productId;
        await LoadProductAsync();
    }

    private async Task LoadProductAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Load product details
            Product = await _apiService.GetProductAsync(ProductId);
            if (Product == null)
            {
                SetError("Product not found");
                return;
            }

            // Set initial image
            if (Product.ImageUrls.Count > 0)
            {
                SelectedImageUrl = Product.ImageUrls[0];
            }

            // Set share URL
            ShareUrl = $"vea://marketplace/product/{ProductId}";

            // Load additional data in parallel
            var reviewsTask = LoadReviewsAsync();
            var similarTask = LoadSimilarProductsAsync();
            var wishlistTask = CheckWishlistStatusAsync();
            var cartTask = CheckCartStatusAsync();

            await Task.WhenAll(reviewsTask, similarTask, wishlistTask, cartTask);
        }, "Failed to load product");
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            Reviews = await _apiService.GetProductReviewsAsync(ProductId, 1);
        }
        catch
        {
            // Ignore errors loading reviews
        }
    }

    private async Task LoadSimilarProductsAsync()
    {
        try
        {
            var similar = await _apiService.GetSimilarProductsAsync(ProductId, 4);
            SimilarProducts.Clear();
            foreach (var product in similar)
            {
                SimilarProducts.Add(product);
            }
        }
        catch
        {
            // Ignore errors loading similar products
        }
    }

    private async Task CheckWishlistStatusAsync()
    {
        try
        {
            var wishlist = await _apiService.GetWishlistAsync();
            IsInWishlist = wishlist.Any(w => w.ProductId == ProductId);
        }
        catch
        {
            // Ignore errors
        }
    }

    private async Task CheckCartStatusAsync()
    {
        try
        {
            var cart = await _apiService.GetCartAsync();
            IsInCart = cart?.Items?.Any(i => i.ProductId == ProductId) ?? false;
        }
        catch
        {
            // Ignore errors
        }
    }

    [RelayCommand]
    private void SelectImage(int index)
    {
        if (Product != null && index >= 0 && index < Product.ImageUrls.Count)
        {
            SelectedImageIndex = index;
            SelectedImageUrl = Product.ImageUrls[index];
        }
    }

    [RelayCommand]
    private void NextImage()
    {
        if (Product != null && Product.ImageUrls.Count > 1)
        {
            var nextIndex = (SelectedImageIndex + 1) % Product.ImageUrls.Count;
            SelectImage(nextIndex);
        }
    }

    [RelayCommand]
    private void PreviousImage()
    {
        if (Product != null && Product.ImageUrls.Count > 1)
        {
            var prevIndex = SelectedImageIndex == 0 ? Product.ImageUrls.Count - 1 : SelectedImageIndex - 1;
            SelectImage(prevIndex);
        }
    }

    [RelayCommand]
    private async Task AddToCartAsync()
    {
        if (Product == null) return;

        await ExecuteAsync(async () =>
        {
            await _apiService.AddToCartAsync(ProductId, Quantity);
            IsInCart = true;
            SetStatus("Added to cart!");
        }, "Failed to add to cart");
    }

    [RelayCommand]
    private async Task BuyNowAsync()
    {
        if (Product == null) return;

        await ExecuteAsync(async () =>
        {
            await _apiService.AddToCartAsync(ProductId, Quantity);
            _navigationService.NavigateToCart();
        }, "Failed to process");
    }

    [RelayCommand]
    private async Task ToggleWishlistAsync()
    {
        if (Product == null) return;

        await ExecuteAsync(async () =>
        {
            if (IsInWishlist)
            {
                await _apiService.RemoveFromWishlistAsync(ProductId);
                IsInWishlist = false;
                SetStatus("Removed from wishlist");
            }
            else
            {
                await _apiService.AddToWishlistAsync(ProductId);
                IsInWishlist = true;
                SetStatus("Added to wishlist!");
            }
        }, "Failed to update wishlist");
    }

    [RelayCommand]
    private void ViewSellerProfile()
    {
        if (Product != null && !string.IsNullOrEmpty(Product.SellerId))
        {
            _navigationService.NavigateToProfile(Product.SellerId);
        }
    }

    [RelayCommand]
    private void ViewAllReviews()
    {
        // Navigate to reviews page
        _navigationService.NavigateTo($"ProductReviews:{ProductId}");
    }

    [RelayCommand]
    private void WriteReview()
    {
        // Navigate to write review page
        _navigationService.NavigateTo($"WriteReview:{ProductId}");
    }

    [RelayCommand]
    private void ViewSimilarProduct(ProductDto product)
    {
        if (product != null)
        {
            _navigationService.NavigateToProduct(product.Id);
        }
    }

    [RelayCommand]
    private void ShareProduct()
    {
        // Copy share URL to clipboard
        if (!string.IsNullOrEmpty(ShareUrl))
        {
            try
            {
                System.Windows.Clipboard.SetText(ShareUrl);
                SetStatus("Link copied to clipboard!");
            }
            catch
            {
                SetError("Failed to copy link");
            }
        }
    }

    [RelayCommand]
    private void ToggleDescription()
    {
        ShowFullDescription = !ShowFullDescription;
    }

    [RelayCommand]
    private void ReportProduct()
    {
        // Open report dialog
        SetStatus("Report functionality coming soon");
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateToMarketplace();
    }

    partial void OnQuantityChanged(int value)
    {
        if (value < 1) Quantity = 1;
        if (Product != null && Product.Stock > 0 && value > Product.Stock)
        {
            Quantity = Product.Stock;
        }
    }
}
