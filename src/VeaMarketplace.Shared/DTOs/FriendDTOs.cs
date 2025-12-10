using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Shared.DTOs;

public class FriendDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserRank Rank { get; set; }
    public bool IsOnline { get; set; }
    public DateTime FriendsSince { get; set; }
}

public class FriendRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterUsername { get; set; } = string.Empty;
    public string RequesterAvatarUrl { get; set; } = string.Empty;
    public UserRole RequesterRole { get; set; }
    public DateTime RequestedAt { get; set; }
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

public class CallResponseDto
{
    public string CallId { get; set; } = string.Empty;
    public bool Accept { get; set; }
}
