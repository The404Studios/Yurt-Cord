using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class OrderDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string BuyerId { get; set; } = string.Empty;
    public string BuyerUsername { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public decimal ListingFee { get; set; }
    public decimal SalesFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Amount => TotalAmount; // Alias for compatibility
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? PaymentTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsDisputed { get; set; } = false;
    public string? DisputeReason { get; set; }
    public bool EscrowHeld { get; set; } = false;
    public bool CanReview { get; set; }
    public bool CanDispute { get; set; }
    public bool IsProcessing { get; set; }
}

public class CreateOrderRequest
{
    public string ProductId { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? CouponCode { get; set; }
}

public class DisputeOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class OrderHistoryDto
{
    public List<OrderDto> Orders { get; set; } = new();
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
}
