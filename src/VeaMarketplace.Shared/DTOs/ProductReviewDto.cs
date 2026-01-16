using System.ComponentModel.DataAnnotations;

namespace VeaMarketplace.Shared.DTOs;

public class ProductReviewDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string UserAvatarUrl { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public int HelpfulCount { get; set; } = 0;
    public int UnhelpfulCount { get; set; } = 0;
    public bool IsVerifiedPurchase { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseAt { get; set; }
}

public class CreateReviewRequest
{
    [Required(ErrorMessage = "Product ID is required")]
    public string ProductId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Review title is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Review content is required")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Content must be between 10 and 5000 characters")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(5, ErrorMessage = "Maximum 5 images allowed")]
    public List<string> ImageUrls { get; set; } = new();
}

public class ReviewSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
}

public class ProductReviewListDto
{
    public List<ProductReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    // Summary properties (flattened for convenience)
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public string? ProductTitle { get; set; }
    public bool HasMore { get; set; }

    // Star breakdown
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
}
