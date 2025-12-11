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
            // TODO: Call API to load wishlist
            // var items = await _apiService.GetWishlistAsync();
            // WishlistItems.Clear();
            // foreach (var item in items)
            //     WishlistItems.Add(item);

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
            // TODO: Add to cart functionality
            // await _apiService.AddToCartAsync(item.ProductId);
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
            // TODO: Call API to remove from wishlist
            // await _apiService.RemoveFromWishlistAsync(item.Id);
            WishlistItems.Remove(item);
            IsWishlistEmpty = WishlistItems.Count == 0;
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

        // TODO: Show confirmation dialog
        try
        {
            // TODO: Call API to clear wishlist
            // await _apiService.ClearWishlistAsync();
            WishlistItems.Clear();
            IsWishlistEmpty = true;
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
}
