using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class AuthService
{
    private readonly DatabaseService _db;
    private readonly IConfiguration _configuration;

    public AuthService(DatabaseService db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public AuthResponse Register(RegisterRequest request)
    {
        if (_db.Users.Exists(u => u.Username.ToLower() == request.Username.ToLower()))
        {
            return new AuthResponse { Success = false, Message = "Username already exists" };
        }

        if (_db.Users.Exists(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return new AuthResponse { Success = false, Message = "Email already exists" };
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AvatarUrl = $"https://api.dicebear.com/7.x/avataaars/svg?seed={request.Username}",
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _db.Users.Insert(user);

        var token = GenerateToken(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            Token = token,
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

        user.LastSeenAt = DateTime.UtcNow;
        user.IsOnline = true;
        _db.Users.Update(user);

        var token = GenerateToken(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
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
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? "YourSuperSecretKeyHere12345678901234567890");

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == "id").Value;

            return GetUserById(userId);
        }
        catch
        {
            return null;
        }
    }

    private string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? "YourSuperSecretKeyHere12345678901234567890");

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
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
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
