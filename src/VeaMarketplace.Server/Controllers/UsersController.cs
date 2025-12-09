using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly AuthService _authService;

    public UsersController(DatabaseService db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    [HttpGet("{id}")]
    public ActionResult<UserDto> GetUser(string id)
    {
        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound();

        return Ok(AuthService.MapToDto(user));
    }

    [HttpGet("{id}/products")]
    public ActionResult<List<ProductDto>> GetUserProducts(string id)
    {
        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound();

        var products = _db.Products
            .Find(p => p.SellerId == id && p.Status == Shared.Enums.ProductStatus.Active)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                SellerId = p.SellerId,
                SellerUsername = p.SellerUsername,
                SellerRole = user.Role,
                SellerRank = user.Rank,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                Category = p.Category,
                Status = p.Status,
                ImageUrls = p.ImageUrls,
                Tags = p.Tags,
                ViewCount = p.ViewCount,
                LikeCount = p.LikeCount,
                CreatedAt = p.CreatedAt,
                IsFeatured = p.IsFeatured
            })
            .ToList();

        return Ok(products);
    }

    [HttpPut("profile")]
    public ActionResult<UserDto> UpdateProfile(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] UpdateProfileRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.Bio))
            user.Bio = request.Bio;

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl;

        _db.Users.Update(user);

        return Ok(AuthService.MapToDto(user));
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

public class UpdateProfileRequest
{
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
