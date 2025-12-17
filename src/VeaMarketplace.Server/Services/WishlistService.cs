using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class WishlistService
{
    private readonly DatabaseService _db;

    public WishlistService(DatabaseService db)
    {
        _db = db;
    }

    public List<WishlistItemDto> GetWishlist(string userId)
    {
        var items = _db.WishlistItems.Find(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .ToList();

        return items.Select(MapToDto).ToList();
    }

    public WishlistItemDto? AddToWishlist(string userId, AddToWishlistRequest request)
    {
        // Check if product exists
        var product = _db.Products.FindById(request.ProductId);
        if (product == null) return null;

        // Check if already in wishlist
        var existing = _db.WishlistItems.FindOne(w =>
            w.UserId == userId && w.ProductId == request.ProductId);
        if (existing != null) return MapToDto(existing);

        var item = new WishlistItem
        {
            UserId = userId,
            ProductId = request.ProductId,
            Notes = request.Notes,
            PriceWhenAdded = product.Price,
            NotifyOnPriceChange = request.NotifyOnPriceChange,
            NotifyWhenAvailable = request.NotifyWhenAvailable,
            AddedAt = DateTime.UtcNow
        };

        _db.WishlistItems.Insert(item);
        return MapToDto(item);
    }

    public WishlistItemDto? UpdateWishlistItem(string userId, UpdateWishlistItemRequest request)
    {
        var item = _db.WishlistItems.FindById(request.WishlistItemId);
        if (item == null || item.UserId != userId) return null;

        item.Notes = request.Notes;
        item.NotifyOnPriceChange = request.NotifyOnPriceChange;
        item.NotifyWhenAvailable = request.NotifyWhenAvailable;

        _db.WishlistItems.Update(item);
        return MapToDto(item);
    }

    public bool RemoveFromWishlist(string userId, string productId)
    {
        var item = _db.WishlistItems.FindOne(w =>
            w.UserId == userId && w.ProductId == productId);

        if (item == null) return false;

        _db.WishlistItems.Delete(item.Id);
        return true;
    }

    public int ClearWishlist(string userId)
    {
        var items = _db.WishlistItems.Find(w => w.UserId == userId).ToList();
        foreach (var item in items)
        {
            _db.WishlistItems.Delete(item.Id);
        }
        return items.Count;
    }

    public bool IsInWishlist(string userId, string productId)
    {
        return _db.WishlistItems.Exists(w =>
            w.UserId == userId && w.ProductId == productId);
    }

    public int GetWishlistCount(string userId)
    {
        return _db.WishlistItems.Count(w => w.UserId == userId);
    }

    private WishlistItemDto MapToDto(WishlistItem item)
    {
        var product = _db.Products.FindById(item.ProductId);
        var seller = product != null ? _db.Users.FindById(product.SellerId) : null;

        var hasPriceDropped = false;
        decimal? priceDropPercent = null;

        if (product != null && item.PriceWhenAdded.HasValue && product.Price < item.PriceWhenAdded.Value)
        {
            hasPriceDropped = true;
            priceDropPercent = Math.Round((1 - product.Price / item.PriceWhenAdded.Value) * 100, 1);
        }

        return new WishlistItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ProductId = item.ProductId,
            Product = product != null ? new ProductDto
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
            } : null,
            AddedAt = item.AddedAt,
            Notes = item.Notes,
            PriceWhenAdded = item.PriceWhenAdded,
            NotifyOnPriceChange = item.NotifyOnPriceChange,
            NotifyWhenAvailable = item.NotifyWhenAvailable,
            HasPriceDropped = hasPriceDropped,
            PriceDropPercent = priceDropPercent
        };
    }
}
