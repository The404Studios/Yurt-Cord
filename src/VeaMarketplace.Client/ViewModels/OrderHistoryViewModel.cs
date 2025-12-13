using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class OrderHistoryViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly Services.INavigationService _navigationService;
    private List<OrderDto> _allOrders = [];

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

    [ObservableProperty]
    private OrderDto? _selectedOrder;

    [ObservableProperty]
    private bool _isOrderDetailsOpen;

    public OrderHistoryViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
        _ = LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            IsLoading = true;
            _allOrders = await _apiService.GetOrdersAsync();
            Orders.Clear();
            foreach (var order in _allOrders)
                Orders.Add(order);

            TotalOrders = _allOrders.Count;
            TotalSpent = _allOrders.Sum(o => o.Amount);
            PendingOrders = _allOrders.Count(o => o.Status == Shared.Enums.OrderStatus.Pending || o.Status == Shared.Enums.OrderStatus.Processing);
            CompletedOrders = _allOrders.Count(o => o.Status == Shared.Enums.OrderStatus.Completed);
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
        SelectedOrder = order;
        IsOrderDetailsOpen = true;
    }

    [RelayCommand]
    private void CloseOrderDetails()
    {
        IsOrderDetailsOpen = false;
        SelectedOrder = null;
    }

    [RelayCommand]
    private void ContactSeller(OrderDto order)
    {
        _navigationService.NavigateToDirectMessage(order.SellerId);
    }

    [RelayCommand]
    private void LeaveReview(OrderDto order)
    {
        var dialog = new Views.WriteReviewDialog
        {
            DataContext = new WriteReviewViewModel(_apiService, order.ProductId, order.ProductTitle)
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenDispute(OrderDto order)
    {
        // For now, navigate to contact seller with dispute intent
        // In a full implementation, this would open a dispute dialog
        _navigationService.NavigateToDirectMessage(order.SellerId);
    }

    [RelayCommand]
    private void ViewProduct(string productId)
    {
        _navigationService.NavigateToProduct(productId);
    }

    [RelayCommand]
    private async Task RefreshOrders()
    {
        await LoadOrdersAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Orders.Clear();
            foreach (var order in _allOrders)
                Orders.Add(order);
        }
        else
        {
            var filtered = _allOrders.Where(o =>
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
