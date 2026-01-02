using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Gets the current authentication mode the server is running in.
    /// </summary>
    [HttpGet("mode")]
    [EnableRateLimiting("api")]
    public ActionResult<AuthModeResponse> GetAuthenticationMode()
    {
        return Ok(new AuthModeResponse
        {
            Mode = _authService.AuthenticationMode,
            ModeName = _authService.AuthenticationMode.ToString(),
            Description = _authService.AuthenticationMode switch
            {
                AuthenticationMode.Session => "Standard session-based authentication. Users can login from any device.",
                AuthenticationMode.Whitelist => "Whitelist-based authentication. Only pre-approved users can login.",
                _ => "Unknown authentication mode."
            }
        });
    }

    [HttpPost("register")]
    public ActionResult<AuthResponse> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return BadRequest(new AuthResponse { Success = false, Message = "Username must be at least 3 characters", AuthMode = _authService.AuthenticationMode });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new AuthResponse { Success = false, Message = "Password must be at least 6 characters", AuthMode = _authService.AuthenticationMode });

        if (!request.Email.Contains('@'))
            return BadRequest(new AuthResponse { Success = false, Message = "Invalid email address", AuthMode = _authService.AuthenticationMode });

        var response = _authService.Register(request);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("login")]
    public ActionResult<AuthResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Username and password are required", AuthMode = _authService.AuthenticationMode });

        var response = _authService.Login(request);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpGet("validate")]
    [EnableRateLimiting("api")]
    public ActionResult<UserDto> ValidateToken([FromHeader(Name = "Authorization")] string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return Unauthorized();

        var token = authorization["Bearer ".Length..];
        var user = _authService.ValidateToken(token);

        if (user == null)
            return Unauthorized();

        return Ok(_authService.MapToDto(user));
    }
}

/// <summary>
/// Response containing the current authentication mode.
/// </summary>
public class AuthModeResponse
{
    public AuthenticationMode Mode { get; set; }
    public string ModeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
