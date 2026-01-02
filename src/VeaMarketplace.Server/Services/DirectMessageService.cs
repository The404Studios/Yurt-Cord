using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class DirectMessageService
{
    private readonly DatabaseService _db;
    private readonly FriendService _friendService;

    public DirectMessageService(DatabaseService db, FriendService friendService)
    {
        _db = db;
        _friendService = friendService;
    }

    public List<ConversationDto> GetConversations(string userId)
    {
        // Get all DMs involving this user
        var sentMessages = _db.DirectMessages
            .Find(m => m.SenderId == userId && !m.IsDeleted)
            .ToList();

        var receivedMessages = _db.DirectMessages
            .Find(m => m.RecipientId == userId && !m.IsDeleted)
            .ToList();

        // Get unique conversation partners
        var partnerIds = sentMessages.Select(m => m.RecipientId)
            .Union(receivedMessages.Select(m => m.SenderId))
            .Distinct()
            .ToList();

        var conversations = new List<ConversationDto>();
        foreach (var partnerId in partnerIds)
        {
            var partner = _db.Users.FindById(partnerId);
            if (partner == null) continue;

            // Get last message
            var lastMessage = _db.DirectMessages
                .Query()
                .Where(m => !m.IsDeleted &&
                           ((m.SenderId == userId && m.RecipientId == partnerId) ||
                            (m.SenderId == partnerId && m.RecipientId == userId)))
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            // Count unread
            var unreadCount = _db.DirectMessages
                .Count(m => m.SenderId == partnerId && m.RecipientId == userId && !m.IsRead && !m.IsDeleted);

            conversations.Add(new ConversationDto
            {
                UserId = partner.Id,
                Username = partner.Username,
                AvatarUrl = partner.AvatarUrl,
                IsOnline = partner.IsOnline,
                LastMessage = lastMessage?.Content,
                LastMessageAt = lastMessage?.Timestamp,
                UnreadCount = unreadCount
            });
        }

        return conversations.OrderByDescending(c => c.LastMessageAt).ToList();
    }

    public List<DirectMessageDto> GetMessages(string userId, string partnerId, int limit = 50)
    {
        var messages = _db.DirectMessages
            .Query()
            .Where(m => !m.IsDeleted &&
                       ((m.SenderId == userId && m.RecipientId == partnerId) ||
                        (m.SenderId == partnerId && m.RecipientId == userId)))
            .OrderByDescending(m => m.Timestamp)
            .Limit(limit)
            .ToList()
            .OrderBy(m => m.Timestamp)
            .ToList();

        return messages.Select(MapToDto).ToList();
    }

    public (bool Success, string Message, DirectMessageDto? Dto) SendMessage(string senderId, string recipientId, string content)
    {
        var sender = _db.Users.FindById(senderId);
        var recipient = _db.Users.FindById(recipientId);

        if (sender == null || recipient == null)
            return (false, "User not found", null);

        // Check if they are friends (optional - can allow DMs to anyone)
        // For now, we'll allow DMs to friends only
        if (!_friendService.AreFriends(senderId, recipientId))
            return (false, "You can only send messages to friends", null);

        var message = new DirectMessage
        {
            SenderId = senderId,
            SenderUsername = sender.Username,
            SenderAvatarUrl = sender.AvatarUrl,
            RecipientId = recipientId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        _db.DirectMessages.Insert(message);

        // Track response metrics for seller response rate
        TrackResponseMetrics(senderId, recipientId);

        // Update recipient's message received tracking
        recipient.TotalMessagesReceived++;
        recipient.LastMessageReceivedAt = DateTime.UtcNow;
        _db.Users.Update(recipient);

        return (true, "Message sent", MapToDto(message));
    }

    public void MarkAsRead(string userId, string partnerId)
    {
        var unreadMessages = _db.DirectMessages
            .Find(m => m.SenderId == partnerId && m.RecipientId == userId && !m.IsRead)
            .ToList();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
            _db.DirectMessages.Update(message);
        }
    }

    public bool DeleteMessage(string messageId, string userId)
    {
        var message = _db.DirectMessages.FindById(messageId);
        if (message == null) return false;

        // Only sender can delete
        if (message.SenderId != userId) return false;

        message.IsDeleted = true;
        _db.DirectMessages.Update(message);
        return true;
    }

    /// <summary>
    /// Tracks response metrics when a user sends a message.
    /// If this message is a response to a previous message, it updates the sender's response stats.
    /// </summary>
    private void TrackResponseMetrics(string senderId, string recipientId)
    {
        var sender = _db.Users.FindById(senderId);
        if (sender == null) return;

        // Check if there's a recent message from the recipient that this user is responding to
        var lastReceivedMessage = _db.DirectMessages
            .Query()
            .Where(m => m.SenderId == recipientId && m.RecipientId == senderId && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefault();

        // Check if sender had any previous messages to recipient (indicating this is a response, not new conversation)
        var lastSentMessage = _db.DirectMessages
            .Query()
            .Where(m => m.SenderId == senderId && m.RecipientId == recipientId && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefault();

        // If there's a received message that is more recent than our last sent message, this is a response
        if (lastReceivedMessage != null &&
            (lastSentMessage == null || lastReceivedMessage.Timestamp > lastSentMessage.Timestamp))
        {
            // Calculate response time
            var responseTime = DateTime.UtcNow - lastReceivedMessage.Timestamp;

            // Only count responses within 7 days as valid (to exclude very old messages)
            if (responseTime.TotalDays <= 7)
            {
                sender.TotalMessagesResponded++;
                sender.TotalResponseTimeMs += (long)responseTime.TotalMilliseconds;
                sender.LastMessageRespondedAt = DateTime.UtcNow;
                _db.Users.Update(sender);
            }
        }
    }

    private static DirectMessageDto MapToDto(DirectMessage message)
    {
        return new DirectMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderUsername = message.SenderUsername,
            SenderAvatarUrl = message.SenderAvatarUrl,
            RecipientId = message.RecipientId,
            Content = message.Content,
            Timestamp = message.Timestamp,
            IsRead = message.IsRead
        };
    }
}
