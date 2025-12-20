using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class AuthService
{
    private readonly DatabaseService _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly KeyGeneratorService? _keyService;
    private readonly byte[] _jwtKey;

    // Minimum password requirements
    private const int MinPasswordLength = 8;

    // Configuration flag for requiring activation keys
    private readonly bool _requireActivationKey;

    // Configuration flag for requiring HWID binding
    private readonly bool _requireHwidBinding;

    public AuthService(DatabaseService db, IConfiguration configuration, ILogger<AuthService> logger, KeyGeneratorService? keyService = null)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _keyService = keyService;

        // Check if activation keys are required (default: true if key service is available)
        _requireActivationKey = configuration.GetValue<bool>("Registration:RequireActivationKey", true);

        // Check if HWID binding is required (default: true)
        _requireHwidBinding = configuration.GetValue<bool>("Registration:RequireHwidBinding", true);

        // Get JWT secret from environment or configuration - require it to be set
        var jwtSecret = Environment.GetEnvironmentVariable("VEA_JWT_SECRET")
            ?? configuration["Jwt:Secret"];

        if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
        {
            _logger.LogWarning("JWT secret not configured or too short. Generating secure random key for this session.");
            // Generate a secure random key for this session (not recommended for production)
            jwtSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        }

        _jwtKey = Encoding.UTF8.GetBytes(jwtSecret);
    }

    public AuthResponse Register(RegisterRequest request)
    {
        // Validate password strength
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return new AuthResponse { Success = false, Message = $"Password must be at least {MinPasswordLength} characters long" };
        }

        if (_db.Users.Exists(u => u.Username.ToLower() == request.Username.ToLower()))
        {
            return new AuthResponse { Success = false, Message = "Username already exists" };
        }

        if (_db.Users.Exists(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return new AuthResponse { Success = false, Message = "Email already exists" };
        }

        // Validate activation key if required
        string? clientSalt = null;
        if (_requireActivationKey && _keyService != null)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationKey))
            {
                return new AuthResponse { Success = false, Message = "Activation key is required" };
            }

            // We'll validate and use the key after creating the user
            if (!_keyService.IsValidKey(request.ActivationKey))
            {
                return new AuthResponse { Success = false, Message = "Invalid or already used activation key" };
            }
        }

        // Validate HWID if required
        if (_requireHwidBinding)
        {
            if (string.IsNullOrWhiteSpace(request.Hwid))
            {
                return new AuthResponse { Success = false, Message = "Hardware ID is required for registration" };
            }

            // Check if this HWID is already bound to another account
            var existingUser = _db.Users.FindOne(u => u.Hwid == request.Hwid);
            if (existingUser != null)
            {
                _logger.LogWarning("HWID {Hwid} already bound to user {Username}",
                    request.Hwid[..8], existingUser.Username);
                return new AuthResponse
                {
                    Success = false,
                    Message = "This device is already registered to another account",
                    HwidMismatch = true,
                    BoundHwidPrefix = request.Hwid.Length >= 8 ? request.Hwid[..8] : request.Hwid
                };
            }
        }

        _logger.LogInformation("Registering new user: {Username}", request.Username);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AvatarUrl = $"https://api.dicebear.com/7.x/avataaars/svg?seed={request.Username}",
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Hwid = request.Hwid,
            HwidBoundAt = DateTime.UtcNow,
            ActivationKey = request.ActivationKey
        };

        _db.Users.Insert(user);

        // Use the activation key after successful registration
        if (_requireActivationKey && _keyService != null && !string.IsNullOrWhiteSpace(request.ActivationKey))
        {
            var (success, salt, message) = _keyService.UseKey(request.ActivationKey, user.Id, user.Username);
            if (success)
            {
                clientSalt = salt;
                user.ClientSalt = salt;
                _db.Users.Update(user);
                _logger.LogInformation("Activation key used for user {Username}", request.Username);
            }
        }

        _logger.LogInformation("User {Username} registered with HWID {Hwid}",
            request.Username, request.Hwid?[..Math.Min(8, request.Hwid?.Length ?? 0)] ?? "none");

        var token = GenerateToken(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            Token = token,
            ClientSalt = clientSalt,
            User = MapToDto(user)
        };
    }

    public AuthResponse Login(LoginRequest request)
    {
        var user = _db.Users.FindOne(u => u.Username.ToLower() == request.Username.ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthResponse { Success = false, Message = "Invalid username or password" };
        }

        if (user.IsBanned)
        {
            return new AuthResponse { Success = false, Message = $"Account banned: {user.BanReason ?? "No reason provided"}" };
        }

        // Validate HWID if the user has one bound
        if (_requireHwidBinding && !string.IsNullOrEmpty(user.Hwid))
        {
            if (string.IsNullOrEmpty(request.Hwid))
            {
                _logger.LogWarning("Login attempt for {Username} without HWID (HWID bound)", user.Username);
                return new AuthResponse
                {
                    Success = false,
                    Message = "Hardware ID is required for this account",
                    HwidMismatch = true
                };
            }

            if (user.Hwid != request.Hwid)
            {
                _logger.LogWarning("HWID mismatch for {Username}: expected {Expected}, got {Got}",
                    user.Username,
                    user.Hwid[..Math.Min(8, user.Hwid.Length)],
                    request.Hwid[..Math.Min(8, request.Hwid.Length)]);
                return new AuthResponse
                {
                    Success = false,
                    Message = "This account is bound to a different device",
                    HwidMismatch = true,
                    BoundHwidPrefix = user.Hwid.Length >= 8 ? user.Hwid[..8] : user.Hwid
                };
            }
        }

        user.LastSeenAt = DateTime.UtcNow;
        user.IsOnline = true;
        _db.Users.Update(user);

        var token = GenerateToken(user);

        _logger.LogInformation("User {Username} logged in from HWID {Hwid}",
            user.Username, request.Hwid?[..Math.Min(8, request.Hwid?.Length ?? 0)] ?? "none");

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            ClientSalt = user.ClientSalt,
            User = MapToDto(user)
        };
    }

    public User? GetUserById(string userId)
    {
        return _db.Users.FindById(userId);
    }

    public UserDto? UpdateProfile(string userId, UpdateProfileRequest request)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return null;

        if (request.Username != null && request.Username != user.Username)
        {
            // Check if username is taken
            if (_db.Users.Exists(u => u.Username.ToLower() == request.Username.ToLower() && u.Id != userId))
                return null;
            user.Username = request.Username;
        }

        // Basic profile fields
        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.Bio != null) user.Bio = request.Bio;
        if (request.Description != null) user.Description = request.Description;
        if (request.StatusMessage != null) user.StatusMessage = request.StatusMessage;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.BannerUrl != null) user.BannerUrl = request.BannerUrl;

        // Appearance
        if (request.AccentColor != null) user.AccentColor = request.AccentColor;

        // Privacy
        if (request.ProfileVisibility != null) user.ProfileVisibility = request.ProfileVisibility.Value;

        // Social links
        if (request.DiscordUsername != null) user.DiscordUsername = request.DiscordUsername;
        if (request.TwitterHandle != null) user.TwitterHandle = request.TwitterHandle;
        if (request.TelegramUsername != null) user.TelegramUsername = request.TelegramUsername;
        if (request.WebsiteUrl != null) user.WebsiteUrl = request.WebsiteUrl;

        _db.Users.Update(user);
        return MapToDto(user);
    }

    public User? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("Token validation failed: empty token");
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_jwtKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == "id").Value;

            return GetUserById(userId);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogDebug("Token validation failed: token expired at {Expiry}", ex.Expires);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug("Token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during token validation");
            return null;
        }
    }

    private string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("id", user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_jwtKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        _logger.LogDebug("Generated token for user {UserId}", user.Id);
        return tokenHandler.WriteToken(token);
    }

    public UserDto MapToDto(User user)
    {
        var customRoles = user.CustomRoleIds
            .Select(id => _db.CustomRoles.FindById(id))
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

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            BannerUrl = user.BannerUrl,
            Bio = user.Bio,
            Description = user.Description,
            StatusMessage = user.StatusMessage,
            AccentColor = user.AccentColor,
            ProfileVisibility = user.ProfileVisibility,
            Role = user.Role,
            Rank = user.Rank,
            Reputation = user.Reputation,
            TotalSales = user.TotalSales,
            TotalPurchases = user.TotalPurchases,
            Balance = user.Balance,
            CreatedAt = user.CreatedAt,
            LastSeenAt = user.LastSeenAt,
            IsOnline = user.IsOnline,
            Badges = user.Badges,
            CustomRoles = customRoles,
            DiscordUsername = user.DiscordUsername,
            TwitterHandle = user.TwitterHandle,
            TelegramUsername = user.TelegramUsername,
            WebsiteUrl = user.WebsiteUrl
        };
    }
}
