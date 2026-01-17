using System.ComponentModel.DataAnnotations;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class ProductBundleDto
{
    public string Id { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = new();
    public List<ProductDto> Products { get; set; } = new();
    public decimal OriginalPrice { get; set; }
    public decimal BundlePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int SalesCount { get; set; } = 0;
}

public class CreateBundleRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "At least one product is required")]
    [MinLength(1, ErrorMessage = "At least one product is required")]
    public List<string> ProductIds { get; set; } = new();

    [Required(ErrorMessage = "Bundle price is required")]
    [Range(0.01, 999999.99, ErrorMessage = "Price must be between 0.01 and 999,999.99")]
    public decimal BundlePrice { get; set; }

    [Url(ErrorMessage = "Invalid image URL")]
    public string? ImageUrl { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class CouponDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public CouponType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinimumPurchase { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsOneTimePerUser { get; set; } = false;
}

public class ValidateCouponRequest
{
    [Required(ErrorMessage = "Coupon code is required")]
    [StringLength(50, ErrorMessage = "Coupon code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product ID is required")]
    public string ProductId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Purchase amount must be greater than 0")]
    public decimal PurchaseAmount { get; set; }
}

public class ValidateCouponResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
}
