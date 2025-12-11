using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class ProductOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string BuyerId { get; set; } = string.Empty;
    public string BuyerUsername { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public decimal ListingFee { get; set; }
    public decimal SalesFee { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public string? PaymentTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsDisputed { get; set; } = false;
    public string? DisputeReason { get; set; }
    public DateTime? DisputedAt { get; set; }
    public string? DisputeResolution { get; set; }
    public DateTime? DisputeResolvedAt { get; set; }
    public bool EscrowHeld { get; set; } = false;
    public DateTime? EscrowReleasedAt { get; set; }
}
