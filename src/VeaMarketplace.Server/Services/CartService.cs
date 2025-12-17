using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class CartService
{
    private readonly DatabaseService _db;
    private readonly OrderService _orderService;
    private const decimal FEE_PERCENT = 0.07m; // 7% total fees

    public CartService(DatabaseService db, OrderService orderService)
    {
        _db = db;
        _orderService = orderService;
    }

    public CartDto GetCart(string userId)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Insert(cart);
        }

        return MapToDto(cart);
    }

    public CartDto? AddToCart(string userId, AddToCartRequest request)
    {
        var product = _db.Products.FindById(request.ProductId);
        if (product == null || product.Status != ProductStatus.Active) return null;
        if (product.SellerId == userId) return null; // Can't add own product

        var seller = _db.Users.FindById(product.SellerId);
        if (seller == null) return null;

        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Insert(cart);
        }

        // Check if product already in cart
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                ProductTitle = product.Title,
                ProductImageUrl = product.ImageUrls.FirstOrDefault() ?? "",
                SellerId = product.SellerId,
                SellerUsername = seller.Username,
                Price = product.Price,
                Quantity = request.Quantity,
                AddedAt = DateTime.UtcNow
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        _db.Carts.Update(cart);

        return MapToDto(cart);
    }

    public CartDto? UpdateCartItem(string userId, UpdateCartItemRequest request)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart == null) return null;

        var item = cart.Items.FirstOrDefault(i => i.Id == request.ItemId);
        if (item == null) return null;

        if (request.Quantity <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            item.Quantity = request.Quantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        _db.Carts.Update(cart);

        return MapToDto(cart);
    }

    public CartDto? RemoveFromCart(string userId, string itemId)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart == null) return null;

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            _db.Carts.Update(cart);
        }

        return MapToDto(cart);
    }

    public CartDto ClearCart(string userId)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart != null)
        {
            cart.Items.Clear();
            cart.AppliedCouponCode = null;
            cart.CouponDiscount = 0;
            cart.UpdatedAt = DateTime.UtcNow;
            _db.Carts.Update(cart);
        }
        else
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Insert(cart);
        }

        return MapToDto(cart);
    }

    public CouponResultDto ApplyCoupon(string userId, string couponCode)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart == null || cart.Items.Count == 0)
        {
            return new CouponResultDto { IsValid = false, ErrorMessage = "Cart is empty" };
        }

        var coupon = _db.Coupons.FindOne(c => c.Code.ToLower() == couponCode.ToLower());
        if (coupon == null)
        {
            return new CouponResultDto { IsValid = false, ErrorMessage = "Invalid coupon code" };
        }

        if (!coupon.IsActive)
        {
            return new CouponResultDto { IsValid = false, ErrorMessage = "Coupon is not active" };
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new CouponResultDto { IsValid = false, ErrorMessage = "Coupon has expired" };
        }

        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value)
        {
            return new CouponResultDto { IsValid = false, ErrorMessage = "Coupon usage limit reached" };
        }

        var subtotal = cart.Items.Sum(i => i.Price * i.Quantity);
        if (coupon.MinimumPurchase.HasValue && subtotal < coupon.MinimumPurchase.Value)
        {
            return new CouponResultDto
            {
                IsValid = false,
                ErrorMessage = $"Minimum purchase of ${coupon.MinimumPurchase.Value:F2} required"
            };
        }

        // Calculate discount
        decimal discount = coupon.Type switch
        {
            CouponType.Percentage => subtotal * (coupon.DiscountValue / 100m),
            CouponType.FixedAmount => coupon.DiscountValue,
            _ => 0
        };

        if (coupon.MaxDiscount.HasValue && discount > coupon.MaxDiscount.Value)
        {
            discount = coupon.MaxDiscount.Value;
        }

        cart.AppliedCouponCode = coupon.Code;
        cart.CouponDiscount = discount;
        cart.UpdatedAt = DateTime.UtcNow;
        _db.Carts.Update(cart);

        return new CouponResultDto
        {
            IsValid = true,
            CouponCode = coupon.Code,
            DiscountAmount = discount,
            DiscountDescription = coupon.Type == CouponType.Percentage
                ? $"{coupon.DiscountValue}% off"
                : $"${coupon.DiscountValue:F2} off"
        };
    }

    public CouponResultDto RemoveCoupon(string userId)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        if (cart != null)
        {
            cart.AppliedCouponCode = null;
            cart.CouponDiscount = 0;
            cart.UpdatedAt = DateTime.UtcNow;
            _db.Carts.Update(cart);
        }

        return new CouponResultDto { IsValid = true };
    }

    public CheckoutResultDto Checkout(string userId, CheckoutRequest request)
    {
        var cart = _db.Carts.FindOne(c => c.UserId == userId);
        var user = _db.Users.FindById(userId);

        if (cart == null || cart.Items.Count == 0)
        {
            return new CheckoutResultDto { Success = false, ErrorMessage = "Cart is empty" };
        }

        if (user == null)
        {
            return new CheckoutResultDto { Success = false, ErrorMessage = "User not found" };
        }

        // Validate all items are still available
        var unavailableItems = new List<string>();
        foreach (var item in cart.Items)
        {
            var product = _db.Products.FindById(item.ProductId);
            if (product == null || product.Status != ProductStatus.Active)
            {
                unavailableItems.Add(item.ProductTitle);
            }
        }

        if (unavailableItems.Count > 0)
        {
            return new CheckoutResultDto
            {
                Success = false,
                ErrorMessage = $"Some items are no longer available: {string.Join(", ", unavailableItems)}"
            };
        }

        // Calculate total
        var subtotal = cart.Items.Sum(i => i.Price * i.Quantity);
        var fees = subtotal * FEE_PERCENT;
        var total = subtotal + fees - cart.CouponDiscount;

        if (user.Balance < total)
        {
            return new CheckoutResultDto
            {
                Success = false,
                ErrorMessage = $"Insufficient balance. Required: ${total:F2}, Available: ${user.Balance:F2}"
            };
        }

        // Create orders for each item
        var orders = new List<OrderDto>();
        foreach (var item in cart.Items)
        {
            var orderRequest = new CreateOrderRequest
            {
                ProductId = item.ProductId,
                PaymentMethod = Enum.TryParse<PaymentMethod>(request.PaymentMethod, out var pm) ? pm : PaymentMethod.Balance
            };

            var order = _orderService.CreateOrder(userId, orderRequest);
            if (order != null)
            {
                orders.Add(order);
            }
        }

        // Update coupon usage
        if (!string.IsNullOrEmpty(cart.AppliedCouponCode))
        {
            var coupon = _db.Coupons.FindOne(c => c.Code == cart.AppliedCouponCode);
            if (coupon != null)
            {
                coupon.UsageCount++;
                _db.Coupons.Update(coupon);
            }
        }

        // Clear the cart
        ClearCart(userId);

        return new CheckoutResultDto
        {
            Success = true,
            Orders = orders,
            TotalPaid = total
        };
    }

    private CartDto MapToDto(Cart cart)
    {
        var items = cart.Items.Select(item =>
        {
            var product = _db.Products.FindById(item.ProductId);
            return new CartItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductTitle = item.ProductTitle,
                ProductImageUrl = item.ProductImageUrl,
                SellerId = item.SellerId,
                SellerUsername = item.SellerUsername,
                Price = item.Price,
                Quantity = item.Quantity,
                AddedAt = item.AddedAt,
                IsAvailable = product != null && product.Status == ProductStatus.Active
            };
        }).ToList();

        var subtotal = items.Where(i => i.IsAvailable).Sum(i => i.Price * i.Quantity);
        var fees = subtotal * FEE_PERCENT;

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = items,
            Subtotal = subtotal,
            Fees = fees,
            Total = subtotal + fees - cart.CouponDiscount,
            ItemCount = items.Count,
            UpdatedAt = cart.UpdatedAt
        };
    }
}
