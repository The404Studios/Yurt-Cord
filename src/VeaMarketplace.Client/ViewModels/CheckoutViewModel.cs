using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public partial class CheckoutViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private CartDto? _cart;

    [ObservableProperty]
    private ObservableCollection<CartItemDto> _cartItems = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _fees;

    [ObservableProperty]
    private decimal _discount;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private string _couponCode = string.Empty;

    [ObservableProperty]
    private string? _appliedCoupon;

    [ObservableProperty]
    private string? _couponError;

    [ObservableProperty]
    private bool _couponApplied;

    [ObservableProperty]
    private string _selectedPaymentMethod = "Balance";

    [ObservableProperty]
    private decimal _userBalance;

    [ObservableProperty]
    private bool _hasInsufficientFunds;

    [ObservableProperty]
    private string? _orderNotes;

    [ObservableProperty]
    private bool _agreeToTerms;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private CheckoutResultDto? _checkoutResult;

    [ObservableProperty]
    private bool _checkoutComplete;

    [ObservableProperty]
    private int _itemCount;

    public ObservableCollection<string> PaymentMethods { get; } = new()
    {
        "Balance",
        "PayPal",
        "Bitcoin",
        "CreditCard"
    };

    public CheckoutViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    public async Task InitializeAsync()
    {
        await LoadCartAsync();
        await LoadUserBalanceAsync();
    }

    private async Task LoadCartAsync()
    {
        await ExecuteAsync(async () =>
        {
            Cart = await _apiService.GetCartAsync();

            CartItems.Clear();
            if (Cart?.Items != null)
            {
                foreach (var item in Cart.Items)
                {
                    CartItems.Add(item);
                }
            }

            UpdateTotals();
        }, "Failed to load cart");
    }

    private async Task LoadUserBalanceAsync()
    {
        try
        {
            var user = await _apiService.GetCurrentUserAsync();
            if (user != null)
            {
                UserBalance = user.Balance;
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    private void UpdateTotals()
    {
        if (Cart == null)
        {
            Subtotal = 0;
            Fees = 0;
            Discount = 0;
            Total = 0;
            ItemCount = 0;
            return;
        }

        Subtotal = Cart.Subtotal;
        Fees = Cart.Fees;
        Total = Cart.Total;
        ItemCount = Cart.ItemCount;

        // Check if user has sufficient funds
        HasInsufficientFunds = SelectedPaymentMethod == "Balance" && Total > UserBalance;
    }

    [RelayCommand]
    private async Task ApplyCouponAsync()
    {
        if (string.IsNullOrWhiteSpace(CouponCode))
        {
            CouponError = "Please enter a coupon code";
            return;
        }

        await ExecuteAsync(async () =>
        {
            CouponError = null;
            var result = await _apiService.ApplyCouponAsync(CouponCode);

            if (result.IsValid)
            {
                AppliedCoupon = result.CouponCode;
                Discount = result.DiscountAmount;
                CouponApplied = true;
                CouponCode = string.Empty;

                // Reload cart to get updated totals
                await LoadCartAsync();
                SetStatus($"Coupon applied: {result.DiscountDescription}");
            }
            else
            {
                CouponError = result.ErrorMessage ?? "Invalid coupon code";
            }
        }, "Failed to apply coupon");
    }

    [RelayCommand]
    private async Task RemoveCouponAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _apiService.RemoveCouponAsync();
            AppliedCoupon = null;
            Discount = 0;
            CouponApplied = false;

            await LoadCartAsync();
            SetStatus("Coupon removed");
        }, "Failed to remove coupon");
    }

    [RelayCommand]
    private async Task UpdateQuantityAsync(CartItemDto item)
    {
        if (item == null) return;

        await ExecuteAsync(async () =>
        {
            Cart = await _apiService.UpdateCartItemAsync(item.Id, item.Quantity);
            UpdateTotals();
        }, "Failed to update quantity");
    }

    [RelayCommand]
    private async Task RemoveItemAsync(CartItemDto item)
    {
        if (item == null) return;

        await ExecuteAsync(async () =>
        {
            Cart = await _apiService.RemoveFromCartAsync(item.Id);
            CartItems.Remove(item);
            UpdateTotals();
        }, "Failed to remove item");
    }

    [RelayCommand]
    private async Task ProcessCheckoutAsync()
    {
        if (!AgreeToTerms)
        {
            SetError("You must agree to the terms and conditions");
            return;
        }

        if (CartItems.Count == 0)
        {
            SetError("Your cart is empty");
            return;
        }

        if (HasInsufficientFunds)
        {
            SetError("Insufficient balance. Please add funds or select a different payment method.");
            return;
        }

        IsProcessing = true;
        ClearError();

        try
        {
            var request = new CheckoutRequest
            {
                PaymentMethod = SelectedPaymentMethod,
                CouponCode = AppliedCoupon,
                Notes = OrderNotes
            };

            CheckoutResult = await _apiService.CheckoutAsync(request);

            if (CheckoutResult.Success)
            {
                CheckoutComplete = true;
                SetStatus("Checkout successful!");
            }
            else
            {
                SetError(CheckoutResult.ErrorMessage ?? "Checkout failed");
            }
        }
        catch (Exception ex)
        {
            SetError($"Checkout failed: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ViewOrders()
    {
        _navigationService.NavigateToOrderHistory();
    }

    [RelayCommand]
    private void ContinueShopping()
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private void ViewProduct(CartItemDto item)
    {
        if (item != null)
        {
            _navigationService.NavigateToProduct(item.ProductId);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateToCart();
    }

    partial void OnSelectedPaymentMethodChanged(string value)
    {
        HasInsufficientFunds = value == "Balance" && Total > UserBalance;
    }
}
