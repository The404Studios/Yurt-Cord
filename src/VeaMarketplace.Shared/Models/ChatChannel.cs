using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class ChatChannel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "💬";
    public UserRole MinimumRole { get; set; } = UserRole.Guest;
    public bool IsDefault { get; set; } = false;
    public int OnlineCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
