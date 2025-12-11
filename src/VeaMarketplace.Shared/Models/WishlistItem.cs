namespace VeaMarketplace.Shared.Models;

public class WishlistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public decimal? PriceWhenAdded { get; set; }
    public bool NotifyOnPriceChange { get; set; } = false;
    public bool NotifyWhenAvailable { get; set; } = false;
}
