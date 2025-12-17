using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class OrderService
{
    private readonly DatabaseService _db;
    private const decimal LISTING_FEE_PERCENT = 0.02m; // 2%
    private const decimal SALES_FEE_PERCENT = 0.05m; // 5%

    public OrderService(DatabaseService db)
    {
        _db = db;
    }

    public OrderHistoryDto GetUserOrders(string userId, OrderStatus? status = null, int page = 1, int pageSize = 20)
    {
        var query = _db.Orders.Query()
            .Where(o => o.BuyerId == userId || o.SellerId == userId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var allOrders = query.OrderByDescending(o => o.CreatedAt).ToList();
        var totalCount = allOrders.Count;

        var orders = allOrders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => MapToDto(o, userId))
            .ToList();

        return new OrderHistoryDto
        {
            Orders = orders,
            TotalOrders = totalCount,
            TotalSpent = allOrders.Where(o => o.BuyerId == userId && o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
            PendingOrders = allOrders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing),
            CompletedOrders = allOrders.Count(o => o.Status == OrderStatus.Completed)
        };
    }

    public OrderDto? GetOrder(string orderId, string userId)
    {
        var order = _db.Orders.FindById(orderId);
        if (order == null) return null;

        // Check if user is buyer or seller
        if (order.BuyerId != userId && order.SellerId != userId) return null;

        return MapToDto(order, userId);
    }

    public OrderDto? CreateOrder(string buyerId, CreateOrderRequest request)
    {
        var buyer = _db.Users.FindById(buyerId);
        var product = _db.Products.FindById(request.ProductId);

        if (buyer == null || product == null) return null;
        if (product.Status != ProductStatus.Active) return null;
        if (product.SellerId == buyerId) return null; // Can't buy own product

        var seller = _db.Users.FindById(product.SellerId);
        if (seller == null) return null;

        // Calculate fees
        var listingFee = product.Price * LISTING_FEE_PERCENT;
        var salesFee = product.Price * SALES_FEE_PERCENT;
        var totalAmount = product.Price + listingFee + salesFee;

        // Check buyer balance
        if (buyer.Balance < totalAmount) return null;

        var order = new ProductOrder
        {
            ProductId = product.Id,
            BuyerId = buyerId,
            BuyerUsername = buyer.Username,
            SellerId = product.SellerId,
            SellerUsername = seller.Username,
            ProductPrice = product.Price,
            ListingFee = listingFee,
            SalesFee = salesFee,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            PaymentMethod = request.PaymentMethod,
            CreatedAt = DateTime.UtcNow,
            EscrowHeld = true
        };

        _db.Orders.Insert(order);

        // Deduct from buyer balance and hold in escrow
        buyer.Balance -= totalAmount;
        _db.Users.Update(buyer);

        return MapToDto(order, buyerId);
    }

    public OrderDto? ProcessPayment(string orderId, string userId)
    {
        var order = _db.Orders.FindById(orderId);
        if (order == null || order.BuyerId != userId) return null;
        if (order.Status != OrderStatus.Pending) return null;

        order.Status = OrderStatus.Processing;
        order.PaidAt = DateTime.UtcNow;
        _db.Orders.Update(order);

        return MapToDto(order, userId);
    }

    public OrderDto? CompleteOrder(string orderId, string userId)
    {
        var order = _db.Orders.FindById(orderId);
        if (order == null) return null;

        // Only buyer can complete order
        if (order.BuyerId != userId) return null;
        if (order.Status != OrderStatus.Processing && order.Status != OrderStatus.Paid) return null;

        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.EscrowHeld = false;
        order.EscrowReleasedAt = DateTime.UtcNow;
        _db.Orders.Update(order);

        // Release funds to seller (minus fees)
        var seller = _db.Users.FindById(order.SellerId);
        if (seller != null)
        {
            seller.Balance += order.ProductPrice - order.SalesFee;
            seller.TotalSales++;
            _db.Users.Update(seller);
        }

        // Update buyer stats
        var buyer = _db.Users.FindById(order.BuyerId);
        if (buyer != null)
        {
            buyer.TotalPurchases++;
            _db.Users.Update(buyer);
        }

        // Update product status
        var product = _db.Products.FindById(order.ProductId);
        if (product != null)
        {
            product.Status = ProductStatus.Sold;
            product.BuyerId = order.BuyerId;
            product.SoldAt = DateTime.UtcNow;
            _db.Products.Update(product);
        }

        return MapToDto(order, userId);
    }

    public OrderDto? CancelOrder(string orderId, string userId, string reason)
    {
        var order = _db.Orders.FindById(orderId);
        if (order == null) return null;

        // Only buyer or seller can cancel
        if (order.BuyerId != userId && order.SellerId != userId) return null;
        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled) return null;

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason;
        order.EscrowHeld = false;
        _db.Orders.Update(order);

        // Refund buyer
        var buyer = _db.Users.FindById(order.BuyerId);
        if (buyer != null)
        {
            buyer.Balance += order.TotalAmount;
            _db.Users.Update(buyer);
        }

        return MapToDto(order, userId);
    }

    public OrderDto? DisputeOrder(string orderId, string userId, DisputeOrderRequest request)
    {
        var order = _db.Orders.FindById(orderId);
        if (order == null || order.BuyerId != userId) return null;
        if (order.Status != OrderStatus.Processing && order.Status != OrderStatus.Paid) return null;

        order.Status = OrderStatus.Disputed;
        order.IsDisputed = true;
        order.DisputeReason = request.Reason;
        order.DisputedAt = DateTime.UtcNow;
        _db.Orders.Update(order);

        return MapToDto(order, userId);
    }

    public OrderDto? ResolveDispute(string orderId, string moderatorId, string resolution, bool refundBuyer)
    {
        var order = _db.Orders.FindById(orderId);
        var moderator = _db.Users.FindById(moderatorId);

        if (order == null || moderator == null) return null;
        if (moderator.Role < UserRole.Moderator) return null;
        if (!order.IsDisputed) return null;

        order.Status = OrderStatus.DisputeResolved;
        order.DisputeResolution = resolution;
        order.DisputeResolvedAt = DateTime.UtcNow;
        order.EscrowHeld = false;
        order.EscrowReleasedAt = DateTime.UtcNow;
        _db.Orders.Update(order);

        if (refundBuyer)
        {
            var buyer = _db.Users.FindById(order.BuyerId);
            if (buyer != null)
            {
                buyer.Balance += order.TotalAmount;
                _db.Users.Update(buyer);
            }
        }
        else
        {
            var seller = _db.Users.FindById(order.SellerId);
            if (seller != null)
            {
                seller.Balance += order.ProductPrice - order.SalesFee;
                seller.TotalSales++;
                _db.Users.Update(seller);
            }
        }

        return MapToDto(order, moderatorId);
    }

    private OrderDto MapToDto(ProductOrder order, string viewerId)
    {
        var product = _db.Products.FindById(order.ProductId);

        return new OrderDto
        {
            Id = order.Id,
            ProductId = order.ProductId,
            ProductTitle = product?.Title ?? "Unknown Product",
            ProductImageUrl = product?.ImageUrls.FirstOrDefault() ?? "",
            BuyerId = order.BuyerId,
            BuyerUsername = order.BuyerUsername,
            SellerId = order.SellerId,
            SellerUsername = order.SellerUsername,
            ProductPrice = order.ProductPrice,
            ListingFee = order.ListingFee,
            SalesFee = order.SalesFee,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentTransactionId = order.PaymentTransactionId,
            CreatedAt = order.CreatedAt,
            PaidAt = order.PaidAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            IsDisputed = order.IsDisputed,
            DisputeReason = order.DisputeReason,
            EscrowHeld = order.EscrowHeld,
            CanReview = order.Status == OrderStatus.Completed && order.BuyerId == viewerId,
            CanDispute = (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Paid) && order.BuyerId == viewerId,
            IsProcessing = order.Status == OrderStatus.Processing
        };
    }
}
