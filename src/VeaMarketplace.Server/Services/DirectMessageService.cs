using Microsoft.Extensions.Logging;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class DirectMessageService
{
    private readonly DatabaseService _db;
    private readonly FriendService _friendService;
    private readonly ILogger<DirectMessageService> _logger;

    public DirectMessageService(DatabaseService db, FriendService friendService, ILogger<DirectMessageService> logger)
    {
        _db = db;
        _friendService = friendService;
        _logger = logger;
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
            // Skip blocked users
            if (_friendService.IsBlocked(userId, partnerId)) continue;

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
        // Don't show messages if either user has blocked the other
        if (_friendService.IsBlocked(userId, partnerId))
            return [];

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

        // Check if either user has blocked the other
        if (_friendService.IsBlocked(senderId, recipientId))
            return (false, "Unable to send message to this user", null);

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

        try
        {
            _db.DirectMessages.Insert(message);

            // Track response metrics for seller response rate (non-critical, log errors but don't fail)
            try
            {
                TrackResponseMetrics(senderId, recipientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track response metrics for message from {SenderId} to {RecipientId}", senderId, recipientId);
            }

            // Update recipient's message received tracking (non-critical)
            try
            {
                recipient.TotalMessagesReceived++;
                recipient.LastMessageReceivedAt = DateTime.UtcNow;
                _db.Users.Update(recipient);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update message tracking for recipient {RecipientId}", recipientId);
            }

            return (true, "Message sent", MapToDto(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message from {SenderId} to {RecipientId}", senderId, recipientId);
            return (false, "Failed to send message", null);
        }
    }

    public void MarkAsRead(string userId, string partnerId)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages as read for user {UserId} from partner {PartnerId}", userId, partnerId);
        }
    }

    public bool DeleteMessage(string messageId, string userId)
    {
        try
        {
            var message = _db.DirectMessages.FindById(messageId);
            if (message == null) return false;

            // Only sender can delete
            if (message.SenderId != userId) return false;

            message.IsDeleted = true;
            _db.DirectMessages.Update(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId} for user {UserId}", messageId, userId);
            return false;
        }
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
