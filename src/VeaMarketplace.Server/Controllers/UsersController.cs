using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly AuthService _authService;
    private readonly FriendService _friendService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public UsersController(DatabaseService db, AuthService authService, FriendService friendService, IHubContext<ChatHub> chatHubContext)
    {
        _db = db;
        _authService = authService;
        _friendService = friendService;
        _chatHubContext = chatHubContext;
    }

    [HttpGet("{id}")]
    public ActionResult<UserDto> GetUser(string id)
    {
        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound();

        return Ok(_authService.MapToDto(user));
    }

    // Public profile endpoint with visibility enforcement
    [HttpGet("{id}/profile")]
    public ActionResult<UserDto> GetPublicProfile(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var targetUser = _db.Users.FindById(id);
        if (targetUser == null)
            return NotFound();

        var requestingUser = GetUserFromToken(authorization);
        var requesterId = requestingUser?.Id;

        // Check visibility
        if (targetUser.ProfileVisibility == ProfileVisibility.Private && requesterId != id)
        {
            return Forbid("This profile is private");
        }

        if (targetUser.ProfileVisibility == ProfileVisibility.FriendsOnly && requesterId != id)
        {
            if (requesterId == null || !_friendService.AreFriends(requesterId, id))
            {
                // Return limited profile
                return Ok(new UserDto
                {
                    Id = targetUser.Id,
                    Username = targetUser.Username,
                    DisplayName = targetUser.DisplayName,
                    AvatarUrl = targetUser.AvatarUrl,
                    Role = targetUser.Role,
                    Rank = targetUser.Rank,
                    IsOnline = targetUser.IsOnline,
                    CreatedAt = targetUser.CreatedAt
                });
            }
        }

        return Ok(_authService.MapToDto(targetUser));
    }

    // Search users by ID or username
    [HttpGet("search")]
    public ActionResult<List<UserSearchResultDto>> SearchUsers(
        [FromQuery] string query,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query is required");

        var requestingUser = GetUserFromToken(authorization);
        var requesterId = requestingUser?.Id;

        var users = _friendService.SearchUsers(query, 20);
        var results = users
            .Where(u => requesterId == null || u.Id != requesterId)
            .Select(u => new UserSearchResultDto
            {
                UserId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Bio = u.ProfileVisibility == ProfileVisibility.Public ? u.Bio : string.Empty,
                StatusMessage = u.ProfileVisibility == ProfileVisibility.Public ? u.StatusMessage : string.Empty,
                Role = u.Role,
                Rank = u.Rank,
                IsOnline = u.IsOnline,
                IsFriend = requesterId != null && _friendService.AreFriends(requesterId, u.Id)
            })
            .ToList();

        return Ok(results);
    }

    // Lookup user by exact ID or username
    [HttpGet("lookup")]
    public ActionResult<UserSearchResultDto?> LookupUser(
        [FromQuery] string query,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query is required");

        var requestingUser = GetUserFromToken(authorization);
        var requesterId = requestingUser?.Id;

        var user = _friendService.SearchUserByIdOrUsername(query);
        if (user == null || (requesterId != null && user.Id == requesterId))
            return Ok((UserSearchResultDto?)null);

        return Ok(new UserSearchResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Bio = user.ProfileVisibility == ProfileVisibility.Public ? user.Bio : string.Empty,
            StatusMessage = user.ProfileVisibility == ProfileVisibility.Public ? user.StatusMessage : string.Empty,
            Role = user.Role,
            Rank = user.Rank,
            IsOnline = user.IsOnline,
            IsFriend = requesterId != null && _friendService.AreFriends(requesterId, user.Id)
        });
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
    public async Task<ActionResult<UserDto>> UpdateProfile(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] UpdateProfileRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _authService.UpdateProfile(user.Id, request);
        if (result == null)
            return BadRequest("Username already taken");

        // Broadcast profile update to all connected clients via SignalR
        var updatedUser = _db.Users.FindById(user.Id);
        if (updatedUser != null)
        {
            await ChatHub.BroadcastProfileUpdate(_chatHubContext, updatedUser);
        }

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

    #region Whitelist Management (Admin Only)

    /// <summary>
    /// Whitelist a user (Admin only). Used in Whitelist authentication mode.
    /// </summary>
    [HttpPost("{id}/whitelist")]
    public ActionResult<WhitelistResponse> WhitelistUser(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var admin = GetUserFromToken(authorization);
        if (admin == null)
            return Unauthorized();

        // Only admins and owners can manage whitelist
        if (admin.Role != Shared.Enums.UserRole.Admin && admin.Role != Shared.Enums.UserRole.Owner)
            return Forbid("Only admins can manage the whitelist");

        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound(new WhitelistResponse { Success = false, Message = "User not found" });

        user.IsWhitelisted = true;
        _db.Users.Update(user);

        return Ok(new WhitelistResponse { Success = true, Message = $"User {user.Username} has been whitelisted" });
    }

    /// <summary>
    /// Remove a user from the whitelist (Admin only).
    /// </summary>
    [HttpDelete("{id}/whitelist")]
    public ActionResult<WhitelistResponse> RemoveFromWhitelist(
        string id,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var admin = GetUserFromToken(authorization);
        if (admin == null)
            return Unauthorized();

        // Only admins and owners can manage whitelist
        if (admin.Role != Shared.Enums.UserRole.Admin && admin.Role != Shared.Enums.UserRole.Owner)
            return Forbid("Only admins can manage the whitelist");

        var user = _db.Users.FindById(id);
        if (user == null)
            return NotFound(new WhitelistResponse { Success = false, Message = "User not found" });

        user.IsWhitelisted = false;
        _db.Users.Update(user);

        return Ok(new WhitelistResponse { Success = true, Message = $"User {user.Username} has been removed from the whitelist" });
    }

    /// <summary>
    /// Get all whitelisted users (Admin only).
    /// </summary>
    [HttpGet("whitelisted")]
    public ActionResult<List<UserDto>> GetWhitelistedUsers(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var admin = GetUserFromToken(authorization);
        if (admin == null)
            return Unauthorized();

        // Only admins and owners can view whitelist
        if (admin.Role != Shared.Enums.UserRole.Admin && admin.Role != Shared.Enums.UserRole.Owner)
            return Forbid("Only admins can view the whitelist");

        var whitelistedUsers = _db.Users
            .Find(u => u.IsWhitelisted)
            .Select(u => _authService.MapToDto(u))
            .ToList();

        return Ok(whitelistedUsers);
    }

    #endregion

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

/// <summary>
/// Response for whitelist operations.
/// </summary>
public class WhitelistResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
