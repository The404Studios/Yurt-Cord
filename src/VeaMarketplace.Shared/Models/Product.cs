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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsFeatured { get; set; } = false;
    public string? BuyerId { get; set; }
    public DateTime? SoldAt { get; set; }
}
