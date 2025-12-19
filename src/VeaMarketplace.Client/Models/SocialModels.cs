using System.Collections.ObjectModel;
using System.Windows.Media;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Models;

/// <summary>
/// Client-side user display model with additional UI properties
/// </summary>
public class UserDisplayModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? CustomStatus { get; set; }
    public string? StatusEmoji { get; set; }
    public string? BannerColor1 { get; set; }
    public string? BannerColor2 { get; set; }
    public UserStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserRoleDisplay>? Roles { get; set; }
}

/// <summary>
/// User role for display purposes
/// </summary>
public class UserRoleDisplay
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Brush Color { get; set; } = Brushes.Blue;
}

/// <summary>
/// Friend relationship status
/// </summary>
public enum FriendRelationship
{
    None,
    Friends,
    PendingOutgoing,
    PendingIncoming,
    Blocked
}

/// <summary>
/// User online status
/// </summary>
public enum UserStatus
{
    Offline,
    Online,
    Idle,
    DoNotDisturb,
    Invisible
}

#region Friend Groups

/// <summary>
/// A user-defined group for organizing friends
/// </summary>
public class FriendGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#FF6B00"; // Plugin orange
    public string? Emoji { get; set; }
    public int SortOrder { get; set; }
    public bool IsCollapsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> MemberIds { get; set; } = new();

    // UI helpers
    public SolidColorBrush ColorBrush => new(
        (Color)System.Windows.Media.ColorConverter.ConvertFromString(Color));
    public int MemberCount => MemberIds.Count;
    public string DisplayName => string.IsNullOrEmpty(Emoji) ? Name : $"{Emoji} {Name}";

    /// <summary>
    /// Populated member data for UI binding - set by the ViewModel when loading friends
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<VeaMarketplace.Shared.DTOs.FriendDto> Members { get; set; } = new();
}

/// <summary>
/// Default friend group types
/// </summary>
public enum DefaultFriendGroupType
{
    Favorites,
    Family,
    Coworkers,
    Gaming,
    School,
    Online,
    Custom
}

#endregion

#region Activity History

/// <summary>
/// Tracks interaction history with a friend
/// </summary>
public class FriendInteractionHistory
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public DateTime LastMessageSent { get; set; }
    public DateTime LastMessageReceived { get; set; }
    public DateTime LastVoiceCall { get; set; }
    public DateTime LastGameTogether { get; set; }
    public DateTime LastScreenShare { get; set; }
    public DateTime LastVoiceChannel { get; set; }
    public int TotalMessagesSent { get; set; }
    public int TotalMessagesReceived { get; set; }
    public int TotalVoiceMinutes { get; set; }
    public int TotalGameSessions { get; set; }
    public int TotalScreenShares { get; set; }
    public int TotalScreenShareMinutesWatched { get; set; }
    public List<InteractionEvent> RecentInteractions { get; set; } = new();
    public double InteractionScore => CalculateInteractionScore();

    private double CalculateInteractionScore()
    {
        var now = DateTime.UtcNow;
        double score = 0;

        // Recent activity weighted higher
        if ((now - LastMessageSent).TotalDays < 1) score += 50;
        else if ((now - LastMessageSent).TotalDays < 7) score += 30;
        else if ((now - LastMessageSent).TotalDays < 30) score += 10;

        if ((now - LastVoiceCall).TotalDays < 7) score += 40;
        if ((now - LastGameTogether).TotalDays < 7) score += 30;
        if ((now - LastScreenShare).TotalDays < 7) score += 25;
        if ((now - LastVoiceChannel).TotalDays < 7) score += 20;

        // Volume-based scoring
        score += Math.Min(TotalMessagesSent * 0.1, 20);
        score += Math.Min(TotalVoiceMinutes * 0.5, 30);
        score += Math.Min(TotalScreenShares * 2, 15);
        score += Math.Min(TotalScreenShareMinutesWatched * 0.3, 10);

        return score;
    }
}

