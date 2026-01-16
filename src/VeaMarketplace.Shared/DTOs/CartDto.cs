using System.ComponentModel.DataAnnotations;

namespace VeaMarketplace.Shared.DTOs;

public class CartDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Fees { get; set; }
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CartItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class AddToCartRequest
{
    [Required(ErrorMessage = "Product ID is required")]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    [Required(ErrorMessage = "Item ID is required")]
    public string ItemId { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "Quantity must be between 0 and 100")]
    public int Quantity { get; set; }
}

public class CheckoutRequest
{
    [Required(ErrorMessage = "Payment method is required")]
    public string PaymentMethod { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Coupon code cannot exceed 50 characters")]
    public string? CouponCode { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
}

public class CheckoutResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<OrderDto> Orders { get; set; } = [];
    public decimal TotalPaid { get; set; }
}

public class ApplyCouponRequest
{
    [Required(ErrorMessage = "Coupon code is required")]
    [StringLength(50, ErrorMessage = "Coupon code cannot exceed 50 characters")]
    public string CouponCode { get; set; } = string.Empty;
}

public class CouponResultDto
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountDescription { get; set; }
}
