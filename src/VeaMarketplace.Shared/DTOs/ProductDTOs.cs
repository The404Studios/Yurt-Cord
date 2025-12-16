using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class CreateProductRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public class UpdateProductRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public ProductStatus Status { get; set; }
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
    public decimal OriginalPrice { get; set; }
    public int Stock { get; set; } = -1; // -1 = unlimited
    public ProductCategory Category { get; set; }
    public ProductStatus Status { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsFeatured { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class ProductListResponse
{
    public List<ProductDto> Products { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
