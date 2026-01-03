using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ChatService
{
    private readonly DatabaseService _db;
    private readonly FileService? _fileService;

    public ChatService(DatabaseService db, FileService? fileService = null)
    {
        _db = db;
        _fileService = fileService;
    }

    public List<ChannelDto> GetChannels(UserRole userRole)
    {
        return _db.Channels
            .Find(c => c.MinimumRole <= userRole)
            .Select(c => new ChannelDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Icon = c.Icon,
                OnlineCount = c.OnlineCount,
                MinimumRole = c.MinimumRole
            })
            .ToList();
    }

    public List<ChatMessageDto> GetChannelHistory(string channelName, int limit = 50)
    {
        var messages = _db.Messages
            .Query()
            .Where(m => m.Channel == channelName && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Limit(limit)
            .ToList()
            .OrderBy(m => m.Timestamp)
            .ToList();

        return messages.Select(m =>
        {
            var sender = _db.Users.FindById(m.SenderId);
            return MapToDto(m, sender);
        }).ToList();
    }

    public ChatMessageDto SaveMessage(string userId, SendMessageRequest request)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) throw new Exception("User not found");

        var message = new ChatMessage
        {
            SenderId = userId,
            SenderUsername = user.Username,
            SenderRole = user.Role,
            SenderRank = user.Rank,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            AttachmentIds = request.AttachmentIds ?? new List<string>()
        };

        _db.Messages.Insert(message);

        return MapToDto(message, user);
    }

    public ChatMessageDto SaveMessageWithAttachments(string userId, SendMessageRequest request, List<MessageAttachmentDto> attachments)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) throw new Exception("User not found");

        var message = new ChatMessage
        {
            SenderId = userId,
            SenderUsername = user.Username,
            SenderRole = user.Role,
            SenderRank = user.Rank,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            AttachmentIds = attachments.Select(a => a.Id).ToList()
        };

        _db.Messages.Insert(message);

        var dto = MapToDto(message, user);
        dto.Attachments = attachments;
        return dto;
    }

    public ChatMessageDto CreateSystemMessage(string channel, string content, MessageType type = MessageType.System)
    {
        var message = new ChatMessage
        {
            SenderId = "system",
            SenderUsername = "System",
            Content = content,
            Type = type,
            Channel = channel,
            Timestamp = DateTime.UtcNow
        };

        _db.Messages.Insert(message);

        return new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderUsername = message.SenderUsername,
            SenderAvatarUrl = "",
            SenderRole = UserRole.Admin,
            SenderRank = UserRank.Legend,
            Content = message.Content,
            Type = message.Type,
            Channel = message.Channel,
            Timestamp = message.Timestamp,
            IsEdited = false
        };
    }

    public bool DeleteMessage(string messageId, string userId)
    {
        var message = _db.Messages.FindById(messageId);
        if (message == null) return false;

        var user = _db.Users.FindById(userId);
        if (user == null) return false;

        // Only allow deletion by message owner or moderators+
        if (message.SenderId != userId && user.Role < UserRole.Moderator)
            return false;

        message.IsDeleted = true;
        _db.Messages.Update(message);
        return true;
    }

    /// <summary>
    /// Adds a reaction to a message. Returns the updated reaction info or null if failed.
    /// </summary>
    public (bool Success, string Channel, MessageReactionDto? Reaction) AddReaction(string userId, string messageId, string emoji)
    {
        var message = _db.Messages.FindById(messageId);
        if (message == null) return (false, "", null);

        var user = _db.Users.FindById(userId);
        if (user == null) return (false, "", null);

        // Check if user already reacted with this emoji
        var existingReaction = _db.MessageReactions
            .FindOne(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

        if (existingReaction != null)
            return (false, message.Channel, null); // Already reacted

        // Add the reaction
        var reaction = new MessageReaction
        {
            MessageId = messageId,
            UserId = userId,
            Username = user.Username,
            Emoji = emoji
        };
        _db.MessageReactions.Insert(reaction);

        // Update reaction counts on the message
        if (!message.ReactionCounts.ContainsKey(emoji))
            message.ReactionCounts[emoji] = 0;
        message.ReactionCounts[emoji]++;
        _db.Messages.Update(message);

        var dto = new MessageReactionDto
        {
            Id = reaction.Id,
            MessageId = messageId,
            UserId = userId,
            Username = user.Username,
            Emoji = emoji,
            CreatedAt = reaction.CreatedAt
        };

        return (true, message.Channel, dto);
    }

    /// <summary>
    /// Removes a reaction from a message. Returns success and channel name.
    /// </summary>
    public (bool Success, string Channel) RemoveReaction(string userId, string messageId, string emoji)
    {
        var message = _db.Messages.FindById(messageId);
        if (message == null) return (false, "");

        var reaction = _db.MessageReactions
            .FindOne(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

        if (reaction == null) return (false, message.Channel);

        _db.MessageReactions.Delete(reaction.Id);

        // Update reaction counts
        if (message.ReactionCounts.ContainsKey(emoji))
        {
            message.ReactionCounts[emoji]--;
            if (message.ReactionCounts[emoji] <= 0)
                message.ReactionCounts.Remove(emoji);
            _db.Messages.Update(message);
        }

        return (true, message.Channel);
    }

    /// <summary>
    /// Gets all reactions for a message grouped by emoji
    /// </summary>
    public List<ReactionGroupDto> GetMessageReactions(string messageId)
    {
        var reactions = _db.MessageReactions
            .Find(r => r.MessageId == messageId)
            .ToList();

        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new ReactionGroupDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                UserNames = g.Select(r => r.Username).ToList(),
                HasCurrentUserReacted = false // Set by caller based on current user
            })
            .ToList();
    }

    private static ChatMessageDto MapToDto(ChatMessage message, User? sender)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderUsername = message.SenderUsername,
            SenderAvatarUrl = sender?.AvatarUrl ?? "",
            SenderRole = message.SenderRole,
            SenderRank = message.SenderRank,
            Content = message.Content,
            Type = message.Type,
            Channel = message.Channel,
            Timestamp = message.Timestamp,
            IsEdited = message.IsEdited
        };
    }
}
