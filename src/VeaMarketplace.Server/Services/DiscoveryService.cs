using Microsoft.Extensions.Caching.Memory;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;
using ActivityType = VeaMarketplace.Shared.Models.ActivityType;

namespace VeaMarketplace.Server.Services;

public class DiscoveryService
{
    private readonly DatabaseService _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortCacheDuration = TimeSpan.FromMinutes(1);

    public DiscoveryService(DatabaseService db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public List<ProductDto> GetFeaturedProducts(int limit = 10)
    {
        var cacheKey = $"featured_products_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            entry.SetSize(limit * 1024); // Estimate ~1KB per product

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active && p.IsFeatured)
                .OrderByDescending(p => p.CreatedAt)
                .Limit(limit)
                .ToList()
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public List<ProductDto> GetTrendingProducts(int limit = 20)
    {
        var cacheKey = $"trending_products_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ShortCacheDuration;
            entry.SetSize(limit * 1024);

            // Trending = most views + likes in the last week
            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active)
                .ToEnumerable()
                .OrderByDescending(p => p.ViewCount + (p.LikeCount * 5)) // Likes weighted more
                .Take(limit)
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public List<ProductDto> GetNewArrivals(int limit = 20)
    {
        var cacheKey = $"new_arrivals_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ShortCacheDuration;
            entry.SetSize(limit * 1024);

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active)
                .OrderByDescending(p => p.CreatedAt)
                .Limit(limit)
                .ToList()
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public List<ProductDto> GetTopRated(int limit = 20)
    {
        var cacheKey = $"top_rated_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            entry.SetSize(limit * 1024);

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active && p.ReviewCount >= 3)
                .ToEnumerable()
                .OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.ReviewCount)
                .Take(limit)
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public List<ProductDto> GetBestSellers(ProductCategory? category = null, int limit = 20)
    {
        var cacheKey = $"best_sellers_{category?.ToString() ?? "all"}_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            entry.SetSize(limit * 1024);

            var query = _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Sold);

            if (category.HasValue)
            {
                query = query.Where(p => p.Category == category.Value);
            }

