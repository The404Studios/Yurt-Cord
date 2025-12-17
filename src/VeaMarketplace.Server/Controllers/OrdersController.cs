using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;
    private readonly ActivityService _activityService;
    private readonly IHubContext<ContentHub> _contentHub;

    public OrdersController(
        OrderService orderService,
        AuthService authService,
        NotificationService notificationService,
        ActivityService activityService,
        IHubContext<ContentHub> contentHub)
    {
        _orderService = orderService;
        _authService = authService;
        _notificationService = notificationService;
        _activityService = activityService;
        _contentHub = contentHub;
    }

    [HttpGet]
    public ActionResult<OrderHistoryDto> GetOrders(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var orders = _orderService.GetUserOrders(user.Id, status, page, pageSize);
        return Ok(orders);
    }

    [HttpGet("{orderId}")]
    public ActionResult<OrderDto> GetOrder(
        string orderId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var order = _orderService.GetOrder(orderId, user.Id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] CreateOrderRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var order = _orderService.CreateOrder(user.Id, request);
        if (order == null)
            return BadRequest("Unable to create order. Check product availability and your balance.");

        // Notify seller
        _notificationService.NotifyProductSold(order.SellerId, order.ProductTitle, user.Username, order.TotalAmount);

        // Log activity
        _activityService.LogProductPurchased(user.Id, order.ProductId);
        _activityService.LogProductSold(order.SellerId, order.ProductId);

        // Broadcast order event
        await _contentHub.Clients.User(order.SellerId).SendAsync("OrderReceived", order);

        return Ok(order);
    }

    [HttpPost("{orderId}/complete")]
    public async Task<ActionResult<OrderDto>> CompleteOrder(
        string orderId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var order = _orderService.CompleteOrder(orderId, user.Id);
        if (order == null)
            return BadRequest("Unable to complete order");

        // Notify seller
        _notificationService.NotifyOrderUpdate(order.SellerId, order.Id, "completed", order.ProductTitle);

        // Broadcast completion
        await _contentHub.Clients.User(order.SellerId).SendAsync("OrderCompleted", order);

        return Ok(order);
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(
        string orderId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] CancelOrderRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var order = _orderService.CancelOrder(orderId, user.Id, request.Reason);
        if (order == null)
            return BadRequest("Unable to cancel order");

        // Notify the other party
        var otherUserId = order.BuyerId == user.Id ? order.SellerId : order.BuyerId;
        _notificationService.NotifyOrderUpdate(otherUserId, order.Id, "cancelled", order.ProductTitle);

        await _contentHub.Clients.User(otherUserId).SendAsync("OrderCancelled", order);

        return Ok(order);
    }

    [HttpPost("{orderId}/dispute")]
    public async Task<ActionResult<OrderDto>> DisputeOrder(
        string orderId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] DisputeOrderRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        request.OrderId = orderId;
        var order = _orderService.DisputeOrder(orderId, user.Id, request);
        if (order == null)
            return BadRequest("Unable to dispute order");

        // Notify seller and admins
        _notificationService.NotifyOrderUpdate(order.SellerId, order.Id, "disputed", order.ProductTitle);

        await _contentHub.Clients.User(order.SellerId).SendAsync("OrderDisputed", order);

        return Ok(order);
    }

    [HttpPost("{orderId}/resolve-dispute")]
    public async Task<ActionResult<OrderDto>> ResolveDispute(
        string orderId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] ResolveDisputeRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        // Only moderators can resolve disputes
        if (user.Role < UserRole.Moderator)
            return Forbid();

        var order = _orderService.ResolveDispute(orderId, user.Id, request.Resolution, request.RefundBuyer);
        if (order == null)
            return BadRequest("Unable to resolve dispute");

        // Notify both parties
        _notificationService.NotifyOrderUpdate(order.BuyerId, order.Id, "dispute resolved", order.ProductTitle);
        _notificationService.NotifyOrderUpdate(order.SellerId, order.Id, "dispute resolved", order.ProductTitle);

        await _contentHub.Clients.Users(new[] { order.BuyerId, order.SellerId })
            .SendAsync("DisputeResolved", order);

        return Ok(order);
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

public class CancelOrderRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ResolveDisputeRequest
{
    public string Resolution { get; set; } = string.Empty;
    public bool RefundBuyer { get; set; }
}
