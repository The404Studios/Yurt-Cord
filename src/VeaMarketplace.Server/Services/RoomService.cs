using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class RoomService
{
    private readonly DatabaseService _db;

    public RoomService(DatabaseService db)
    {
        _db = db;
    }

    public (bool success, string message, Room? room) CreateRoom(string userId, string username, CreateRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (false, "Room name is required", null);

        if (request.Name.Length > 100)
            return (false, "Room name must be 100 characters or less", null);

        var room = new Room
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            IconUrl = request.IconUrl,
            OwnerId = userId,
            OwnerUsername = username,
            IsPublic = request.IsPublic,
            AllowMarketplace = request.AllowMarketplace,
            AllowVoice = request.AllowVoice,
            AllowVideo = request.AllowVideo,
            AllowScreenShare = request.AllowScreenShare
        };

        // Create default channels
        room.TextChannels.Add(new RoomChannel
        {
            Name = "general",
            Type = RoomChannelType.Text,
            Position = 0
        });

        room.VoiceChannels.Add(new RoomChannel
        {
            Name = "General Voice",
            Type = RoomChannelType.Voice,
            Position = 0,
            MaxUsers = 25,
            Bitrate = 64,
            VideoEnabled = true,
            ScreenShareEnabled = true
        });

        // Create default roles
        var everyoneRole = new RoomRole
        {
            Name = "@everyone",
            Color = "#99AAB5",
            Position = 0,
            IsDefault = true,
            Permissions = new RoomPermissions()
        };
        everyoneRole.Permissions = RoomPermissions.ViewChannels | RoomPermissions.SendMessages |
                                   RoomPermissions.Connect | RoomPermissions.Speak | RoomPermissions.Video;

        var moderatorRole = new RoomRole
        {
            Name = "Moderator",
            Color = "#3498DB",
            Position = 1,
            Permissions = everyoneRole.Permissions | RoomPermissions.KickMembers |
                         RoomPermissions.MuteMembers | RoomPermissions.ManageMessages
        };

        var adminRole = new RoomRole
        {
            Name = "Admin",
            Color = "#E74C3C",
            Position = 2,
            Permissions = RoomPermissions.Administrator
        };

        room.Roles.Add(everyoneRole);
        room.Roles.Add(moderatorRole);
        room.Roles.Add(adminRole);

        // Add owner as member with admin role
        room.Members.Add(new RoomMember
        {
            UserId = userId,
            Username = username,
            RoleIds = new List<string> { adminRole.Id, everyoneRole.Id }
        });
        room.TotalMembers = 1;

        _db.Rooms.Insert(room);

        return (true, "Room created successfully", room);
    }

    public Room? GetRoom(string roomId)
    {
        return _db.Rooms.FindById(roomId);
    }

    public List<Room> GetPublicRooms(int skip = 0, int take = 50)
    {
        return _db.Rooms.Find(r => r.IsPublic)
            .OrderByDescending(r => r.OnlineMembers)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public List<Room> GetUserRooms(string userId)
    {
        return _db.Rooms.Find(r => r.Members.Any(m => m.UserId == userId)).ToList();
    }

    public (bool success, string message) UpdateRoom(string userId, string roomId, UpdateRoomRequest request)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found");

        if (!HasPermission(userId, room, RoomPermissions.ManageRoom))
            return (false, "Insufficient permissions");

        if (request.Name != null) room.Name = request.Name;
        if (request.Description != null) room.Description = request.Description;
        if (request.IconUrl != null) room.IconUrl = request.IconUrl;
        if (request.BannerUrl != null) room.BannerUrl = request.BannerUrl;
        if (request.IsPublic.HasValue) room.IsPublic = request.IsPublic.Value;
        if (request.AllowMarketplace.HasValue) room.AllowMarketplace = request.AllowMarketplace.Value;
        if (request.AllowVoice.HasValue) room.AllowVoice = request.AllowVoice.Value;
        if (request.AllowVideo.HasValue) room.AllowVideo = request.AllowVideo.Value;
        if (request.AllowScreenShare.HasValue) room.AllowScreenShare = request.AllowScreenShare.Value;
        if (request.MaxMembers.HasValue) room.MaxMembers = request.MaxMembers.Value;
        if (request.MaxConcurrentStreams.HasValue) room.MaxConcurrentStreams = request.MaxConcurrentStreams.Value;
        if (request.StreamingTier.HasValue) room.StreamingTier = request.StreamingTier.Value;
        if (request.MarketplaceFeePercent.HasValue) room.MarketplaceFeePercent = request.MarketplaceFeePercent.Value;

        _db.Rooms.Update(room);
        return (true, "Room updated successfully");
    }

    public (bool success, string message) JoinRoom(string userId, string username, string roomId, string? avatarUrl)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found");

        if (room.Members.Any(m => m.UserId == userId))
            return (false, "Already a member of this room");

        if (room.Members.Count >= room.MaxMembers)
            return (false, "Room is full");

        var defaultRole = room.Roles.FirstOrDefault(r => r.IsDefault);
        room.Members.Add(new RoomMember
        {
            UserId = userId,
            Username = username,
            AvatarUrl = avatarUrl,
            RoleIds = defaultRole != null ? new List<string> { defaultRole.Id } : new List<string>()
        });
        room.TotalMembers++;

        _db.Rooms.Update(room);
        return (true, "Joined room successfully");
    }

    public (bool success, string message) LeaveRoom(string userId, string roomId)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found");

        var member = room.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return (false, "Not a member of this room");

        if (room.OwnerId == userId)
            return (false, "Owner cannot leave. Transfer ownership or delete the room.");

        room.Members.Remove(member);
        room.TotalMembers--;

        _db.Rooms.Update(room);
        return (true, "Left room successfully");
    }

    public (bool success, string message, RoomChannel? channel) CreateChannel(string userId, string roomId, CreateChannelRequest request)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found", null);

        if (!HasPermission(userId, room, RoomPermissions.ManageChannels))
            return (false, "Insufficient permissions", null);

        var channel = new RoomChannel
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            ParentId = request.ParentId,
            IsPrivate = request.IsPrivate,
            MaxUsers = request.MaxUsers,
            Bitrate = request.Bitrate,
            VideoEnabled = request.VideoEnabled,
            ScreenShareEnabled = request.ScreenShareEnabled,
            Position = request.Type == RoomChannelType.Voice
                ? room.VoiceChannels.Count
                : room.TextChannels.Count
        };

        if (request.Type == RoomChannelType.Voice || request.Type == RoomChannelType.Video || request.Type == RoomChannelType.Stage)
            room.VoiceChannels.Add(channel);
        else
            room.TextChannels.Add(channel);

        _db.Rooms.Update(room);
        return (true, "Channel created successfully", channel);
    }

    public (bool success, string message, RoomRole? role) CreateRole(string userId, string roomId, CreateRoleRequest request)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found", null);

        if (!HasPermission(userId, room, RoomPermissions.ManageRoles))
            return (false, "Insufficient permissions", null);

        var role = new RoomRole
        {
            Name = request.Name,
            Color = request.Color,
            Permissions = request.Permissions,
            Position = room.Roles.Count
        };

        room.Roles.Add(role);
        _db.Rooms.Update(room);

        return (true, "Role created successfully", role);
    }

    public (bool success, string message) AssignRole(string userId, string roomId, string targetUserId, string roleId)
    {
        var room = _db.Rooms.FindById(roomId);
        if (room == null)
            return (false, "Room not found");

        if (!HasPermission(userId, room, RoomPermissions.ManageRoles))
            return (false, "Insufficient permissions");

        var member = room.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (member == null)
            return (false, "Member not found");

        var role = room.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
            return (false, "Role not found");

        if (!member.RoleIds.Contains(roleId))
        {
            member.RoleIds.Add(roleId);
            _db.Rooms.Update(room);
        }

        return (true, "Role assigned successfully");
    }

    public bool HasPermission(string userId, Room room, RoomPermissions permission)
    {
        // Owner has all permissions
        if (room.OwnerId == userId)
            return true;

        var member = room.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return false;

        var memberPermissions = RoomPermissions.None;
        foreach (var roleId in member.RoleIds)
        {
            var role = room.Roles.FirstOrDefault(r => r.Id == roleId);
            if (role != null)
            {
                // Administrator has all permissions
                if (role.Permissions == RoomPermissions.Administrator)
                    return true;
                memberPermissions |= role.Permissions;
            }
        }

        return memberPermissions.HasFlag(permission);
    }

    public bool CanStream(string userId, Room room, StreamingTier requiredTier)
    {
        // Check room allows streaming
        if (!room.AllowVideo && !room.AllowScreenShare)
            return false;

        // Check streaming tier
        if (requiredTier > room.StreamingTier)
            return false;

        // Check permissions
        var member = room.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return false;

        // Owner can always stream
        if (room.OwnerId == userId)
            return true;

        var hasBasicStream = HasPermission(userId, room, RoomPermissions.Video | RoomPermissions.ScreenShare);
        if (!hasBasicStream)
            return false;

        // Check for high quality streaming permission
        if (requiredTier >= StreamingTier.Premium && !HasPermission(userId, room, RoomPermissions.StreamHighQuality))
            return false;

        if (requiredTier >= StreamingTier.Ultra && !HasPermission(userId, room, RoomPermissions.StreamUltraQuality))
            return false;

        return true;
    }

    public RoomDto MapToDto(Room room)
    {
        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            Description = room.Description,
            IconUrl = room.IconUrl,
            BannerUrl = room.BannerUrl,
            OwnerId = room.OwnerId,
            OwnerUsername = room.OwnerUsername,
            IsPublic = room.IsPublic,
            AllowMarketplace = room.AllowMarketplace,
            AllowVoice = room.AllowVoice,
            AllowVideo = room.AllowVideo,
            AllowScreenShare = room.AllowScreenShare,
            MaxMembers = room.MaxMembers,
            MaxConcurrentStreams = room.MaxConcurrentStreams,
            StreamingTier = room.StreamingTier,
            TotalMembers = room.TotalMembers,
            OnlineMembers = room.OnlineMembers,
            TextChannels = room.TextChannels.Select(c => new RoomChannelDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Type = c.Type,
                Position = c.Position,
                ParentId = c.ParentId,
                IsPrivate = c.IsPrivate
            }).ToList(),
            VoiceChannels = room.VoiceChannels.Select(c => new RoomChannelDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Type = c.Type,
                Position = c.Position,
                ParentId = c.ParentId,
                IsPrivate = c.IsPrivate,
                MaxUsers = c.MaxUsers,
                Bitrate = c.Bitrate,
                VideoEnabled = c.VideoEnabled,
                ScreenShareEnabled = c.ScreenShareEnabled
            }).ToList(),
            Roles = room.Roles.Select(r => new RoomRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Color = r.Color,
                Position = r.Position,
                IsDefault = r.IsDefault,
                Permissions = r.Permissions
            }).ToList(),
            CreatedAt = room.CreatedAt
        };
    }
}
