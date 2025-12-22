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

    #region Whitelist Codes (6-digit)

    /// <summary>
    /// Generates 6-digit whitelist codes (admin only)
    /// </summary>
    [HttpPost("whitelist/generate")]
    [Authorize]
    public IActionResult GenerateWhitelistCodes([FromBody] WhitelistCodeGenerateRequest request)
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

        var codes = _keyService.GenerateWhitelistCodes(request.Count, request.ExpirationHours, request.Note);

        _logger.LogInformation("Admin {Username} generated {Count} whitelist codes (expires in {Hours}h)",
            user.Username, request.Count, request.ExpirationHours);

        return Ok(new
        {
            success = true,
            codes,
            expirationHours = request.ExpirationHours,
            message = $"Generated {codes.Count} whitelist codes (valid for {request.ExpirationHours} hours)"
        });
    }

    /// <summary>
    /// Gets available whitelist codes (admin only)
    /// </summary>
    [HttpGet("whitelist/available")]
    [Authorize]
    public IActionResult GetAvailableWhitelistCodes([FromQuery] int count = 10)
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        var codes = _keyService.GetAvailableWhitelistCodes(count);

        return Ok(new
        {
            codes = codes.Select(c => new
            {
                c.Code,
                c.GeneratedAt,
                c.ExpiresAt,
                c.Note,
                remainingMinutes = (int)(c.ExpiresAt - DateTime.UtcNow).TotalMinutes
            }),
            count = codes.Count
        });
    }

    /// <summary>
    /// Gets whitelist code statistics (admin only)
    /// </summary>
    [HttpGet("whitelist/stats")]
    [Authorize]
    public IActionResult GetWhitelistCodeStats()
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        var (total, available, used, expired) = _keyService.GetWhitelistCodeStats();

        return Ok(new
        {
            total,
            available,
            used,
            expired,
            message = $"{available} codes available"
        });
    }

    /// <summary>
    /// Validates a whitelist code without consuming it
    /// </summary>
    [HttpPost("whitelist/validate")]
    public IActionResult ValidateWhitelistCode([FromBody] WhitelistCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { success = false, message = "Code is required" });
        }

        var isValid = _keyService.IsValidWhitelistCode(request.Code);

        return Ok(new
        {
            success = isValid,
            message = isValid ? "Code is valid" : "Invalid, expired, or already used code"
        });
    }

    /// <summary>
    /// Revokes a whitelist code (admin only)
    /// </summary>
    [HttpPost("whitelist/revoke")]
    [Authorize]
    public IActionResult RevokeWhitelistCode([FromBody] WhitelistCodeRequest request)
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { success = false, message = "Code is required" });
        }

        var success = _keyService.RevokeWhitelistCode(request.Code);

        if (success)
        {
            _logger.LogInformation("Admin {Username} revoked whitelist code {Code}", user.Username, request.Code);
        }

        return Ok(new
        {
            success,
            message = success ? "Whitelist code revoked successfully" : "Code not found or already used"
        });
    }

    /// <summary>
    /// Cleans up expired whitelist codes (admin only)
    /// </summary>
    [HttpPost("whitelist/cleanup")]
    [Authorize]
    public IActionResult CleanupExpiredCodes()
    {
        // Verify admin role
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        var user = _authService.ValidateToken(token);

        if (user == null || (user.Role != UserRole.Admin && user.Role != UserRole.Owner))
        {
            return Forbid();
        }

        var removedCount = _keyService.CleanupExpiredCodes();

        _logger.LogInformation("Admin {Username} cleaned up {Count} expired whitelist codes", user.Username, removedCount);

        return Ok(new
        {
            success = true,
            removedCount,
            message = removedCount > 0 ? $"Removed {removedCount} expired codes" : "No expired codes to clean up"
        });
    }

    #endregion
}

public class KeyValidateRequest
{
    public string Key { get; set; } = string.Empty;
}

public class KeyGenerateRequest
{
    public int Count { get; set; } = 10;
}

public class WhitelistCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class WhitelistCodeGenerateRequest
{
    public int Count { get; set; } = 10;
    public int ExpirationHours { get; set; } = 24;
    public string? Note { get; set; }
}
