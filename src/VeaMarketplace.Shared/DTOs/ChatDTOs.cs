using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Channel { get; set; } = "general";
    public List<string>? AttachmentIds { get; set; }
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
    public List<MessageAttachmentDto> Attachments { get; set; } = new();
    public List<MessageEmbedDto> Embeds { get; set; } = new();
    public List<MessageReactionDto> Reactions { get; set; } = new();
}

/// <summary>
/// DTO for embedded content in messages (shared products, auctions, profiles)
/// </summary>
public class MessageEmbedDto
{
    public string Id { get; set; } = string.Empty;
    public EmbedContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? CurrentBid { get; set; }
    public decimal? MinBidIncrement { get; set; }
    public DateTime? AuctionEndsAt { get; set; }
    public string? SellerId { get; set; }
    public string? SellerUsername { get; set; }
    public string? SellerAvatarUrl { get; set; }
    public string? SellerRole { get; set; }
    public string? ContentId { get; set; }
    public string ShareLink => $"vea://marketplace/{Type.ToString().ToLower()}/{ContentId}";
}

/// <summary>
/// DTO for message reactions
/// </summary>
public class MessageReactionDto
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Grouped reaction display
/// </summary>
public class ReactionGroupDto
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool HasCurrentUserReacted { get; set; }
    public List<string> UserNames { get; set; } = new();
}

/// <summary>
/// Types of embedded content
/// </summary>
public enum EmbedContentType
{
    Product,
    Auction,
    Profile,
    Listing,
    Link,
    Image
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

/// <summary>
/// DTO for message attachments (files, images, videos, etc.)
/// </summary>
public class MessageAttachmentDto
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public AttachmentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Response from file upload
/// </summary>
public class FileUploadResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? FileId { get; set; }
    public string? FileUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public AttachmentType FileType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>
/// Request to upload a profile image (avatar or banner)
/// </summary>
public class ProfileImageUploadResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
