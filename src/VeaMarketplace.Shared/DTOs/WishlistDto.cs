namespace VeaMarketplace.Shared.DTOs;

public class WishlistItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public ProductDto? Product { get; set; }
    public DateTime AddedAt { get; set; }
    public string? Notes { get; set; }
    public decimal? PriceWhenAdded { get; set; }
    public bool NotifyOnPriceChange { get; set; } = false;
    public bool NotifyWhenAvailable { get; set; } = false;
    public bool HasPriceDropped { get; set; }
    public decimal? PriceDropPercent { get; set; }
}

public class AddToWishlistRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool NotifyOnPriceChange { get; set; } = false;
    public bool NotifyWhenAvailable { get; set; } = false;
}

public class UpdateWishlistItemRequest
{
    public string WishlistItemId { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool NotifyOnPriceChange { get; set; }
    public bool NotifyWhenAvailable { get; set; }
}
