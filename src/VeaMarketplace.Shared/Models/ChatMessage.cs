using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public UserRole SenderRole { get; set; }
    public UserRank SenderRank { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Text;
    public string Channel { get; set; } = "general";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public List<string> Mentions { get; set; } = new();
}
