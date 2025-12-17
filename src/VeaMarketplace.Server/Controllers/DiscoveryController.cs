using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class DiscoveryController : ControllerBase
{
    private readonly DiscoveryService _discoveryService;
    private readonly AuthService _authService;

    public DiscoveryController(DiscoveryService discoveryService, AuthService authService)
    {
        _discoveryService = discoveryService;
        _authService = authService;
    }

    [HttpGet("featured")]
    [OutputCache(PolicyName = "Discovery")]
    public ActionResult<List<ProductDto>> GetFeaturedProducts([FromQuery] int limit = 10)
    {
        var products = _discoveryService.GetFeaturedProducts(limit);
        return Ok(products);
    }

    [HttpGet("trending")]
    [OutputCache(PolicyName = "Products")]
    public ActionResult<List<ProductDto>> GetTrendingProducts([FromQuery] int limit = 20)
    {
        var products = _discoveryService.GetTrendingProducts(limit);
        return Ok(products);
    }

    [HttpGet("new")]
    [OutputCache(PolicyName = "Products")]
    public ActionResult<List<ProductDto>> GetNewArrivals([FromQuery] int limit = 20)
    {
        var products = _discoveryService.GetNewArrivals(limit);
        return Ok(products);
    }

    [HttpGet("top-rated")]
    [OutputCache(PolicyName = "Discovery")]
    public ActionResult<List<ProductDto>> GetTopRated([FromQuery] int limit = 20)
    {
        var products = _discoveryService.GetTopRated(limit);
        return Ok(products);
    }

    [HttpGet("best-sellers")]
    [OutputCache(PolicyName = "Discovery")]
    public ActionResult<List<ProductDto>> GetBestSellers(
        [FromQuery] ProductCategory? category = null,
        [FromQuery] int limit = 20)
    {
        var products = _discoveryService.GetBestSellers(category, limit);
        return Ok(products);
    }

    [HttpGet("top-sellers")]
    [OutputCache(PolicyName = "Discovery")]
    public ActionResult<List<SellerProfileDto>> GetTopSellers([FromQuery] int limit = 10)
    {
        var sellers = _discoveryService.GetTopSellers(limit);
        return Ok(sellers);
    }

    [HttpGet("recommended")]
    public ActionResult<List<ProductDto>> GetRecommendedProducts(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] int limit = 20)
    {
        var user = GetUserFromToken(authorization);
        var products = _discoveryService.GetRecommendedProducts(user?.Id, limit);
        return Ok(products);
    }

    [HttpGet("similar/{productId}")]
    [OutputCache(PolicyName = "Products")]
    public ActionResult<List<ProductDto>> GetSimilarProducts(
        string productId,
        [FromQuery] int limit = 10)
    {
        var products = _discoveryService.GetSimilarProducts(productId, limit);
        return Ok(products);
    }

    [HttpGet("recently-viewed")]
    public ActionResult<List<ProductDto>> GetRecentlyViewed(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] int limit = 10)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var products = _discoveryService.GetRecentlyViewed(user.Id, limit);
        return Ok(products);
    }

    [HttpGet("category/{category}")]
    [OutputCache(PolicyName = "Products")]
    public ActionResult<List<ProductDto>> GetProductsByCategory(
        ProductCategory category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var products = _discoveryService.GetProductsByCategory(category, page, pageSize);
        return Ok(products);
    }

    [HttpGet("categories")]
    [OutputCache(PolicyName = "Static")]
    public ActionResult<Dictionary<ProductCategory, int>> GetCategoryCounts()
    {
        var counts = _discoveryService.GetCategoryCounts();
        return Ok(counts);
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
