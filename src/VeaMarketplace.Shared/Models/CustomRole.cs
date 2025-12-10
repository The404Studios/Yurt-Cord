namespace VeaMarketplace.Shared.Models;

public class CustomRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public int Position { get; set; } = 0;
    public bool IsHoisted { get; set; } = false;
    public bool IsMentionable { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class RolePermissions
{
    public const string ManageRoles = "manage_roles";
    public const string ManageChannels = "manage_channels";
    public const string KickMembers = "kick_members";
    public const string BanMembers = "ban_members";
    public const string ManageMessages = "manage_messages";
    public const string MuteMembers = "mute_members";
    public const string DeafenMembers = "deafen_members";
    public const string MoveMembers = "move_members";
    public const string Administrator = "administrator";
    public const string ViewChannels = "view_channels";
    public const string SendMessages = "send_messages";
    public const string EmbedLinks = "embed_links";
    public const string AttachFiles = "attach_files";
    public const string MentionEveryone = "mention_everyone";
    public const string UseVoice = "use_voice";
    public const string Speak = "speak";
    public const string Video = "video";
    public const string PrioritySpeaker = "priority_speaker";
}
