using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public List<string> ImageUrls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int ViewCount { get; set; } = 0;
    public int LikeCount { get; set; } = 0;
    public double AverageRating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;
    public int FiveStarCount { get; set; } = 0;
    public int FourStarCount { get; set; } = 0;
    public int ThreeStarCount { get; set; } = 0;
    public int TwoStarCount { get; set; } = 0;
    public int OneStarCount { get; set; } = 0;
    public bool IsDigitalDelivery { get; set; } = true;
    public string? DeliveryInstructions { get; set; }
    public int StockQuantity { get; set; } = 1;
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsFeatured { get; set; } = false;
    public string? BuyerId { get; set; }
    public DateTime? SoldAt { get; set; }
    public List<string> BundleIds { get; set; } = []; // Bundles this product is part of
}
