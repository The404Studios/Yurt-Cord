using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly CartService _cartService;
    private readonly AuthService _authService;

    public CartController(CartService cartService, AuthService authService)
    {
        _cartService = cartService;
        _authService = authService;
    }

    [HttpGet]
    public ActionResult<CartDto> GetCart(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var cart = _cartService.GetCart(user.Id);
        return Ok(cart);
    }

    [HttpPost("items")]
    public ActionResult<CartDto> AddToCart(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] AddToCartRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var cart = _cartService.AddToCart(user.Id, request);
        if (cart == null)
            return BadRequest("Unable to add item to cart. Product may not be available.");

        return Ok(cart);
    }

    [HttpPut("items/{itemId}")]
    public ActionResult<CartDto> UpdateCartItem(
        string itemId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] UpdateCartItemRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var cart = _cartService.UpdateCartItem(user.Id, request);
        if (cart == null)
            return NotFound("Cart item not found");

        return Ok(cart);
    }

    [HttpDelete("items/{itemId}")]
    public ActionResult<CartDto> RemoveFromCart(
        string itemId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var cart = _cartService.RemoveFromCart(user.Id, itemId);
        if (cart == null)
            return NotFound();

        return Ok(cart);
    }

    [HttpDelete]
    public ActionResult<CartDto> ClearCart(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var cart = _cartService.ClearCart(user.Id);
        return Ok(cart);
    }

    [HttpPost("coupon")]
    public ActionResult<CouponResultDto> ApplyCoupon(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] ApplyCouponRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _cartService.ApplyCoupon(user.Id, request.CouponCode);
        if (!result.IsValid)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("coupon")]
    public ActionResult<CouponResultDto> RemoveCoupon(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _cartService.RemoveCoupon(user.Id);
        return Ok(result);
    }

    [HttpPost("checkout")]
    public ActionResult<CheckoutResultDto> Checkout(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] CheckoutRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _cartService.Checkout(user.Id, request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
