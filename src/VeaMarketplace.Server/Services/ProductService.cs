using Microsoft.Extensions.Caching.Memory;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ProductService
{
    private readonly DatabaseService _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public ProductService(DatabaseService db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public ProductDto CreateProduct(string userId, CreateProductRequest request)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) throw new ArgumentException($"User with ID '{userId}' not found", nameof(userId));

        var product = new Product
        {
            SellerId = userId,
            SellerUsername = user.Username,
            Title = request.Title,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrls = request.ImageUrls,
            Tags = request.Tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Products.Insert(product);

        return MapToDto(product, user);
    }

    public ProductListResponse GetProducts(int page = 1, int pageSize = 20, ProductCategory? category = null, string? search = null)
    {
        // Use cache for non-search queries
        var cacheKey = string.IsNullOrEmpty(search)
            ? $"products_{page}_{pageSize}_{category?.ToString() ?? "all"}"
            : null;

        if (cacheKey != null && _cache.TryGetValue(cacheKey, out ProductListResponse? cached) && cached != null)
        {
            return cached;
        }

        var query = _db.Products.Query()
            .Where(p => p.Status == ProductStatus.Active);

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(searchLower) ||
                p.Description.ToLower().Contains(searchLower));
        }

        var totalCount = query.Count();
        var products = query
            .OrderByDescending(p => p.CreatedAt)
            .ToEnumerable()
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var productDtos = products.Select(p =>
        {
            var seller = _db.Users.FindById(p.SellerId);
            return MapToDto(p, seller);
        }).ToList();

        var result = new ProductListResponse
        {
            Products = productDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        if (cacheKey != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration)
                .SetSize(pageSize * 1024); // Estimate ~1KB per product
            _cache.Set(cacheKey, result, cacheOptions);
        }

        return result;
    }

    public ProductDto? GetProduct(string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return null;

        product.ViewCount++;
        _db.Products.Update(product);

        var seller = _db.Users.FindById(product.SellerId);
        return MapToDto(product, seller);
    }

    public List<ProductDto> GetUserProducts(string userId)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return new List<ProductDto>();

        var products = _db.Products.Find(p => p.SellerId == userId).ToList();
        return products.Select(p => MapToDto(p, user)).ToList();
    }

    public ProductDto? UpdateProduct(string userId, string productId, UpdateProductRequest request)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return null;
        if (product.SellerId != userId) return null;
        if (product.Status == ProductStatus.Sold) return null;

        if (!string.IsNullOrWhiteSpace(request.Title))
            product.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.Description))
            product.Description = request.Description;
        if (request.Price.HasValue && request.Price.Value > 0)
            product.Price = request.Price.Value;
        if (request.Category.HasValue)
            product.Category = request.Category.Value;
        if (request.ImageUrls != null && request.ImageUrls.Count > 0)
            product.ImageUrls = request.ImageUrls;
        if (request.Tags != null)
            product.Tags = request.Tags;

        product.UpdatedAt = DateTime.UtcNow;
        _db.Products.Update(product);

        var seller = _db.Users.FindById(product.SellerId);
        return MapToDto(product, seller);
    }

    public bool DeleteProduct(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return false;
        if (product.SellerId != userId) return false;
        if (product.Status == ProductStatus.Sold) return false;

        product.Status = ProductStatus.Removed;
        product.UpdatedAt = DateTime.UtcNow;
        _db.Products.Update(product);

        return true;
    }

    public bool LikeProduct(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return false;

        product.LikeCount++;
        _db.Products.Update(product);

        return true;
    }

    public bool PurchaseProduct(string buyerId, string productId)
    {
        var product = _db.Products.FindById(productId);
        var buyer = _db.Users.FindById(buyerId);

        if (product == null || buyer == null) return false;
        if (product.Status != ProductStatus.Active) return false;
        if (buyer.Balance < product.Price) return false;
        if (product.SellerId == buyerId) return false;

        var seller = _db.Users.FindById(product.SellerId);
        if (seller == null) return false;

        // Process transaction
        buyer.Balance -= product.Price;
        buyer.TotalPurchases++;
        seller.Balance += product.Price * 0.95m; // 5% fee
        seller.TotalSales++;

        // Update rank based on sales
        seller.Rank = seller.TotalSales switch
        {
            >= 100 => UserRank.Legend,
            >= 50 => UserRank.Elite,
            >= 25 => UserRank.Diamond,
            >= 15 => UserRank.Platinum,
            >= 10 => UserRank.Gold,
            >= 5 => UserRank.Silver,
            >= 1 => UserRank.Bronze,
            _ => UserRank.Newcomer
        };

        product.Status = ProductStatus.Sold;
        product.BuyerId = buyerId;
        product.SoldAt = DateTime.UtcNow;

        var transaction = new Transaction
        {
            BuyerId = buyerId,
            SellerId = product.SellerId,
            ProductId = productId,
            ProductTitle = product.Title,
            Amount = product.Price,
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        _db.Users.Update(buyer);
        _db.Users.Update(seller);
        _db.Products.Update(product);
        _db.Transactions.Insert(transaction);

        return true;
    }

    private static ProductDto MapToDto(Product product, User? seller)
    {
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
            IsFeatured = product.IsFeatured
        };
    }
}
