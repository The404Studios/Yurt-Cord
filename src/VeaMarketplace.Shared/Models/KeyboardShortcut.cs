namespace VeaMarketplace.Shared.Models;

public enum ShortcutAction
{
    ToggleMute = 0,
    ToggleDeafen = 1,
    PushToTalk = 2,
    QuickSwitchChannel = 3,
    OpenSettings = 4,
    OpenProfile = 5,
    OpenMarketplace = 6,
    OpenFriends = 7,
    SearchUsers = 8,
    ScreenShare = 9,
    NextChannel = 10,
    PreviousChannel = 11,
    MarkAsRead = 12,
    JumpToUnread = 13,
    SendMessage = 14,
    EditLastMessage = 15,
    DeleteMessage = 16,
    CopyMessage = 17,
    QuickReply = 18,
    Emoji = 19
}

public class KeyboardShortcut
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ShortcutAction Action { get; set; }
    public string Keys { get; set; } = string.Empty; // e.g., "Ctrl+Shift+M"
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
