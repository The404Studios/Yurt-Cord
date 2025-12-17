using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;
using ActivityType = VeaMarketplace.Shared.Models.ActivityType;

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

    // Following endpoints

    [HttpGet("following")]
    public ActionResult<List<UserActivityDto>> GetFollowingFeed(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var activities = _activityService.GetFollowingFeed(user.Id, page, pageSize);
        return Ok(activities);
    }

    [HttpPost("follow/{userId}")]
    public ActionResult FollowUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        string userId)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _activityService.FollowUser(user.Id, userId);
        if (!result)
            return BadRequest("Cannot follow this user");

        return Ok(new { Success = true, Message = "Now following user" });
    }

    [HttpDelete("follow/{userId}")]
    public ActionResult UnfollowUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        string userId)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var result = _activityService.UnfollowUser(user.Id, userId);
        return Ok(new { Success = true, Message = result ? "Unfollowed user" : "Was not following" });
    }

    [HttpGet("follow/{userId}/status")]
    public ActionResult GetFollowStatus(
        [FromHeader(Name = "Authorization")] string? authorization,
        string userId)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var isFollowing = _activityService.IsFollowing(user.Id, userId);
        var followerCount = _activityService.GetFollowerCount(userId);
        var followingCount = _activityService.GetFollowingCount(userId);

        return Ok(new
        {
            IsFollowing = isFollowing,
            FollowerCount = followerCount,
            FollowingCount = followingCount
        });
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
