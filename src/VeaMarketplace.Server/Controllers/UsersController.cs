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

        return Ok(_authService.MapToDto(user));
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

        var result = _authService.UpdateProfile(user.Id, request);
        if (result == null)
            return BadRequest("Username already taken");

        return Ok(result);
    }

    [HttpGet("{id}/roles")]
    public ActionResult<List<CustomRoleDto>> GetUserRoles(string id)
    {
        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound();

        var roles = user.CustomRoleIds
            .Select(roleId => _db.CustomRoles.FindById(roleId))
            .Where(r => r != null)
            .OrderByDescending(r => r!.Position)
            .Select(r => new CustomRoleDto
            {
                Id = r!.Id,
                Name = r.Name,
                Color = r.Color,
                Position = r.Position,
                IsHoisted = r.IsHoisted,
                Permissions = r.Permissions
            })
            .ToList();

        return Ok(roles);
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
