using System.ComponentModel.DataAnnotations;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class CreateProductRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 1000000, ErrorMessage = "Price must be between $0.01 and $1,000,000")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Category is required")]
    public ProductCategory Category { get; set; }

    [MinLength(1, ErrorMessage = "At least one image is required")]
    [MaxLength(10, ErrorMessage = "Maximum 10 images allowed")]
    public List<string> ImageUrls { get; set; } = [];

    [MaxLength(20, ErrorMessage = "Maximum 20 tags allowed")]
    public List<string> Tags { get; set; } = [];
}

public class UpdateProductRequest
{
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string? Title { get; set; }

    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
    public string? Description { get; set; }

    [Range(0.01, 1000000, ErrorMessage = "Price must be between $0.01 and $1,000,000")]
    public decimal? Price { get; set; }

    public ProductCategory? Category { get; set; }

    [MaxLength(10, ErrorMessage = "Maximum 10 images allowed")]
    public List<string>? ImageUrls { get; set; }

    [MaxLength(20, ErrorMessage = "Maximum 20 tags allowed")]
    public List<string>? Tags { get; set; }

    public ProductStatus? Status { get; set; }
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
