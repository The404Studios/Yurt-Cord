using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class FriendDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#00B4D8"; // Yurt Cord teal
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public bool IsOnline { get; set; }
    public DateTime FriendsSince { get; set; }

    // Helper to get display name or username
    public string GetDisplayName() => string.IsNullOrEmpty(DisplayName) ? Username : DisplayName;
}

public class UserSearchResultDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public bool IsOnline { get; set; }
    public bool IsFriend { get; set; }

    // Helper to get display name or username
    public string GetDisplayName() => string.IsNullOrEmpty(DisplayName) ? Username : DisplayName;
}

public class FriendRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterUsername { get; set; } = string.Empty;
    public string RequesterAvatarUrl { get; set; } = string.Empty;
    public UserRole RequesterRole { get; set; }
    public DateTime RequestedAt { get; set; }

    // Additional fields for both incoming and outgoing request support
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;

    // Alias for RequesterId for clearer semantics
    public string SenderId => RequesterId;
}

public class SendFriendRequestDto
{
    public string Username { get; set; } = string.Empty;
}

public class FriendRequestResponseDto
{
    public string RequestId { get; set; } = string.Empty;
    public bool Accept { get; set; }
}

public class DirectMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
}

public class SendDirectMessageDto
{
    public string RecipientId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ConversationDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class VoiceCallDto
{
    public string Id { get; set; } = string.Empty;
    public string CallerId { get; set; } = string.Empty;
    public string CallerUsername { get; set; } = string.Empty;
    public string CallerAvatarUrl { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string RecipientAvatarUrl { get; set; } = string.Empty;
    public VoiceCallStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public TimeSpan Duration { get; set; }
}

public class StartCallDto
{
    public string RecipientId { get; set; } = string.Empty;
}

// Group call DTOs
public class GroupCallDto
{
    public string Id { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string HostAvatarUrl { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<GroupCallParticipantDto> Participants { get; set; } = [];
    public GroupCallStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public bool AllowVideo { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
}

public class GroupCallParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsScreenSharing { get; set; }
    public bool HasVideo { get; set; }
    public GroupCallParticipantStatus Status { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class StartGroupCallDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> InvitedUserIds { get; set; } = [];
    public bool AllowVideo { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
}

public class GroupCallInviteDto
{
    public string CallId { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string HostAvatarUrl { get; set; } = string.Empty;
    public string CallName { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public DateTime InvitedAt { get; set; }
}

public enum GroupCallStatus
{
    Starting,
    Active,
    Ended
}

public enum GroupCallParticipantStatus
{
    Invited,
    Ringing,
    Joined,
    Declined,
    Left
}

public class CallResponseDto
{
    public string CallId { get; set; } = string.Empty;
    public bool Accept { get; set; }
}

public class BlockedUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
    public string? Reason { get; set; }
}

public class BlockUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class CancelFriendRequestDto
{
    public string RequestId { get; set; } = string.Empty;
}

public class TypingIndicatorDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
    public DateTime Timestamp { get; set; }
}

// MessageReactionDto is defined in ChatDTOs.cs

// Voice Room DTOs for room/server system
public class VoiceRoomDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string HostAvatarUrl { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public string? Password { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public int CurrentParticipants { get; set; }
    public List<VoiceRoomParticipantDto> Participants { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public VoiceRoomCategory Category { get; set; } = VoiceRoomCategory.General;
    public bool AllowScreenShare { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class VoiceRoomParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public bool IsModerator { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsScreenSharing { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateVoiceRoomDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public string? Password { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public VoiceRoomCategory Category { get; set; } = VoiceRoomCategory.General;
    public bool AllowScreenShare { get; set; } = true;
}

public class JoinVoiceRoomDto
{
    public string RoomId { get; set; } = string.Empty;
    public string? Password { get; set; }
}

public class VoiceRoomSearchDto
{
    public string? Query { get; set; }
    public VoiceRoomCategory? Category { get; set; }
    public bool OnlyWithSpace { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class NudgeDto
{
    public string FromUserId { get; set; } = string.Empty;
    public string FromUsername { get; set; } = string.Empty;
    public string FromAvatarUrl { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Message { get; set; }
}

public enum VoiceRoomCategory
{
    General,
    Gaming,
    Music,
    Study,
    Work,
    Social,
    Movies,
    Sports,
    Other
}

public class CreateGroupChatRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public string? IconPath { get; set; }
}
