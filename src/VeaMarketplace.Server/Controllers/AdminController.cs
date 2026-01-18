using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly AuthService _authService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AdminService adminService,
        AuthService authService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _authService = authService;
        _logger = logger;
    }

    #region Server Stats

    /// <summary>
    /// Gets comprehensive server statistics.
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<ServerStatsDto> GetServerStats(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var stats = _adminService.GetServerStats(user.Id);
        return Ok(stats);
    }

    #endregion

    #region Online Users

    /// <summary>
    /// Gets all currently online users.
    /// </summary>
    [HttpGet("online-users")]
    public ActionResult<List<OnlineUserInfo>> GetOnlineUsers(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var users = _adminService.GetOnlineUsers();
        return Ok(users);
    }

    /// <summary>
    /// Gets online user count.
    /// </summary>
    [HttpGet("online-count")]
    public ActionResult<int> GetOnlineUserCount(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _adminService.GetOnlineUserCount();
        return Ok(count);
    }

    #endregion

    #region Broadcast

    /// <summary>
    /// Sends a system-wide broadcast message.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<ActionResult> SendBroadcast(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] BroadcastRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var success = await _adminService.SendBroadcastAsync(user.Id, request.Message, request.Channel);
        if (success)
            return Ok(new { Success = true });

        return BadRequest("Failed to send broadcast");
    }

    #endregion

    #region Role Management

    /// <summary>
    /// Promotes a user to a higher role.
    /// </summary>
    [HttpPost("promote")]
    public ActionResult PromoteUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] RoleChangeRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var success = _adminService.PromoteUser(user.Id, request.UserId, request.NewRole);
        if (success)
            return Ok(new { Success = true });

        return BadRequest("Failed to promote user. Check permissions and target user role.");
    }

    /// <summary>
    /// Demotes a user to a lower role.
    /// </summary>
    [HttpPost("demote")]
    public ActionResult DemoteUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] RoleChangeRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var success = _adminService.DemoteUser(user.Id, request.UserId, request.NewRole);
        if (success)
            return Ok(new { Success = true });

        return BadRequest("Failed to demote user. Check permissions and target user role.");
    }

    #endregion

    #region Kick User

    /// <summary>
    /// Kicks a user from the server.
    /// </summary>
    [HttpPost("kick")]
    public async Task<ActionResult> KickUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] KickUserRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var success = await _adminService.KickUserAsync(user.Id, request.UserId, request.Reason);
        if (success)
            return Ok(new { Success = true });

        return BadRequest("Failed to kick user. Check permissions and target user.");
    }

    #endregion

    #region Superusers Management

    /// <summary>
    /// Gets all configured superusers.
    /// </summary>
    [HttpGet("superusers")]
    public ActionResult<List<SuperuserEntry>> GetSuperusers(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var superusers = _adminService.GetSuperusers(user.Id);
        return Ok(superusers);
    }

    /// <summary>
    /// Adds a new superuser entry.
    /// </summary>
    [HttpPost("superusers")]
    public ActionResult AddSuperuser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] SuperuserEntry entry)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var success = _adminService.AddSuperuser(user.Id, entry);
        if (success)
            return Ok(new { Success = true });

        return BadRequest("Failed to add superuser. User may already exist.");
    }

    /// <summary>
    /// Removes a superuser entry.
    /// </summary>
    [HttpDelete("superusers/{username}")]
    public ActionResult RemoveSuperuser(
        string username,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Admin)
            return Forbid();

        var success = _adminService.RemoveSuperuser(user.Id, username);
        if (success)
            return Ok(new { Success = true });

        return NotFound("Superuser not found");
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

#region Request Models

public class BroadcastRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Channel { get; set; }
}

public class RoleChangeRequest
{
    public string UserId { get; set; } = string.Empty;
    public UserRole NewRole { get; set; }
}

public class KickUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

#endregion
