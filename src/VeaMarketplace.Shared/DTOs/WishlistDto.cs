using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Product ID is required")]
    public string ProductId { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }

    public bool NotifyOnPriceChange { get; set; } = false;
    public bool NotifyWhenAvailable { get; set; } = false;
}

public class UpdateWishlistItemRequest
{
    [Required(ErrorMessage = "Wishlist item ID is required")]
    public string WishlistItemId { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }

    public bool NotifyOnPriceChange { get; set; }
    public bool NotifyWhenAvailable { get; set; }
}
