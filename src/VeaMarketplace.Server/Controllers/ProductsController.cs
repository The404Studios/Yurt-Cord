using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly AuthService _authService;

    public ProductsController(ProductService productService, AuthService authService)
    {
        _productService = productService;
        _authService = authService;
    }

    [HttpGet]
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
    public ActionResult<ProductDto> CreateProduct(
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

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
