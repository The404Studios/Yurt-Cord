using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KeyController : ControllerBase
{
    private readonly KeyGeneratorService _keyService;
    private readonly AuthService _authService;
    private readonly ILogger<KeyController> _logger;

    public KeyController(
        KeyGeneratorService keyService,
        AuthService authService,
        ILogger<KeyController> logger)
    {
        _keyService = keyService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Validates an activation key without consuming it
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidateKey([FromBody] KeyValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest(new { success = false, message = "Key is required" });
        }

        var isValid = _keyService.IsValidKey(request.Key);

        return Ok(new
        {
            success = isValid,
            message = isValid ? "Key is valid" : "Invalid or already used key"
        });
    }

    /// <summary>
    /// Gets key statistics (admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize]
    public IActionResult GetStats()
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        var (total, unused, used, lastGenerated) = _keyService.GetStats();

        return Ok(new
        {
            total,
            unused,
            used,
            lastGenerated,
            message = $"{unused} keys available"
        });
    }

    /// <summary>
    /// Gets available keys (admin only)
    /// </summary>
    [HttpGet("available")]
    [Authorize]
    public IActionResult GetAvailableKeys([FromQuery] int count = 10)
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        var keys = _keyService.GetAvailableKeys(count);

        return Ok(new
        {
            keys,
            count = keys.Count
        });
    }

    /// <summary>
    /// Generates new keys (admin only)
    /// </summary>
    [HttpPost("generate")]
    [Authorize]
    public IActionResult GenerateKeys([FromBody] KeyGenerateRequest request)
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        if (request.Count <= 0 || request.Count > 100)
        {
            return BadRequest(new { success = false, message = "Count must be between 1 and 100" });
        }

        var keys = _keyService.GenerateKeys(request.Count);

        _logger.LogInformation("Admin {Username} generated {Count} new keys", user.Username, request.Count);

        return Ok(new
        {
            success = true,
            keys,
            message = $"Generated {keys.Count} new keys"
        });
    }

    /// <summary>
    /// Revokes a key (admin only)
    /// </summary>
    [HttpPost("revoke")]
    [Authorize]
    public IActionResult RevokeKey([FromBody] KeyValidateRequest request)
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest(new { success = false, message = "Key is required" });
        }

        var success = _keyService.RevokeKey(request.Key);

        if (success)
        {
            _logger.LogInformation("Admin {Username} revoked key {Key}", user.Username, request.Key);
        }

        return Ok(new
        {
            success,
            message = success ? "Key revoked successfully" : "Key not found or already used"
        });
    }
}

public class KeyValidateRequest
{
    public string Key { get; set; } = string.Empty;
}

public class KeyGenerateRequest
{
    public int Count { get; set; } = 10;
}
