namespace VeaMarketplace.Shared.Models;

public class FriendNickname
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty; // Who set the nickname
    public string FriendUserId { get; set; } = string.Empty; // Friend being nicknamed
    public string Nickname { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FriendCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int Order { get; set; } = 0;
    public List<string> FriendUserIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FavoriteFriend
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string FriendUserId { get; set; } = string.Empty;
    public int Order { get; set; } = 0;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
