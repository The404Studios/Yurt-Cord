using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly ActivityService _activityService;
    private readonly AuthService _authService;

    public ActivityController(ActivityService activityService, AuthService authService)
    {
        _activityService = activityService;
        _authService = authService;
    }

    [HttpGet]
    public ActionResult<List<UserActivityDto>> GetActivityFeed(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] ActivityType? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var activities = _activityService.GetActivityFeed(user.Id, type, page, pageSize);
        return Ok(activities);
    }

    [HttpGet("global")]
    public ActionResult<List<UserActivityDto>> GetGlobalFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var activities = _activityService.GetGlobalFeed(page, pageSize);
        return Ok(activities);
    }

    [HttpGet("friends")]
    public ActionResult<List<UserActivityDto>> GetFriendsFeed(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var activities = _activityService.GetFriendsFeed(user.Id, page, pageSize);
        return Ok(activities);
    }

    [HttpGet("user/{userId}")]
    public ActionResult<List<UserActivityDto>> GetUserActivity(
        string userId,
        [FromQuery] ActivityType? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var activities = _activityService.GetActivityFeed(userId, type, page, pageSize);
        // Filter to only public activities
        var publicActivities = activities.Where(a => a.IsPublic).ToList();
        return Ok(publicActivities);
    }

    [HttpPost("status")]
    public ActionResult<UserActivityDto> PostStatusUpdate(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] StatusUpdateRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Status))
            return BadRequest("Status cannot be empty");

        _activityService.LogStatusUpdate(user.Id, request.Status);
        return Ok(new { Success = true });
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

public class StatusUpdateRequest
{
    public string Status { get; set; } = string.Empty;
}
