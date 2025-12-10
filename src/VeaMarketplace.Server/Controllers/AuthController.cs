using Microsoft.AspNetCore.Mvc;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public ActionResult<AuthResponse> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return BadRequest(new AuthResponse { Success = false, Message = "Username must be at least 3 characters" });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new AuthResponse { Success = false, Message = "Password must be at least 6 characters" });

        if (!request.Email.Contains('@'))
            return BadRequest(new AuthResponse { Success = false, Message = "Invalid email address" });

        var response = _authService.Register(request);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("login")]
    public ActionResult<AuthResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Username and password are required" });

        var response = _authService.Login(request);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpGet("validate")]
    public ActionResult<UserDto> ValidateToken([FromHeader(Name = "Authorization")] string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return Unauthorized();

        var token = authorization["Bearer ".Length..];
        var user = _authService.ValidateToken(token);

        if (user == null)
            return Unauthorized();

        return Ok(AuthService.MapToDto(user));
    }
}
