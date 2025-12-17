using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly AuthService _authService;
    private readonly IHubContext<ContentHub> _contentHub;

    public NotificationsController(
        NotificationService notificationService,
        AuthService authService,
        IHubContext<ContentHub> contentHub)
    {
        _notificationService = notificationService;
        _authService = authService;
        _contentHub = contentHub;
    }

    [HttpGet]
    public ActionResult<List<NotificationDto>> GetNotifications(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var notifications = _notificationService.GetNotifications(user.Id, unreadOnly, page, pageSize);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public ActionResult<int> GetUnreadCount(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _notificationService.GetUnreadCount(user.Id);
        return Ok(new { Count = count });
    }

    [HttpPost("{notificationId}/read")]
    public ActionResult MarkAsRead(
        string notificationId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_notificationService.MarkAsRead(notificationId, user.Id))
            return Ok(new { Success = true });

        return NotFound();
    }

    [HttpPost("read-all")]
    public ActionResult MarkAllAsRead(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _notificationService.MarkAllAsRead(user.Id);
        return Ok(new { Success = true, MarkedCount = count });
    }

    [HttpDelete("{notificationId}")]
    public ActionResult DeleteNotification(
        string notificationId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_notificationService.DeleteNotification(notificationId, user.Id))
            return Ok(new { Success = true });

        return NotFound();
    }

    [HttpDelete]
    public ActionResult ClearAllNotifications(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        var count = _notificationService.ClearAllNotifications(user.Id);
        return Ok(new { Success = true, DeletedCount = count });
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}
