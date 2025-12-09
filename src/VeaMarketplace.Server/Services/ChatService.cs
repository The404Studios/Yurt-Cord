using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ChatService
{
    private readonly DatabaseService _db;

    public ChatService(DatabaseService db)
    {
        _db = db;
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
            Timestamp = DateTime.UtcNow
        };

        _db.Messages.Insert(message);

        return MapToDto(message, user);
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
