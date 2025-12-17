using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModerationController : ControllerBase
{
    private readonly ModerationService _moderationService;
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;
    private readonly IHubContext<ChatHub> _chatHub;

    public ModerationController(
        ModerationService moderationService,
        AuthService authService,
        NotificationService notificationService,
        IHubContext<ChatHub> chatHub)
    {
        _moderationService = moderationService;
        _authService = authService;
        _notificationService = notificationService;
        _chatHub = chatHub;
    }

    [HttpGet("dashboard")]
    public ActionResult<ModerationDashboardDto> GetDashboard(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var dashboard = _moderationService.GetDashboard();
        return Ok(dashboard);
    }

    [HttpGet("bans")]
    public ActionResult<List<UserBanDto>> GetBannedUsers(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var bans = _moderationService.GetBannedUsers();
        return Ok(bans);
    }

    [HttpGet("reports")]
    public ActionResult<List<MessageReportDto>> GetPendingReports(
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var reports = _moderationService.GetPendingReports();
        return Ok(reports);
    }

    [HttpPost("ban")]
    public async Task<ActionResult<UserBanDto>> BanUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] BanUserRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var ban = _moderationService.BanUser(user.Id, request);
        if (ban == null)
            return BadRequest("Unable to ban user. Check permissions and target user.");

        // Notify the banned user
        _notificationService.NotifyModerationAction(request.UserId, "banned", request.Reason);

        // Disconnect the user
        await _chatHub.Clients.User(request.UserId).SendAsync("Banned", new
        {
            Reason = request.Reason,
            ExpiresAt = request.ExpiresAt
        });

        return Ok(ban);
    }

    [HttpPost("unban/{userId}")]
    public ActionResult UnbanUser(
        string userId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.UnbanUser(user.Id, userId))
        {
            _notificationService.NotifyModerationAction(userId, "unbanned", null);
            return Ok(new { Success = true });
        }

        return NotFound();
    }

    [HttpPost("mute")]
    public async Task<ActionResult<UserMuteDto>> MuteUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] MuteUserRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var mute = _moderationService.MuteUser(user.Id, request);
        if (mute == null)
            return BadRequest("Unable to mute user");

        _notificationService.NotifyModerationAction(request.UserId, "muted", request.Reason);

        await _chatHub.Clients.User(request.UserId).SendAsync("Muted", new
        {
            Reason = request.Reason,
            ExpiresAt = request.ExpiresAt,
            Channels = request.MutedChannels
        });

        return Ok(mute);
    }

    [HttpPost("unmute/{userId}")]
    public ActionResult UnmuteUser(
        string userId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.UnmuteUser(user.Id, userId))
        {
            _notificationService.NotifyModerationAction(userId, "unmuted", null);
            return Ok(new { Success = true });
        }

        return NotFound();
    }

    [HttpPost("warn")]
    public ActionResult WarnUser(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] WarnUserRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.WarnUser(user.Id, request))
        {
            _notificationService.NotifyModerationAction(request.UserId, "warned", request.Reason);
            return Ok(new { Success = true });
        }

        return BadRequest("Unable to warn user");
    }

    [HttpGet("warnings/{userId}")]
    public ActionResult<List<Shared.Models.UserWarning>> GetUserWarnings(
        string userId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        var warnings = _moderationService.GetUserWarnings(userId);
        return Ok(warnings);
    }

    [HttpDelete("message/{messageId}")]
    public async Task<ActionResult> DeleteMessage(
        string messageId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] string? reason)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.DeleteMessage(user.Id, messageId, reason ?? "Removed by moderator"))
        {
            await _chatHub.Clients.All.SendAsync("MessageDeleted", messageId);
            return Ok(new { Success = true });
        }

        return NotFound();
    }

    [HttpPost("reports/{reportId}/resolve")]
    public ActionResult ResolveReport(
        string reportId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] ResolveReportRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.ResolveReport(user.Id, reportId, request.Resolution))
            return Ok(new { Success = true });

        return NotFound();
    }

    [HttpPost("reports/{reportId}/dismiss")]
    public ActionResult DismissReport(
        string reportId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (user.Role < UserRole.Moderator)
            return Forbid();

        if (_moderationService.DismissReport(user.Id, reportId))
            return Ok(new { Success = true });

        return NotFound();
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

public class ResolveReportRequest
{
    public string Resolution { get; set; } = string.Empty;
}
