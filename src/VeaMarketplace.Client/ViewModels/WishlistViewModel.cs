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
        LoadWishlistAsync();
    }

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
            // Purchase the product directly (simplified flow)
            if (await _apiService.PurchaseProductAsync(item.ProductId))
            {
                // Optionally remove from wishlist after purchase
                await RemoveFromWishlist(item);
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
