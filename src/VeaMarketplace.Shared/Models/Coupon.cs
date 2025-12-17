using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class Coupon
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public CouponType Type { get; set; }
    public decimal Value { get; set; } // Percentage or fixed amount
    public decimal? MinimumPurchase { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; } = 0;
    public List<string>? ApplicableProductIds { get; set; } // null = all products
    public List<ProductCategory>? ApplicableCategories { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsOneTimePerUser { get; set; } = false;
    public List<string> UsedByUserIds { get; set; } = new();

    // Aliases for backward compatibility
    public decimal DiscountValue
    {
        get => Value;
        set => Value = value;
    }

    public decimal? MaxDiscount
    {
        get => MaximumDiscount;
        set => MaximumDiscount = value;
    }

    public int? UsageLimit
    {
        get => MaxUses;
        set => MaxUses = value;
    }

    public int UsageCount
    {
        get => CurrentUses;
        set => CurrentUses = value;
    }
}
