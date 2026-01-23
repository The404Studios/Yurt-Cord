using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class WishlistViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly Services.INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<WishlistItemDto> _wishlistItems = new();

    [ObservableProperty]
    private bool _isWishlistEmpty = true;

    public WishlistViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        // Don't auto-load - call LoadDataAsync() explicitly when view opens
    }

    /// <summary>
    /// Loads wishlist data. Call this when wishlist view opens.
    /// </summary>
    public Task LoadDataAsync() => LoadWishlistAsync();

    private async Task LoadWishlistAsync()
    {
        try
        {
            IsLoading = true;
            var items = await _apiService.GetWishlistAsync();
            WishlistItems.Clear();
            foreach (var item in items)
                WishlistItems.Add(item);

            IsWishlistEmpty = WishlistItems.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load wishlist: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddToCart(WishlistItemDto item)
    {
        try
        {
            // Add product to cart with quantity of 1
            var cart = await _apiService.AddToCartAsync(item.ProductId, 1);
            if (cart != null)
            {
                // Show success message
                ErrorMessage = ""; // Clear any previous errors
                // Optionally navigate to cart or show success notification
                _navigationService.NavigateToCart();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add to cart: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewProduct(string productId)
    {
        _navigationService.NavigateToProduct(productId);
    }

    [RelayCommand]
    private async Task RemoveFromWishlist(WishlistItemDto item)
    {
        try
        {
            if (await _apiService.RemoveFromWishlistAsync(item.ProductId))
            {
                WishlistItems.Remove(item);
                IsWishlistEmpty = WishlistItems.Count == 0;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to remove from wishlist: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearWishlist()
    {
        if (WishlistItems.Count == 0) return;

        try
        {
            if (await _apiService.ClearWishlistAsync())
            {
                WishlistItems.Clear();
                IsWishlistEmpty = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to clear wishlist: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseMarketplace()
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private async Task RefreshWishlist()
    {
        await LoadWishlistAsync();
    }
}