            // Count sales per seller's other products
            var sellerSales = query.ToList()
                .GroupBy(p => p.SellerId)
                .Select(g => new { SellerId = g.Key, SalesCount = g.Count() })
                .ToDictionary(x => x.SellerId, x => x.SalesCount);

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active)
                .ToEnumerable()
                .Where(p => sellerSales.ContainsKey(p.SellerId))
                .OrderByDescending(p => sellerSales.GetValueOrDefault(p.SellerId, 0))
                .Take(limit)
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public List<SellerProfileDto> GetTopSellers(int limit = 10)
    {
        var cacheKey = $"top_sellers_{limit}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            entry.SetSize(limit * 2048); // Seller profiles are larger

            return _db.Users.Query()
                .Where(u => u.TotalSales > 0)
                .ToEnumerable()
                .OrderByDescending(u => u.TotalSales)
                .ThenByDescending(u => u.Reputation)
                .Take(limit)
                .Select(MapSellerToDto)
                .ToList();
        }) ?? new List<SellerProfileDto>();
    }

    public List<ProductDto> GetRecommendedProducts(string? userId, int limit = 20)
    {
        if (string.IsNullOrEmpty(userId))
        {
            // Return trending products for anonymous users
            return GetTrendingProducts(limit);
        }

        // Get user's purchase history and wishlist
        var purchasedCategories = _db.Orders
            .Find(o => o.BuyerId == userId && o.Status == OrderStatus.Completed)
            .Select(o => _db.Products.FindById(o.ProductId)?.Category)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        var wishlistCategories = _db.WishlistItems
            .Find(w => w.UserId == userId)
            .Select(w => _db.Products.FindById(w.ProductId)?.Category)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        var preferredCategories = purchasedCategories
            .Concat(wishlistCategories)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(3)
            .ToList();

        if (preferredCategories.Count == 0)
        {
            return GetTrendingProducts(limit);
        }

        // Get products from preferred categories
        return _db.Products.Query()
            .Where(p => p.Status == ProductStatus.Active && p.SellerId != userId)
            .ToEnumerable()
            .Where(p => preferredCategories.Contains(p.Category))
            .OrderByDescending(p => p.AverageRating)
            .ThenByDescending(p => p.ViewCount)
            .Take(limit)
            .Select(MapProductToDto)
            .ToList();
    }

    public List<ProductDto> GetSimilarProducts(string productId, int limit = 10)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return new();

        // Find products in same category with similar tags
        return _db.Products.Query()
            .Where(p => p.Status == ProductStatus.Active &&
                       p.Id != productId &&
                       p.Category == product.Category)
            .ToEnumerable()
            .Select(p => new
            {
                Product = p,
                TagScore = p.Tags.Intersect(product.Tags).Count()
            })
            .OrderByDescending(x => x.TagScore)
            .ThenByDescending(x => x.Product.AverageRating)
            .Take(limit)
            .Select(x => MapProductToDto(x.Product))
            .ToList();
    }

    public List<ProductDto> GetRecentlyViewed(string userId, int limit = 10)
    {
        // Get from user activity
        return _db.UserActivities
            .Find(a => a.UserId == userId && a.Type == ActivityType.ViewedProduct)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a =>
            {
                var product = _db.Products.FindById(a.TargetId);
                return product != null ? MapProductToDto(product) : null;
            })
            .Where(p => p != null)
            .Cast<ProductDto>()
            .ToList();
    }

    public List<ProductDto> GetProductsByCategory(ProductCategory category, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"products_category_{category}_{page}_{pageSize}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ShortCacheDuration;
            entry.SetSize(pageSize * 1024);

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active && p.Category == category)
                .ToEnumerable()
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapProductToDto)
                .ToList();
        }) ?? new List<ProductDto>();
    }

    public Dictionary<ProductCategory, int> GetCategoryCounts()
    {
        var cacheKey = "category_counts";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            entry.SetSize(1024);

            return _db.Products.Query()
                .Where(p => p.Status == ProductStatus.Active)
                .ToList()
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.Count());
        }) ?? new Dictionary<ProductCategory, int>();
    }

    public void InvalidateCache(string? pattern = null)
    {
        // In a production environment, you would use IMemoryCache.Remove()
        // or implement a distributed cache with pattern-based invalidation
        // For now, cache will expire naturally based on TTL
    }

    private ProductDto MapProductToDto(Product product)
    {
        var seller = _db.Users.FindById(product.SellerId);
        return new ProductDto
        {
            Id = product.Id,
            SellerId = product.SellerId,
            SellerUsername = product.SellerUsername,
            SellerRole = seller?.Role ?? UserRole.Member,
            SellerRank = seller?.Rank ?? UserRank.Newcomer,
            Title = product.Title,
            Description = product.Description,
            Price = product.Price,
            Category = product.Category,
            Status = product.Status,
            ImageUrls = product.ImageUrls,
            Tags = product.Tags,
            ViewCount = product.ViewCount,
            LikeCount = product.LikeCount,
            CreatedAt = product.CreatedAt,
            IsFeatured = product.IsFeatured,
            AverageRating = product.AverageRating,
            ReviewCount = product.ReviewCount
        };
    }

    private SellerProfileDto MapSellerToDto(User user)
    {
        var activeListings = _db.Products.Count(p => p.SellerId == user.Id && p.Status == ProductStatus.Active);
        var sellerProductIds = _db.Products
            .Find(p => p.SellerId == user.Id)
            .Select(p => p.Id)
            .ToHashSet();
        var reviews = _db.ProductReviews
            .FindAll()
            .Where(r => sellerProductIds.Contains(r.ProductId))
            .ToList();

        return new SellerProfileDto
        {
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName ?? user.Username,
            AvatarUrl = user.AvatarUrl ?? "",
            BannerUrl = user.BannerUrl,
            Bio = user.Bio,
            Role = user.Role,
            Rank = user.Rank,
            IsVerified = user.Role >= UserRole.Verified,
            TotalSales = user.TotalSales,
            TotalEarnings = user.Balance, // Simplified
            AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0,
            TotalReviews = reviews.Count,
            ActiveListings = activeListings,
            MemberSince = user.CreatedAt,
            ResponseRate = 95, // Placeholder
            ResponseTime = "< 1 hour" // Placeholder
        };
    }
}
