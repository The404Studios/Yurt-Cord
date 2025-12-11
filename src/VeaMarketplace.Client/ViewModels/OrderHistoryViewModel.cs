using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class OrderHistoryViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly Services.INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<OrderDto> _orders = new();

    [ObservableProperty]
    private int _totalOrders = 0;

    [ObservableProperty]
    private decimal _totalSpent = 0;

    [ObservableProperty]
    private int _pendingOrders = 0;

    [ObservableProperty]
    private int _completedOrders = 0;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public OrderHistoryViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load orders
            // var orderHistory = await _apiService.GetOrderHistoryAsync();
            // Orders.Clear();
            // foreach (var order in orderHistory.Orders)
            //     Orders.Add(order);

            // TotalOrders = orderHistory.TotalOrders;
            // TotalSpent = orderHistory.TotalSpent;
            // PendingOrders = orderHistory.PendingOrders;
            // CompletedOrders = orderHistory.CompletedOrders;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load orders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ViewOrderDetails(OrderDto order)
    {
        // TODO: Open order details dialog or navigate to details page
    }

    [RelayCommand]
    private async Task ContactSeller(OrderDto order)
    {
        // TODO: Open chat with seller
        // _navigationService.NavigateToDirectMessage(order.SellerId);
    }

    [RelayCommand]
    private void LeaveReview(OrderDto order)
    {
        // Open review dialog
        var dialog = new Views.WriteReviewDialog
        {
            DataContext = new WriteReviewViewModel(_apiService, order.ProductId, order.ProductTitle)
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task OpenDispute(OrderDto order)
    {
        // TODO: Open dispute dialog
        // Show confirmation and reason input
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Filter orders based on search query
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadOrdersAsync();
        }
        else
        {
            var filtered = Orders.Where(o =>
                o.ProductTitle.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                o.SellerUsername.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                o.Id.Contains(value, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            Orders.Clear();
            foreach (var order in filtered)
                Orders.Add(order);
        }
    }
}
