namespace VeaMarketplace.Shared.Models;

public class MessageReaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MessageReactionSummary
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Usernames { get; set; } = new();
    public bool HasCurrentUser { get; set; }
}
