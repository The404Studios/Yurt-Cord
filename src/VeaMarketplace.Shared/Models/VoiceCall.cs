namespace VeaMarketplace.Shared.Models;

public class VoiceCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CallerId { get; set; } = string.Empty;
    public string CallerUsername { get; set; } = string.Empty;
    public string CallerAvatarUrl { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string RecipientAvatarUrl { get; set; } = string.Empty;
    public VoiceCallStatus Status { get; set; } = VoiceCallStatus.Ringing;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan Duration => EndedAt.HasValue && AnsweredAt.HasValue
        ? EndedAt.Value - AnsweredAt.Value
        : TimeSpan.Zero;
}

public enum VoiceCallStatus
{
    Ringing,
    InProgress,
    Ended,
    Missed,
    Declined,
    Failed
}
