namespace VeaMarketplace.Shared.Models;

public enum ReportReason
{
    Spam = 0,
    Harassment = 1,
    HateSpeech = 2,
    Violence = 3,
    NSFW = 4,
    Scam = 5,
    Other = 6
}

public enum ReportStatus
{
    Pending = 0,
    UnderReview = 1,
    Resolved = 2,
    Dismissed = 3
}

public class MessageReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterUsername { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public string ReportedUsername { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
    public string? AdditionalInfo { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ModeratorNotes { get; set; }
}
