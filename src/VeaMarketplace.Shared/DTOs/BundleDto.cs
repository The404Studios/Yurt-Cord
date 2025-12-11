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
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = new();
    public decimal BundlePrice { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CouponDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
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
    public string Code { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public decimal PurchaseAmount { get; set; }
}

public class ValidateCouponResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
}
