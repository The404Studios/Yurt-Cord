namespace VeaMarketplace.Shared.Models;

public enum AutoModRuleType
{
    BannedWords = 0,
    SpamDetection = 1,
    LinkFilter = 2,
    MentionSpam = 3,
    CapitalLetters = 4,
    Emojis = 5,
    Custom = 6
}

public enum AutoModAction
{
    Delete = 0,
    Flag = 1,
    Mute = 2,
    Warn = 3,
    Kick = 4
}

public class AutoModRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public AutoModRuleType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AutoModAction Action { get; set; }
    public List<string> BannedWords { get; set; } = new();
    public List<string> AllowedDomains { get; set; } = new();
    public int? MaxMentions { get; set; }
    public int? MaxEmojis { get; set; }
    public int? MaxCapitalPercent { get; set; }
    public string? CustomRegex { get; set; }
    public List<string> ExemptRoles { get; set; } = new();
    public List<string> ExemptUsers { get; set; } = new();
    public int? MuteDuration { get; set; } // in minutes
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