/// <summary>
/// A single interaction event for history tracking
/// </summary>
public class InteractionEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FriendId { get; set; } = string.Empty;
    public InteractionType Type { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Description => Type switch
    {
        InteractionType.MessageSent => "Sent a message",
        InteractionType.MessageReceived => "Received a message",
        InteractionType.VoiceCallStarted => "Started a voice call",
        InteractionType.VoiceCallEnded => $"Voice call ended ({Details})",
        InteractionType.VoiceChannelJoined => $"Joined voice channel {Details}",
        InteractionType.VoiceChannelLeft => $"Left voice channel {Details}",
        InteractionType.GameSessionStarted => $"Started playing {Details}",
        InteractionType.GameSessionEnded => $"Finished playing {Details}",
        InteractionType.GiftSent => $"Sent a gift: {Details}",
        InteractionType.GiftReceived => $"Received a gift: {Details}",
        InteractionType.ReactionAdded => $"Reacted with {Details}",
        InteractionType.ProfileViewed => "Viewed their profile",
        InteractionType.ScreenShareStarted => "Started sharing their screen",
        InteractionType.ScreenShareEnded => "Stopped sharing their screen",
        InteractionType.ScreenShareWatched => $"Watched screen share for {Details}",
        _ => "Interacted"
    };

    public string Icon => Type switch
    {
        InteractionType.MessageSent => "💬",
        InteractionType.MessageReceived => "📨",
        InteractionType.VoiceCallStarted => "📞",
        InteractionType.VoiceCallEnded => "📴",
        InteractionType.VoiceChannelJoined => "🔊",
        InteractionType.VoiceChannelLeft => "🔇",
        InteractionType.GameSessionStarted => "🎮",
        InteractionType.GameSessionEnded => "🏁",
        InteractionType.GiftSent => "🎁",
        InteractionType.GiftReceived => "🎀",
        InteractionType.ReactionAdded => "👍",
        InteractionType.ProfileViewed => "👀",
        InteractionType.ScreenShareStarted => "🖥️",
        InteractionType.ScreenShareEnded => "📺",
        InteractionType.ScreenShareWatched => "👁️",
        _ => "📌"
    };
}

/// <summary>
/// Types of interactions for tracking
/// </summary>
public enum InteractionType
{
    MessageSent,
    MessageReceived,
    VoiceCallStarted,
    VoiceCallEnded,
    VoiceChannelJoined,
    VoiceChannelLeft,
    GameSessionStarted,
    GameSessionEnded,
    GiftSent,
    GiftReceived,
    ReactionAdded,
    ProfileViewed,
    ScreenShareStarted,
    ScreenShareEnded,
    ScreenShareWatched
}

#endregion

#region Rich Presence

/// <summary>
/// Rich presence information for a user
/// </summary>
public class RichPresence
{
    public string UserId { get; set; } = string.Empty;
    public RichPresenceType Type { get; set; }
    public string? ApplicationId { get; set; }
    public string? ApplicationName { get; set; }
    public string? ApplicationIcon { get; set; }
    public string? State { get; set; }
    public string? Details { get; set; }
    public string? LargeImageKey { get; set; }
    public string? LargeImageText { get; set; }
    public string? SmallImageKey { get; set; }
    public string? SmallImageText { get; set; }
    public DateTime StartTimestamp { get; set; }
    public DateTime? EndTimestamp { get; set; }
    public int? PartySize { get; set; }
    public int? PartyMax { get; set; }
    public string? JoinSecret { get; set; }
    public string? SpectateSecret { get; set; }
    public bool IsJoinable { get; set; }
    public bool IsSpectatable { get; set; }

    // Streaming specific
    public string? StreamUrl { get; set; }
    public string? StreamPlatform { get; set; }

