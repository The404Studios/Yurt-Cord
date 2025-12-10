namespace VeaMarketplace.Shared.Models;

public class Friendship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequesterId { get; set; } = string.Empty;
    public string AddresseeId { get; set; } = string.Empty;
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
}

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Declined,
    Blocked
}
