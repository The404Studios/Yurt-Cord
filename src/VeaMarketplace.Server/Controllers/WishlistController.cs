using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WishlistController : ControllerBase
{
    private readonly WishlistService _wishlistService;
    private readonly AuthService _authService;

    public WishlistController(WishlistService wishlistService, AuthService authService)
    {
        _wishlistService = wishlistService;
        _authService = authService;
    }

    [HttpGet]
    public ActionResult<List<WishlistItemDto>> GetWishlist(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var wishlist = _wishlistService.GetWishlist(user.Id);
        return Ok(wishlist);
    }

    [HttpPost]
    public ActionResult<WishlistItemDto> AddToWishlist(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] AddToWishlistRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var item = _wishlistService.AddToWishlist(user.Id, request);
        if (item == null)
            return BadRequest("Unable to add item to wishlist. Product may not exist.");

        return Ok(item);
    }

    [HttpPut("{wishlistItemId}")]
    public ActionResult<WishlistItemDto> UpdateWishlistItem(
        string wishlistItemId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] UpdateWishlistItemRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        request.WishlistItemId = wishlistItemId;
        var item = _wishlistService.UpdateWishlistItem(user.Id, request);
        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpDelete("{productId}")]
    public ActionResult RemoveFromWishlist(
        string productId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_wishlistService.RemoveFromWishlist(user.Id, productId))
            return Ok(new { Success = true });

        return NotFound();
    }

    [HttpDelete]
    public ActionResult ClearWishlist(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _wishlistService.ClearWishlist(user.Id);
        return Ok(new { Success = true, RemovedCount = count });
    }

    [HttpGet("check/{productId}")]
    public ActionResult<bool> IsInWishlist(
        string productId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var isInWishlist = _wishlistService.IsInWishlist(user.Id, productId);
        return Ok(new { IsInWishlist = isInWishlist });
    }

    [HttpGet("count")]
    public ActionResult<int> GetWishlistCount(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _wishlistService.GetWishlistCount(user.Id);
        return Ok(new { Count = count });
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
