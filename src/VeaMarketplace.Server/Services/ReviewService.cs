using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ReviewService
{
    private readonly DatabaseService _db;

    public ReviewService(DatabaseService db)
    {
        _db = db;
    }

    public ProductReviewListDto GetProductReviews(string productId, int page = 1, int pageSize = 10, string? sortBy = null)
    {
        var product = _db.Products.FindById(productId);
        if (product == null)
        {
            return new ProductReviewListDto { Reviews = new(), TotalCount = 0 };
        }

        var query = _db.ProductReviews.Query()
            .Where(r => r.ProductId == productId);

        var allReviews = query.ToList();
        var totalCount = allReviews.Count;

        // Sort reviews
        IEnumerable<ProductReview> sorted = sortBy?.ToLower() switch
        {
            "helpful" => allReviews.OrderByDescending(r => r.HelpfulCount),
            "rating_high" => allReviews.OrderByDescending(r => r.Rating),
            "rating_low" => allReviews.OrderBy(r => r.Rating),
            "oldest" => allReviews.OrderBy(r => r.CreatedAt),
            _ => allReviews.OrderByDescending(r => r.CreatedAt)
        };

        var reviews = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();

        // Calculate rating breakdown
        var fiveStar = allReviews.Count(r => r.Rating == 5);
        var fourStar = allReviews.Count(r => r.Rating == 4);
        var threeStar = allReviews.Count(r => r.Rating == 3);
        var twoStar = allReviews.Count(r => r.Rating == 2);
        var oneStar = allReviews.Count(r => r.Rating == 1);
        var avgRating = totalCount > 0 ? allReviews.Average(r => r.Rating) : 0;

        return new ProductReviewListDto
        {
            Reviews = reviews,
            TotalCount = totalCount,
            TotalReviews = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < totalCount,
            AverageRating = Math.Round(avgRating, 1),
            FiveStarCount = fiveStar,
            FourStarCount = fourStar,
            ThreeStarCount = threeStar,
            TwoStarCount = twoStar,
            OneStarCount = oneStar,
            ProductTitle = product.Title
        };
    }

    public ProductReviewDto? CreateReview(string userId, CreateReviewRequest request)
    {
        var user = _db.Users.FindById(userId);
        var product = _db.Products.FindById(request.ProductId);

        if (user == null || product == null) return null;

        // Check if user already reviewed this product
        var existingReview = _db.ProductReviews.FindOne(r =>
            r.ProductId == request.ProductId && r.UserId == userId);
        if (existingReview != null) return null;

        // Check if user purchased the product
        var isVerifiedPurchase = _db.Orders.Exists(o =>
            o.ProductId == request.ProductId &&
            o.BuyerId == userId &&
            o.Status == Shared.Enums.OrderStatus.Completed);

        var review = new ProductReview
        {
            ProductId = request.ProductId,
            UserId = userId,
            Username = user.Username,
            UserAvatarUrl = user.AvatarUrl ?? "",
            Rating = Math.Clamp(request.Rating, 1, 5),
            Title = request.Title,
            Content = request.Content,
            ImageUrls = request.ImageUrls ?? new(),
            IsVerifiedPurchase = isVerifiedPurchase,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ProductReviews.Insert(review);

        // Update product rating
        UpdateProductRating(request.ProductId);

        return MapToDto(review);
    }

    public bool MarkReviewHelpful(string reviewId, string userId, bool isHelpful)
    {
        var review = _db.ProductReviews.FindById(reviewId);
        if (review == null) return false;

        if (isHelpful)
            review.HelpfulCount++;
        else
            review.UnhelpfulCount++;

        review.UpdatedAt = DateTime.UtcNow;
        _db.ProductReviews.Update(review);
        return true;
    }

    public bool ReportReview(string reviewId, string reporterId, string reason)
    {
        var review = _db.ProductReviews.FindById(reviewId);
        var reporter = _db.Users.FindById(reporterId);

        if (review == null || reporter == null) return false;

        var report = new MessageReport
        {
            MessageId = reviewId, // Using MessageId field for review ID
            ReporterId = reporterId,
            ReportedUserId = review.UserId,
            Reason = Shared.Models.ReportReason.Other,
            AdditionalInfo = $"Review Report: {reason}",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.MessageReports.Insert(report);
        return true;
    }

    public bool AddSellerResponse(string reviewId, string sellerId, string response)
    {
        var review = _db.ProductReviews.FindById(reviewId);
        if (review == null) return false;

        var product = _db.Products.FindById(review.ProductId);
        if (product == null || product.SellerId != sellerId) return false;

        review.SellerResponse = response;
        review.SellerResponseAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        _db.ProductReviews.Update(review);
        return true;
    }

    public bool DeleteReview(string reviewId, string userId)
    {
        var review = _db.ProductReviews.FindById(reviewId);
        if (review == null || review.UserId != userId) return false;

        _db.ProductReviews.Delete(reviewId);
        UpdateProductRating(review.ProductId);
        return true;
    }

    private void UpdateProductRating(string productId)
    {
        var reviews = _db.ProductReviews.Find(r => r.ProductId == productId).ToList();
        var product = _db.Products.FindById(productId);

        if (product != null)
        {
            product.AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
            product.ReviewCount = reviews.Count;
            _db.Products.Update(product);
        }
    }

    private static ProductReviewDto MapToDto(ProductReview review)
    {
        return new ProductReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            Username = review.Username,
            UserAvatarUrl = review.UserAvatarUrl,
            Rating = review.Rating,
            Title = review.Title,
            Content = review.Content,
            ImageUrls = review.ImageUrls,
            HelpfulCount = review.HelpfulCount,
            UnhelpfulCount = review.UnhelpfulCount,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            SellerResponse = review.SellerResponse,
            SellerResponseAt = review.SellerResponseAt
        };
    }
}
