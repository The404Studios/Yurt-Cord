using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class CartViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<CartItemDto> _items = [];

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _fees;

    [ObservableProperty]
    private decimal _discount;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private string _couponCode = string.Empty;

    [ObservableProperty]
    private string? _appliedCouponCode;

    [ObservableProperty]
    private string? _couponDescription;

    [ObservableProperty]
    private bool _hasCouponApplied;

    [ObservableProperty]
    private string _selectedPaymentMethod = "balance";

    [ObservableProperty]
    private string _checkoutNotes = string.Empty;

    [ObservableProperty]
    private bool _isCheckoutInProgress;

    [ObservableProperty]
    private CheckoutResultDto? _checkoutResult;

    [ObservableProperty]
    private bool _showCheckoutSuccess;

    public bool IsCartEmpty => ItemCount == 0;
    public bool HasItems => ItemCount > 0;

    public CartViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        // Don't auto-load - call LoadDataAsync() explicitly when view opens
    }

    /// <summary>
    /// Loads cart data. Call this when cart view opens.
    /// </summary>
    public Task LoadDataAsync() => LoadCartAsync();

    private async Task LoadCartAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var cart = await _apiService.GetCartAsync();
            UpdateCartState(cart);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load cart: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCartState(CartDto cart)
    {
        Items.Clear();
        foreach (var item in cart.Items)
            Items.Add(item);

        Subtotal = cart.Subtotal;
        Fees = cart.Fees;
        Total = cart.Total;
        ItemCount = cart.ItemCount;

        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(HasItems));
    }

    [RelayCommand]
    private async Task RefreshCart()
    {
        await LoadCartAsync();
    }

    [RelayCommand]
    private async Task AddToCart(string productId)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var request = new AddToCartRequest { ProductId = productId, Quantity = 1 };
            var cart = await _apiService.AddToCartAsync(request);
            UpdateCartState(cart);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add to cart: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateQuantity(CartItemDto item)
    {
        if (item.Quantity < 1)
        {
            await RemoveItem(item);
            return;
        }

        try
        {
            IsLoading = true;

            var request = new UpdateCartItemRequest { ItemId = item.Id, Quantity = item.Quantity };
            var cart = await _apiService.UpdateCartItemAsync(request);
            UpdateCartState(cart);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update quantity: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task IncreaseQuantity(CartItemDto item)
    {
        item.Quantity++;
        await UpdateQuantity(item);
    }

    [RelayCommand]
    private async Task DecreaseQuantity(CartItemDto item)
    {
        if (item.Quantity > 1)
        {
            item.Quantity--;
            await UpdateQuantity(item);
        }
        else
        {
            await RemoveItem(item);
        }
    }

    [RelayCommand]
    private async Task RemoveItem(CartItemDto item)
    {
        try
        {
            IsLoading = true;

            var success = await _apiService.RemoveFromCartAsync(item.Id);
            if (success)
            {
                Items.Remove(item);
                ItemCount--;
                OnPropertyChanged(nameof(IsCartEmpty));
                OnPropertyChanged(nameof(HasItems));

                // Refresh to get updated totals
                await LoadCartAsync();
            }
            else
            {
                ErrorMessage = "Failed to remove item from cart.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to remove item: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearCart()
    {
        try
        {
            IsLoading = true;

            var success = await _apiService.ClearCartAsync();
            if (success)
            {
                Items.Clear();
                Subtotal = 0;
                Fees = 0;
                Total = 0;
                ItemCount = 0;
                Discount = 0;
                AppliedCouponCode = null;
                CouponDescription = null;
                HasCouponApplied = false;

                OnPropertyChanged(nameof(IsCartEmpty));
                OnPropertyChanged(nameof(HasItems));
            }
            else
            {
                ErrorMessage = "Failed to clear cart.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to clear cart: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplyCoupon()
    {
        if (string.IsNullOrWhiteSpace(CouponCode)) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await _apiService.ApplyCouponAsync(CouponCode);

            if (result.IsValid)
            {
                AppliedCouponCode = result.CouponCode;
                Discount = result.DiscountAmount;
                CouponDescription = result.DiscountDescription;
                HasCouponApplied = true;
                CouponCode = string.Empty;

                // Refresh cart to get updated totals
                await LoadCartAsync();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid coupon code.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to apply coupon: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveCoupon()
    {
        try
        {
            IsLoading = true;

            var success = await _apiService.RemoveCouponAsync();
            if (success)
            {
                AppliedCouponCode = null;
                Discount = 0;
                CouponDescription = null;
                HasCouponApplied = false;

                // Refresh cart to get updated totals
                await LoadCartAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to remove coupon: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Checkout()
    {
        if (ItemCount == 0) return;

        try
        {
            IsCheckoutInProgress = true;
            ErrorMessage = null;

            var request = new CheckoutRequest
            {
                PaymentMethod = SelectedPaymentMethod,
                CouponCode = AppliedCouponCode,
                Notes = string.IsNullOrWhiteSpace(CheckoutNotes) ? null : CheckoutNotes
            };

            var result = await _apiService.CheckoutAsync(request);
            CheckoutResult = result;

            if (result.Success)
            {
                ShowCheckoutSuccess = true;

                // Clear cart state
                Items.Clear();
                Subtotal = 0;
                Fees = 0;
                Total = 0;
                ItemCount = 0;
                Discount = 0;
                AppliedCouponCode = null;
                CouponDescription = null;
                HasCouponApplied = false;
                CheckoutNotes = string.Empty;

                OnPropertyChanged(nameof(IsCartEmpty));
                OnPropertyChanged(nameof(HasItems));
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Checkout failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Checkout failed: {ex.Message}";
        }
        finally
        {
            IsCheckoutInProgress = false;
        }
    }

    [RelayCommand]
    private void ViewOrder(OrderDto order)
    {
        _navigationService.NavigateToOrder(order.Id);
    }

    [RelayCommand]
    private void ViewProduct(CartItemDto item)
    {
        _navigationService.NavigateToProduct(item.ProductId);
    }

    [RelayCommand]
    private void ContinueShopping()
    {
        ShowCheckoutSuccess = false;
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private void ViewOrders()
    {
        ShowCheckoutSuccess = false;
        _navigationService.NavigateToOrders();
    }

    [RelayCommand]
    private void SelectPaymentMethod(string method)
    {
        SelectedPaymentMethod = method;
    }
}
