namespace VeaMarketplace.Shared.Models;

public class ProductBundle
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = new();
    public decimal OriginalPrice { get; set; }
    public decimal BundlePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int SalesCount { get; set; } = 0;
}
