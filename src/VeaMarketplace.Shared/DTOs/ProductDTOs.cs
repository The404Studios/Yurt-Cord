using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class CreateProductRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public UserRole SellerRole { get; set; }
    public UserRank SellerRank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public ProductStatus Status { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsFeatured { get; set; }
}

public class ProductListResponse
{
    public List<ProductDto> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
