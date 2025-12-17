using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class SellerProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public bool IsVerified { get; set; } = false;
    public int TotalSales { get; set; } = 0;
    public decimal TotalEarnings { get; set; } = 0;
    public int TotalOrders { get; set; } = 0;
    public int ActiveListings { get; set; } = 0;
    public double AverageRating { get; set; } = 0;
    public int TotalReviews { get; set; } = 0;
    public int PositiveReviews { get; set; } = 0;
    public int NeutralReviews { get; set; } = 0;
    public int NegativeReviews { get; set; } = 0;
    public double ResponseRate { get; set; } = 0;
    public string ResponseTime { get; set; } = "< 24 hours";
    public double AverageResponseTime { get; set; } = 0;
    public DateTime MemberSince { get; set; }
    public DateTime? LastActive { get; set; }
    public bool IsVerifiedSeller { get; set; } = false;
    public bool IsFeaturedSeller { get; set; } = false;
    public List<string> Badges { get; set; } = new();
    public List<ProductDto> RecentProducts { get; set; } = new();
    public List<ProductReviewDto> RecentReviews { get; set; } = new();
}
