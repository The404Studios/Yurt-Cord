using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public UserRank Rank { get; set; } = UserRank.Newcomer;
    public int Reputation { get; set; } = 0;
    public int TotalSales { get; set; } = 0;
    public int TotalPurchases { get; set; } = 0;
    public decimal Balance { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = false;
    public bool IsBanned { get; set; } = false;
    public string? BanReason { get; set; }
    public List<string> Badges { get; set; } = new();
    public List<string> CustomRoleIds { get; set; } = new();
}
