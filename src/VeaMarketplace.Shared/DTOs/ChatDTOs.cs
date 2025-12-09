using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Channel { get; set; } = "general";
}

public class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    public UserRole SenderRole { get; set; }
    public UserRank SenderRank { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string Channel { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsEdited { get; set; }
}

public class ChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int OnlineCount { get; set; }
    public UserRole MinimumRole { get; set; }
}
