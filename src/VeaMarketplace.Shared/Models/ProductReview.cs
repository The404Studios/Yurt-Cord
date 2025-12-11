namespace VeaMarketplace.Shared.Models;

public class ProductReview
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string UserAvatarUrl { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public int HelpfulCount { get; set; } = 0;
    public int UnhelpfulCount { get; set; } = 0;
    public bool IsVerifiedPurchase { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseAt { get; set; }
}
