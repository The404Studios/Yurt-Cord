using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ProductService
{
    private readonly DatabaseService _db;

    public ProductService(DatabaseService db)
    {
        _db = db;
    }

    public ProductDto CreateProduct(string userId, CreateProductRequest request)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) throw new Exception("User not found");

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
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();

        var productDtos = products.Select(p =>
        {
            var seller = _db.Users.FindById(p.SellerId);
            return MapToDto(p, seller);
        }).ToList();

        return new ProductListResponse
        {
            Products = productDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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