    // Spotify-like listening info
    public string? TrackTitle { get; set; }
    public string? ArtistName { get; set; }
    public string? AlbumName { get; set; }
    public string? AlbumArtUrl { get; set; }
    public TimeSpan? TrackDuration { get; set; }
    public TimeSpan? TrackPosition { get; set; }

    // Computed properties
    public string ElapsedTime
    {
        get
        {
            var elapsed = DateTime.UtcNow - StartTimestamp;
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            return $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }
    }

    public string? RemainingTime
    {
        get
        {
            if (!EndTimestamp.HasValue) return null;
            var remaining = EndTimestamp.Value - DateTime.UtcNow;
            if (remaining.TotalSeconds < 0) return "00:00";
            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            return $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }

    public string PartyInfo => PartySize.HasValue && PartyMax.HasValue
        ? $"{PartySize}/{PartyMax}"
        : string.Empty;

    public string TypeIcon => Type switch
    {
        RichPresenceType.Playing => "🎮",
        RichPresenceType.Streaming => "📺",
        RichPresenceType.Listening => "🎵",
        RichPresenceType.Watching => "📽️",
        RichPresenceType.Competing => "🏆",
        RichPresenceType.Custom => "💬",
        _ => "🔵"
    };

    public string TypeText => Type switch
    {
        RichPresenceType.Playing => "Playing",
        RichPresenceType.Streaming => "Streaming",
        RichPresenceType.Listening => "Listening to",
        RichPresenceType.Watching => "Watching",
        RichPresenceType.Competing => "Competing in",
        RichPresenceType.Custom => string.Empty,
        _ => string.Empty
    };
}

/// <summary>
/// Types of rich presence activities
/// </summary>
public enum RichPresenceType
{
    Playing,
    Streaming,
    Listening,
    Watching,
    Competing,
    Custom
}

/// <summary>
/// Custom user activity status
/// </summary>
public class CustomActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Emoji { get; set; } = "💬";
    public string Text { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool ClearOnStatusChange { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public string Display => $"{Emoji} {Text}";
}

#endregion

#region Message Reactions and Pinned Messages

/// <summary>
/// A reaction to a message
/// </summary>
public class MessageReaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsCustomEmoji { get; set; }
    public string? CustomEmojiUrl { get; set; }
    public DateTime ReactedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Grouped reactions for display
/// </summary>
public class ReactionGroup
{
    public string Emoji { get; set; } = string.Empty;
    public bool IsCustomEmoji { get; set; }
    public string? CustomEmojiUrl { get; set; }
    public int Count { get; set; }
    public List<string> UserIds { get; set; } = new();
    public List<string> Usernames { get; set; } = new();
    public bool CurrentUserReacted { get; set; }

    public string Tooltip => Count == 1
        ? Usernames.FirstOrDefault() ?? "1 reaction"
        : Count <= 3
            ? string.Join(", ", Usernames)
            : $"{string.Join(", ", Usernames.Take(2))} and {Count - 2} others";
}

/// <summary>
/// A pinned message in a conversation
/// </summary>
public class PinnedMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string PinnedByUserId { get; set; } = string.Empty;
    public string PinnedByUsername { get; set; } = string.Empty;
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    // Message content snapshot
    public string SenderUsername { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime MessageTimestamp { get; set; }
}

/// <summary>
/// Enhanced direct message with reactions and pin support
/// </summary>
public class EnhancedDirectMessage
{
    public string Id { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }
    public List<ReactionGroup> Reactions { get; set; } = new();
    public string? ReplyToMessageId { get; set; }
    public EnhancedDirectMessage? ReplyToMessage { get; set; }
    public List<MessageAttachment> Attachments { get; set; } = new();

    // UI helpers
    public bool HasReactions => Reactions.Count > 0;
    public bool IsEdited => EditedAt.HasValue;
    public bool IsReply => !string.IsNullOrEmpty(ReplyToMessageId);
    public int TotalReactionCount => Reactions.Sum(r => r.Count);
}

/// <summary>
/// Message attachment for media/files
/// </summary>
public class MessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Filename { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? ThumbnailUrl { get; set; }

    public bool IsImage => ContentType.StartsWith("image/");
    public bool IsVideo => ContentType.StartsWith("video/");
    public bool IsAudio => ContentType.StartsWith("audio/");
    public string SizeFormatted
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}

#endregion

#region Friend Recommendations

/// <summary>
/// A friend recommendation based on mutual connections
/// </summary>
public class FriendRecommendation
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public RecommendationReason Reason { get; set; }
    public List<MutualConnection> MutualFriends { get; set; } = new();
    public List<string> SharedServers { get; set; } = new();
    public List<string> CommonInterests { get; set; } = new();
    public double RecommendationScore { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public bool IsDismissed { get; set; }

    public string ReasonText => Reason switch
    {
        RecommendationReason.MutualFriends => $"{MutualFriends.Count} mutual friend{(MutualFriends.Count != 1 ? "s" : "")}",
        RecommendationReason.SharedServer => $"From {SharedServers.FirstOrDefault() ?? "a shared server"}",
        RecommendationReason.CommonInterests => $"Common interests: {string.Join(", ", CommonInterests.Take(2))}",
        RecommendationReason.SuggestedByFriend => "Suggested by a friend",
        RecommendationReason.RecentlyActive => "Active in your communities",
        _ => "You may know"
    };

    public string ReasonIcon => Reason switch
    {
        RecommendationReason.MutualFriends => "👥",
        RecommendationReason.SharedServer => "🏠",
        RecommendationReason.CommonInterests => "🎯",
        RecommendationReason.SuggestedByFriend => "💡",
        RecommendationReason.RecentlyActive => "⚡",
        _ => "✨"
    };
}

/// <summary>
/// Mutual connection info for recommendations
/// </summary>
public class MutualConnection
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// Why a friend was recommended
/// </summary>
public enum RecommendationReason
{
    MutualFriends,
    SharedServer,
    CommonInterests,
    SuggestedByFriend,
    RecentlyActive
}

#endregion

#region Friend Statistics

/// <summary>
/// Statistics and insights about friendships
/// </summary>
public class FriendshipStats
{
    public int TotalFriends { get; set; }
    public int OnlineFriends { get; set; }
    public int NewFriendsThisMonth { get; set; }
    public int TotalConversations { get; set; }
    public int ActiveConversationsToday { get; set; }
    public int TotalVoiceMinutesThisWeek { get; set; }
    public int TotalMessagesThisWeek { get; set; }

    // Top friends by interaction
    public List<FriendInteractionSummary> TopFriendsByInteraction { get; set; } = new();

    // Activity breakdown
    public Dictionary<DayOfWeek, int> MessagesByDayOfWeek { get; set; } = new();
    public Dictionary<int, int> MessagesByHour { get; set; } = new();

    // Streaks
    public int CurrentDailyStreak { get; set; }
    public int LongestDailyStreak { get; set; }
    public DateTime? LastActiveDay { get; set; }
}

/// <summary>
/// Summary of interaction with a specific friend
/// </summary>
public class FriendInteractionSummary
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string? FriendAvatarUrl { get; set; }
    public int TotalInteractions { get; set; }
    public DateTime LastInteraction { get; set; }
    public TimeSpan FriendshipDuration { get; set; }

    public string FriendshipDurationText
    {
        get
        {
            if (FriendshipDuration.TotalDays < 1) return "Less than a day";
            if (FriendshipDuration.TotalDays < 30) return $"{(int)FriendshipDuration.TotalDays} days";
            if (FriendshipDuration.TotalDays < 365) return $"{(int)(FriendshipDuration.TotalDays / 30)} months";
            var years = (int)(FriendshipDuration.TotalDays / 365);
            return $"{years} year{(years != 1 ? "s" : "")}";
        }
    }
}

#endregion
