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
