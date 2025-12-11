namespace VeaMarketplace.Shared.Models;

public enum AttachmentType
{
    Image = 0,
    Video = 1,
    Audio = 2,
    Document = 3,
    File = 4,
    Link = 5
}

public class MessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public AttachmentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public long FileSize { get; set; } // in bytes
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; } // for video/audio
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
