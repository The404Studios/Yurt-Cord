using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Hubs;

/// <summary>
/// Hub for room/server management with support for multiple concurrent streams,
/// role-based permissions, and integrated marketplace functionality.
/// </summary>
public class RoomHub : Hub
{
    private readonly RoomService _roomService;
    private readonly AuthService _authService;
    private readonly ProductService _productService;

    // Connection tracking (support multiple connections per user)
    private static readonly ConcurrentDictionary<string, List<string>> _userConnections = new(); // userId -> connectionIds
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, string> _connectionRooms = new(); // connectionId -> roomId
    private static readonly ConcurrentDictionary<string, DateTime> _connectionTimestamps = new();

    // Active streams per room: roomId -> list of stream info
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StreamInfoDto>> _activeStreams = new();

    // Bandwidth tracking per user (for rate limiting): userId -> (bytesUsed, lastReset)
    private static readonly ConcurrentDictionary<string, (long bytesUsed, DateTime lastReset)> _bandwidthUsage = new();

    // Constants for bandwidth limits
    private const long MaxUploadBytesPerSecond = 30L * 1024 * 1024; // 30 MB/s upload
    private const long MaxDownloadBytesPerSecond = 50L * 1024 * 1024; // 50 MB/s download per user

    public RoomHub(RoomService roomService, AuthService authService, ProductService productService)
    {
        _roomService = roomService;
        _authService = authService;
        _productService = productService;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _connectionTimestamps[connectionId] = DateTime.UtcNow;

        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = connectionId,
            ServerTime = DateTime.UtcNow,
            Hub = "RoomHub"
        });

        await base.OnConnectedAsync();
    }

    public async Task Ping()
    {
        _connectionTimestamps[Context.ConnectionId] = DateTime.UtcNow;
        await Clients.Caller.SendAsync("Pong", new { ServerTime = DateTime.UtcNow });
    }

    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        // Track connection (support multiple connections per user)
        _userConnections.AddOrUpdate(
            user.Id,
            _ => new List<string> { Context.ConnectionId },
            (_, list) => { lock (list) { if (!list.Contains(Context.ConnectionId)) list.Add(Context.ConnectionId); } return list; }
        );
        _connectionUsers[Context.ConnectionId] = user.Id;

        // Add to personal group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");

        // Send user's rooms
        var rooms = _roomService.GetUserRooms(user.Id);
        var roomDtos = rooms.Select(r => _roomService.MapToDto(r)).ToList();
        await Clients.Caller.SendAsync("UserRooms", roomDtos);

        await Clients.Caller.SendAsync("AuthenticationSuccess");
    }

    // === Room Management ===

    public async Task CreateRoom(CreateRoomRequest request)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var user = _authService.GetUserById(userId);
        if (user == null)
        {
            await Clients.Caller.SendAsync("RoomError", "User not found");
            return;
        }

        var (success, message, room) = _roomService.CreateRoom(userId, user.Username, request);

        if (success && room != null)
        {
            var roomDto = _roomService.MapToDto(room);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{room.Id}");
            _connectionRooms[Context.ConnectionId] = room.Id;
            await Clients.Caller.SendAsync("RoomCreated", roomDto);
        }
        else
        {
            await Clients.Caller.SendAsync("RoomError", message);
        }
    }

    public async Task JoinRoom(string roomId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var user = _authService.GetUserById(userId);
        if (user == null)
        {
            await Clients.Caller.SendAsync("RoomError", "User not found");
            return;
        }

        var room = _roomService.GetRoom(roomId);
        if (room == null)
        {
            await Clients.Caller.SendAsync("RoomError", "Room not found");
            return;
        }

        // Check if already a member
        var isMember = room.Members.Any(m => m.UserId == userId);
        if (!isMember)
        {
            var (success, message) = _roomService.JoinRoom(userId, user.Username, roomId, user.AvatarUrl);
            if (!success)
            {
                await Clients.Caller.SendAsync("RoomError", message);
                return;
            }
        }

        // Join the room's SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
        _connectionRooms[Context.ConnectionId] = roomId;

        // Refresh room data
        room = _roomService.GetRoom(roomId);
        if (room == null)
        {
            await Clients.Caller.SendAsync("RoomError", "Room not found after joining");
            return;
        }

        // Update online member count
        var member = room.Members.FirstOrDefault(m => m.UserId == userId);
        if (member != null)
        {
            member.IsOnline = true;
            room.OnlineMembers = room.Members.Count(m => m.IsOnline);
        }

        var roomDto = _roomService.MapToDto(room);
        await Clients.Caller.SendAsync("RoomJoined", roomDto);

        // Notify other members
        await Clients.OthersInGroup($"room_{roomId}").SendAsync("MemberJoined", new RoomMemberDto
        {
            UserId = userId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            IsOnline = true,
            JoinedAt = DateTime.UtcNow
        });

        // Send active streams in this room
        if (_activeStreams.TryGetValue(roomId, out var streams))
        {
            await Clients.Caller.SendAsync("ActiveStreams", streams.Values.ToList());
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
        _connectionRooms.TryRemove(Context.ConnectionId, out _);

        // Notify other members
        await Clients.OthersInGroup($"room_{roomId}").SendAsync("MemberLeft", userId);
    }

    public async Task UpdateRoom(string roomId, UpdateRoomRequest request)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var (success, message) = _roomService.UpdateRoom(userId, roomId, request);

        if (success)
        {
            var room = _roomService.GetRoom(roomId);
            if (room != null)
            {
                var roomDto = _roomService.MapToDto(room);
                await Clients.Group($"room_{roomId}").SendAsync("RoomUpdated", roomDto);
            }
        }
        else
        {
            await Clients.Caller.SendAsync("RoomError", message);
        }
    }

    // === Channel Management ===

    public async Task CreateChannel(string roomId, CreateChannelRequest request)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var (success, message, channel) = _roomService.CreateChannel(userId, roomId, request);

        if (success && channel != null)
        {
            var channelDto = new RoomChannelDto
            {
                Id = channel.Id,
                Name = channel.Name,
                Description = channel.Description,
                Type = channel.Type,
                Position = channel.Position,
                ParentId = channel.ParentId,
                IsPrivate = channel.IsPrivate,
                MaxUsers = channel.MaxUsers,
                Bitrate = channel.Bitrate,
                VideoEnabled = channel.VideoEnabled,
                ScreenShareEnabled = channel.ScreenShareEnabled
            };
            await Clients.Group($"room_{roomId}").SendAsync("ChannelCreated", channelDto);
        }
        else
        {
            await Clients.Caller.SendAsync("RoomError", message);
        }
    }

    // === Role Management ===

    public async Task CreateRole(string roomId, CreateRoleRequest request)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var (success, message, role) = _roomService.CreateRole(userId, roomId, request);

        if (success && role != null)
        {
            var roleDto = new RoomRoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Color = role.Color,
                Position = role.Position,
                Permissions = role.Permissions
            };
            await Clients.Group($"room_{roomId}").SendAsync("RoleCreated", roleDto);
        }
        else
        {
            await Clients.Caller.SendAsync("RoomError", message);
        }
    }

    public async Task AssignRole(string roomId, string targetUserId, string roleId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Not authenticated");
            return;
        }

        var (success, message) = _roomService.AssignRole(userId, roomId, targetUserId, roleId);

        if (success)
        {
            await Clients.Group($"room_{roomId}").SendAsync("RoleAssigned", targetUserId, roleId);
        }
        else
        {
            await Clients.Caller.SendAsync("RoomError", message);
        }
    }

    // === Streaming ===

    public async Task StartStream(string roomId, string channelId, StreamQualityRequestDto quality)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("StreamError", "Not authenticated");
            return;
        }

        var user = _authService.GetUserById(userId);
        var room = _roomService.GetRoom(roomId);

        if (user == null || room == null)
        {
            await Clients.Caller.SendAsync("StreamError", "User or room not found");
            return;
        }

        // Determine streaming tier based on quality
        var preset = StreamingQualityPreset.Presets.FirstOrDefault(p => p.Name == quality.PresetName)
                    ?? StreamingQualityPreset.Presets[1]; // Default to 720p

        if (!_roomService.CanStream(userId, room, preset.RequiredTier))
        {
            await Clients.Caller.SendAsync("StreamError", "Insufficient permissions for this quality level");
            return;
        }

        // Check concurrent stream limit
        var roomStreams = _activeStreams.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, StreamInfoDto>());
        if (roomStreams.Count >= room.MaxConcurrentStreams)
        {
            await Clients.Caller.SendAsync("StreamError", "Room has reached maximum concurrent streams");
            return;
        }

        var streamInfo = new StreamInfoDto
        {
            StreamerId = userId,
            StreamerUsername = user.Username,
            RoomId = roomId,
            ChannelId = channelId,
            Type = StreamType.Camera,
            Width = quality.CustomWidth ?? preset.Width,
            Height = quality.CustomHeight ?? preset.Height,
            FrameRate = quality.CustomFrameRate ?? preset.FrameRate,
            BitrateKbps = quality.CustomBitrate ?? preset.BitrateKbps,
            StartedAt = DateTime.UtcNow
        };

        roomStreams[userId] = streamInfo;

        await Clients.Group($"room_{roomId}").SendAsync("StreamStarted", streamInfo);
        await Clients.Caller.SendAsync("StreamReady", streamInfo);
    }

    public async Task StopStream(string roomId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_activeStreams.TryGetValue(roomId, out var streams))
        {
            if (streams.TryRemove(userId, out _))
            {
                await Clients.Group($"room_{roomId}").SendAsync("StreamStopped", userId);
            }
        }
    }

    public async Task SendStreamFrame(string roomId, byte[] frameData, int width, int height)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        // Check bandwidth limit
        if (!CheckBandwidthLimit(userId, frameData.Length))
        {
            // Silently drop frame if over limit
            return;
        }

        // Broadcast to room (excluding sender for efficiency, they don't need their own stream back)
        await Clients.OthersInGroup($"room_{roomId}").SendAsync("ReceiveStreamFrame", userId, frameData, width, height);

        // Update stream viewer count periodically would go here
    }

    public async Task RequestStreamQuality(string roomId, string streamerId, string qualityPreset)
    {
        // Viewer requests different quality from streamer
        // This enables adaptive bitrate streaming
        await Clients.User(streamerId).SendAsync("QualityChangeRequested", Context.ConnectionId, qualityPreset);
    }

    // === Marketplace Integration ===

    public async Task ListProductInRoom(string roomId, string productId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("MarketplaceError", "Not authenticated");
            return;
        }

        var room = _roomService.GetRoom(roomId);
        if (room == null || !room.AllowMarketplace)
        {
            await Clients.Caller.SendAsync("MarketplaceError", "Marketplace not available in this room");
            return;
        }

        if (!_roomService.HasPermission(userId, room, RoomPermissions.SellProducts))
        {
            await Clients.Caller.SendAsync("MarketplaceError", "No permission to sell in this room");
            return;
        }

        // Validate product exists and belongs to user
        var product = _productService.GetProduct(productId);
        if (product == null)
        {
            await Clients.Caller.SendAsync("MarketplaceError", "Product not found");
            return;
        }

        if (product.SellerId != userId)
        {
            await Clients.Caller.SendAsync("MarketplaceError", "You can only list your own products");
            return;
        }

        // Notify room members about the product listing
        await Clients.Group($"room_{roomId}").SendAsync("ProductListed", product, userId);
    }

    public async Task GetPublicRooms(int skip = 0, int take = 50)
    {
        var rooms = _roomService.GetPublicRooms(skip, take);
        var roomDtos = rooms.Select(r => _roomService.MapToDto(r)).ToList();
        await Clients.Caller.SendAsync("PublicRooms", roomDtos);
    }

    // === Helper Methods ===

    private bool CheckBandwidthLimit(string userId, long bytes)
    {
        var now = DateTime.UtcNow;

        if (_bandwidthUsage.TryGetValue(userId, out var usage))
        {
            // Reset if more than 1 second has passed
            if ((now - usage.lastReset).TotalSeconds >= 1)
            {
                _bandwidthUsage[userId] = (bytes, now);
                return true;
            }

            // Check if over limit
            if (usage.bytesUsed + bytes > MaxUploadBytesPerSecond)
            {
                return false;
            }

            _bandwidthUsage[userId] = (usage.bytesUsed + bytes, usage.lastReset);
        }
        else
        {
            _bandwidthUsage[userId] = (bytes, now);
        }

        return true;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _connectionTimestamps.TryRemove(connectionId, out _);

        if (_connectionUsers.TryRemove(connectionId, out var userId))
        {
            // Remove this connection from user's connection list
            bool userHasNoMoreConnections = false;
            if (_userConnections.TryGetValue(userId, out var connIds))
            {
                lock (connIds)
                {
                    connIds.Remove(connectionId);
                    if (connIds.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                        userHasNoMoreConnections = true;
                    }
                }
            }

            // Only clean up streams and notify offline if no more connections
            if (userHasNoMoreConnections)
            {
                // Stop any active streams and notify room members
                foreach (var (roomId, roomStreams) in _activeStreams)
                {
                    if (roomStreams.TryRemove(userId, out var streamInfo))
                    {
                        // Notify room members that stream stopped
                        await Clients.Group($"room_{roomId}").SendAsync("StreamStopped", new
                        {
                            UserId = userId,
                            StreamType = streamInfo.StreamType,
                            RoomId = roomId
                        });
                    }
                }
            }

            // Leave room for this specific connection
            if (_connectionRooms.TryRemove(connectionId, out var roomId))
            {
                // Only notify offline if no other connections in this room
                if (userHasNoMoreConnections)
                {
                    await Clients.OthersInGroup($"room_{roomId}").SendAsync("MemberOffline", userId);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
