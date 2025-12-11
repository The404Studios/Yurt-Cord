namespace VeaMarketplace.Shared.Models;

public class MessageEdit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string OldContent { get; set; } = string.Empty;
    public string NewContent { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
