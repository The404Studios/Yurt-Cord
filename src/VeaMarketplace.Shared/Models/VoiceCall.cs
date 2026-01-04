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

    /// <summary>
    /// Calculates call duration. For ended calls, returns EndedAt - AnsweredAt.
    /// For in-progress calls, returns current time - AnsweredAt.
    /// For calls not yet answered, returns TimeSpan.Zero.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            if (!AnsweredAt.HasValue)
                return TimeSpan.Zero;

            var endTime = EndedAt ?? DateTime.UtcNow;
            return endTime - AnsweredAt.Value;
        }
    }
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
