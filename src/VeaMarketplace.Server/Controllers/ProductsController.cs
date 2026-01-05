using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly AuthService _authService;
    private readonly ActivityService _activityService;
    private readonly IHubContext<ContentHub> _contentHubContext;

    public ProductsController(
        ProductService productService,
        AuthService authService,
        ActivityService activityService,
        IHubContext<ContentHub> contentHubContext)
    {
        _productService = productService;
        _authService = authService;
        _activityService = activityService;
        _contentHubContext = contentHubContext;
    }

    [HttpGet]
    [OutputCache(PolicyName = "Products")]
    public ActionResult<ProductListResponse> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ProductCategory? category = null,
        [FromQuery] string? search = null)
    {
        var products = _productService.GetProducts(page, pageSize, category, search);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public ActionResult<ProductDto> GetProduct(string id)
    {
        var product = _productService.GetProduct(id);
        if (product == null)
            return NotFound();
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] CreateProductRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required");

        if (request.Price <= 0)
            return BadRequest("Price must be greater than 0");

        var product = _productService.CreateProduct(user.Id, request);
        if (product == null)
            return BadRequest("Unable to create product");

        // Log activity for product listing
        _activityService.LogProductListed(user.Id, product.Id);

        // Broadcast new product event to all connected clients
        await ContentHub.BroadcastNewProduct(_contentHubContext, new NewProductEvent
        {
            SourceUserId = user.Id,
            SourceUsername = user.Username,
            SourceAvatarUrl = user.AvatarUrl,
            Product = product,
            ShareLink = $"vea://marketplace/product/{product.Id}"
        });

        // Also broadcast as a new post event for the feed
        await ContentHub.BroadcastNewPost(_contentHubContext, new NewPostEvent
        {
            SourceUserId = user.Id,
            SourceUsername = user.Username,
            SourceAvatarUrl = user.AvatarUrl,
            PostId = product.Id,
            Title = product.Title,
            Description = product.Description,
            PreviewImageUrl = product.ImageUrls.FirstOrDefault(),
            ContentType = PostContentType.Product,
            Price = product.Price,
            IsAuction = false,
            Category = product.Category.ToString(),
            Tags = product.Tags
        });

        return Ok(product);
    }

    [HttpPost("{id}/purchase")]
    public ActionResult PurchaseProduct(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_productService.PurchaseProduct(user.Id, id))
            return Ok(new { Success = true, Message = "Purchase successful" });

        return BadRequest(new { Success = false, Message = "Purchase failed. Check your balance." });
    }

    [HttpGet("my")]
    public ActionResult<List<ProductDto>> GetMyProducts(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var products = _productService.GetUserProducts(user.Id);
        return Ok(products);
    }

    [HttpPut("{id}")]
    public ActionResult<ProductDto> UpdateProduct(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] UpdateProductRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var product = _productService.UpdateProduct(user.Id, id, request);
        if (product == null)
            return NotFound(new { Message = "Product not found or you don't have permission to update it" });

        return Ok(product);
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteProduct(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_productService.DeleteProduct(user.Id, id))
            return Ok(new { Success = true, Message = "Product deleted successfully" });

        return NotFound(new { Success = false, Message = "Product not found or you don't have permission to delete it" });
    }

    [HttpPost("{id}/like")]
    public ActionResult LikeProduct(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_productService.LikeProduct(user.Id, id))
            return Ok(new { Success = true });

        return NotFound(new { Success = false, Message = "Product not found" });
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
