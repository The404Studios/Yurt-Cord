using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public int Reputation { get; set; }
    public int TotalSales { get; set; }
    public int TotalPurchases { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public List<string> Badges { get; set; } = new();
    public List<CustomRoleDto> CustomRoles { get; set; } = new();
}

public class UpdateProfileRequest
{
    public string? Username { get; set; }
    public string? Bio { get; set; }
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
}

public class CustomRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public int Position { get; set; }
    public bool IsHoisted { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class OnlineUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
}
