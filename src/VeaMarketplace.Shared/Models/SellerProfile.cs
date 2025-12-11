namespace VeaMarketplace.Shared.Models;

public class SellerProfile
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public decimal TotalSales { get; set; } = 0;
    public int TotalOrders { get; set; } = 0;
    public int ActiveListings { get; set; } = 0;
    public double AverageRating { get; set; } = 0;
    public int TotalReviews { get; set; } = 0;
    public int PositiveReviews { get; set; } = 0;
    public int NeutralReviews { get; set; } = 0;
    public int NegativeReviews { get; set; } = 0;
    public double ResponseRate { get; set; } = 0;
    public double AverageResponseTime { get; set; } = 0; // in hours
    public DateTime MemberSince { get; set; } = DateTime.UtcNow;
    public DateTime? LastActive { get; set; }
    public bool IsVerifiedSeller { get; set; } = false;
    public bool IsFeaturedSeller { get; set; } = false;
    public List<string> Badges { get; set; } = new();
    public string? PayPalEmail { get; set; }
    public string? BitcoinAddress { get; set; }
}
