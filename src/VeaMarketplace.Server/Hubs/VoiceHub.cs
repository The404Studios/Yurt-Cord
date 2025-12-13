using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Hubs;

public class VoiceHub : Hub
{
    private readonly VoiceCallService? _callService;
    private readonly AuthService? _authService;
    private static readonly ConcurrentDictionary<string, VoiceChannelState> _voiceChannels = new();
    private static readonly ConcurrentDictionary<string, VoiceUserState> _voiceUsers = new();
    private static readonly ConcurrentDictionary<string, string> _userConnections = new(); // userId -> connectionId
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, string> _activeCalls = new(); // callId -> (caller|recipient connectionId pair)

    // Voice Room system
    private static readonly ConcurrentDictionary<string, VoiceRoom> _voiceRooms = new();

    // Bandwidth tracking for rate limiting (userId -> (bytesUsed, lastResetTime))
    private static readonly ConcurrentDictionary<string, (long bytesUsed, DateTime lastReset)> _bandwidthUsage = new();

    // Stream quality preferences per viewer (viewerConnectionId -> preferred quality)
    private static readonly ConcurrentDictionary<string, string> _viewerQualityPrefs = new();

    // Constants for high-bandwidth streaming
    private const long MaxUploadBytesPerSecond = 30L * 1024 * 1024; // 30 MB/s upload per user
    private const long MaxDownloadBytesPerSecond = 50L * 1024 * 1024; // 50 MB/s download per user
    private const int MaxConcurrentStreamsPerChannel = 10;

    public VoiceHub(VoiceCallService? callService = null, AuthService? authService = null)
    {
        _callService = callService;
        _authService = authService;
    }

    public async Task JoinVoiceChannel(string channelId, string userId, string username, string avatarUrl)
    {
        var userState = new VoiceUserState
        {
            ConnectionId = Context.ConnectionId,
            UserId = userId,
            Username = username,
            AvatarUrl = avatarUrl,
            ChannelId = channelId,
            IsMuted = false,
            IsDeafened = false,
            IsSpeaking = false
        };

        _voiceUsers[Context.ConnectionId] = userState;

        var channel = _voiceChannels.GetOrAdd(channelId, _ => new VoiceChannelState { ChannelId = channelId });
        channel.Users[Context.ConnectionId] = userState;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"voice_{channelId}");

        // Notify others in channel
        await Clients.OthersInGroup($"voice_{channelId}").SendAsync("UserJoinedVoice", userState);

        // Send list of users in channel to joiner
        await Clients.Caller.SendAsync("VoiceChannelUsers", channel.Users.Values.ToList());
    }

    public async Task LeaveVoiceChannel()
    {
        if (_voiceUsers.TryRemove(Context.ConnectionId, out var userState))
        {
            if (_voiceChannels.TryGetValue(userState.ChannelId, out var channel))
            {
                channel.Users.TryRemove(Context.ConnectionId, out _);

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice_{userState.ChannelId}");
                await Clients.Group($"voice_{userState.ChannelId}").SendAsync("UserLeftVoice", userState);

                // Clean up empty channels to prevent memory leak
                if (channel.Users.IsEmpty)
                {
                    _voiceChannels.TryRemove(userState.ChannelId, out _);
                }
            }
        }
    }

    public async Task UpdateVoiceState(bool isMuted, bool isDeafened)
    {
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            userState.IsMuted = isMuted;
            userState.IsDeafened = isDeafened;

            await Clients.Group($"voice_{userState.ChannelId}").SendAsync("VoiceStateUpdated", Context.ConnectionId, isMuted, isDeafened);
        }
    }

    public async Task UpdateSpeakingState(bool isSpeaking, double audioLevel)
    {
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            userState.IsSpeaking = isSpeaking;
            userState.AudioLevel = audioLevel;

            await Clients.OthersInGroup($"voice_{userState.ChannelId}")
                .SendAsync("UserSpeaking", Context.ConnectionId, userState.Username, isSpeaking, audioLevel);
        }
    }

    // Send audio to all other users in the voice channel
    public async Task SendAudio(byte[] audioData)
    {
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            // Don't send if user is muted
            if (userState.IsMuted) return;

            // Broadcast audio to all OTHER users in the channel (not the sender)
            await Clients.OthersInGroup($"voice_{userState.ChannelId}")
                .SendAsync("ReceiveAudio", Context.ConnectionId, audioData);
        }
    }

    // Admin: Disconnect a user from voice
    public async Task DisconnectUser(string targetConnectionId)
    {
        // Verify caller has permission
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var callerId))
        {
            await Clients.Caller.SendAsync("VoiceError", "Not authenticated");
            return;
        }

        // Check if caller is admin/moderator (or self-disconnect)
        var callerState = _voiceUsers.Values.FirstOrDefault(u => u.UserId == callerId);
        var targetState = _voiceUsers.GetValueOrDefault(targetConnectionId);

        // Allow self-disconnect or if we have permission check via AuthService
        var isSelfDisconnect = targetState?.UserId == callerId;

        if (!isSelfDisconnect && _authService != null)
        {
            var caller = _authService.GetUserById(callerId);
            if (caller == null || (caller.Role != UserRole.Admin && caller.Role != UserRole.Moderator))
            {
                await Clients.Caller.SendAsync("VoiceError", "Insufficient permissions");
                return;
            }
        }

        if (_voiceUsers.TryGetValue(targetConnectionId, out var targetUser))
        {
            // Notify the user they've been disconnected
            if (!isSelfDisconnect)
            {
                await Clients.Client(targetConnectionId).SendAsync("DisconnectedByAdmin", "You have been disconnected by an administrator");
            }

            // Remove them from the channel
            if (_voiceChannels.TryGetValue(targetUser.ChannelId, out var channel))
            {
                channel.Users.TryRemove(targetConnectionId, out _);
                await Groups.RemoveFromGroupAsync(targetConnectionId, $"voice_{targetUser.ChannelId}");
                await Clients.Group($"voice_{targetUser.ChannelId}").SendAsync("UserLeftVoice", targetUser);
            }
            _voiceUsers.TryRemove(targetConnectionId, out _);
        }
    }

    // Admin: Move a user to a different channel
    public async Task MoveUserToChannel(string targetConnectionId, string targetChannelId)
    {
        // Verify caller has permission
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var callerId))
        {
            await Clients.Caller.SendAsync("VoiceError", "Not authenticated");
            return;
        }

        if (_authService != null)
        {
            var caller = _authService.GetUserById(callerId);
            if (caller == null || (caller.Role != UserRole.Admin && caller.Role != UserRole.Moderator))
            {
                await Clients.Caller.SendAsync("VoiceError", "Insufficient permissions to move users");
                return;
            }
        }

        if (_voiceUsers.TryGetValue(targetConnectionId, out var targetUser))
        {
            var oldChannelId = targetUser.ChannelId;

            // Remove from old channel
            if (_voiceChannels.TryGetValue(oldChannelId, out var oldChannel))
            {
                oldChannel.Users.TryRemove(targetConnectionId, out _);
                await Groups.RemoveFromGroupAsync(targetConnectionId, $"voice_{oldChannelId}");
                await Clients.Group($"voice_{oldChannelId}").SendAsync("UserLeftVoice", targetUser);
            }

            // Add to new channel
            targetUser.ChannelId = targetChannelId;
            var newChannel = _voiceChannels.GetOrAdd(targetChannelId, _ => new VoiceChannelState { ChannelId = targetChannelId });
            newChannel.Users[targetConnectionId] = targetUser;
            await Groups.AddToGroupAsync(targetConnectionId, $"voice_{targetChannelId}");

            // Notify NEW channel users about the join (not old channel - that was the bug)
            await Clients.OthersInGroup($"voice_{targetChannelId}").SendAsync("UserJoinedVoice", targetUser);

            // Notify the moved user
            await Clients.Client(targetConnectionId).SendAsync("MovedToChannel", targetChannelId, "Administrator");

            // Send the user the list of users in new channel
            await Clients.Client(targetConnectionId).SendAsync("VoiceChannelUsers", newChannel.Users.Values.ToList());
        }
    }

    // Admin: Kick user (just disconnects with a kick message)
    public async Task KickUser(string userId, string reason)
    {
        // Verify caller has permission
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var callerId))
        {
            await Clients.Caller.SendAsync("VoiceError", "Not authenticated");
            return;
        }

        if (_authService != null)
        {
            var caller = _authService.GetUserById(callerId);
            if (caller == null || (caller.Role != UserRole.Admin && caller.Role != UserRole.Moderator))
            {
                await Clients.Caller.SendAsync("VoiceError", "Insufficient permissions to kick users");
                return;
            }
        }

        // Find connection ID by user ID
        var targetEntry = _voiceUsers.FirstOrDefault(kvp => kvp.Value.UserId == userId);
        if (targetEntry.Value != null)
        {
            await Clients.Client(targetEntry.Key).SendAsync("DisconnectedByAdmin", $"You have been kicked: {reason}");
            await DisconnectUser(targetEntry.Key);
        }
    }

    // Admin: Ban user from voice channels
    public async Task BanUser(string userId, string reason, double? durationMinutes)
    {
        // Verify caller has permission
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var callerId))
        {
            await Clients.Caller.SendAsync("VoiceError", "Not authenticated");
            return;
        }

        if (_authService != null)
        {
            var caller = _authService.GetUserById(callerId);
            if (caller == null || (caller.Role != UserRole.Admin && caller.Role != UserRole.Moderator))
            {
                await Clients.Caller.SendAsync("VoiceError", "Insufficient permissions to ban users");
                return;
            }
        }

        // Find and kick the user first
        var targetEntry = _voiceUsers.FirstOrDefault(kvp => kvp.Value.UserId == userId);
        if (targetEntry.Value != null)
        {
            var banDuration = durationMinutes.HasValue ? $" for {durationMinutes} minutes" : " permanently";
            await Clients.Client(targetEntry.Key).SendAsync("DisconnectedByAdmin", $"You have been banned from voice{banDuration}: {reason}");
            await DisconnectUser(targetEntry.Key);
        }
        // Note: Actual ban persistence would require a database - this is just the immediate disconnect
    }

    // Screen Sharing
    private static readonly ConcurrentDictionary<string, ScreenShareState> _screenSharers = new(); // connectionId -> state
    private static readonly ConcurrentDictionary<string, HashSet<string>> _screenShareViewers = new(); // sharerConnectionId -> viewer connectionIds

    public async Task StartScreenShare()
    {
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            var shareState = new ScreenShareState
            {
                SharerConnectionId = Context.ConnectionId,
                SharerUsername = userState.Username,
                ChannelId = userState.ChannelId,
                StartedAt = DateTime.UtcNow
            };

            _screenSharers[Context.ConnectionId] = shareState;
            _screenShareViewers[Context.ConnectionId] = new HashSet<string>();
            userState.IsScreenSharing = true;

            // Update in channel state too
            if (_voiceChannels.TryGetValue(userState.ChannelId, out var channel))
            {
                if (channel.Users.TryGetValue(Context.ConnectionId, out var channelUser))
                {
                    channelUser.IsScreenSharing = true;
                }
            }

            // Notify all users in the channel (including self for UI update)
            await Clients.Group($"voice_{userState.ChannelId}")
                .SendAsync("UserScreenShareChanged", Context.ConnectionId, true);

            // Send detailed info about the new screen share
            await Clients.Group($"voice_{userState.ChannelId}")
                .SendAsync("ScreenShareStarted", Context.ConnectionId, userState.Username, userState.ChannelId);
        }
    }

    public async Task StopScreenShare()
    {
        _screenSharers.TryRemove(Context.ConnectionId, out _);
        _screenShareViewers.TryRemove(Context.ConnectionId, out _);

        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            userState.IsScreenSharing = false;

            // Update in channel state too
            if (_voiceChannels.TryGetValue(userState.ChannelId, out var channel))
            {
                if (channel.Users.TryGetValue(Context.ConnectionId, out var channelUser))
                {
                    channelUser.IsScreenSharing = false;
                }
            }

            await Clients.Group($"voice_{userState.ChannelId}")
                .SendAsync("UserScreenShareChanged", Context.ConnectionId, false);

            await Clients.Group($"voice_{userState.ChannelId}")
                .SendAsync("ScreenShareStopped", Context.ConnectionId);
        }
    }

    public async Task SendScreenFrame(byte[] frameData, int width, int height)
    {
        if (!_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
            return;

        // Check bandwidth limit (30 MB/s upload)
        if (!CheckBandwidthLimit(Context.ConnectionId, frameData.Length))
        {
            // Over bandwidth limit - drop frame silently
            if (_screenSharers.TryGetValue(Context.ConnectionId, out var ss))
                ss.FramesDropped++;
            return;
        }

        // Update frame stats
        if (_screenSharers.TryGetValue(Context.ConnectionId, out var shareState))
        {
            shareState.FramesSent++;
            shareState.LastWidth = width;
            shareState.LastHeight = height;
            shareState.LastFrameTime = DateTime.UtcNow;
            shareState.BytesSent += frameData.Length;
        }

        // Send screen frame ONLY to OTHER users in the channel (not back to sender)
        // This prevents echo and reduces bandwidth for the sharer
        await Clients.OthersInGroup($"voice_{userState.ChannelId}")
            .SendAsync("ReceiveScreenFrame", Context.ConnectionId, frameData, width, height);
    }

    // Bandwidth check helper
    private bool CheckBandwidthLimit(string connectionId, long bytes)
    {
        var now = DateTime.UtcNow;

        if (_bandwidthUsage.TryGetValue(connectionId, out var usage))
        {
            // Reset counter every second
            if ((now - usage.lastReset).TotalSeconds >= 1)
            {
                _bandwidthUsage[connectionId] = (bytes, now);
                return true;
            }

            // Check if over limit
            if (usage.bytesUsed + bytes > MaxUploadBytesPerSecond)
                return false;

            _bandwidthUsage[connectionId] = (usage.bytesUsed + bytes, usage.lastReset);
        }
        else
        {
            _bandwidthUsage[connectionId] = (bytes, now);
        }

        return true;
    }

    // Viewer requests a quality level from streamer
    public async Task RequestStreamQuality(string streamerConnectionId, string qualityPreset)
    {
        _viewerQualityPrefs[Context.ConnectionId] = qualityPreset;

        // Notify streamer of quality request (they can choose to honor it)
        await Clients.Client(streamerConnectionId)
            .SendAsync("QualityChangeRequested", Context.ConnectionId, qualityPreset);
    }

    // Get available quality presets
    public Task<string[]> GetAvailableQualities()
    {
        return Task.FromResult(new[]
        {
            "480p", "720p", "720p60", "1080p", "1080p60", "1440p", "1440p60", "4K"
        });
    }

    // Viewer joins a screen share
    public async Task JoinScreenShare(string sharerConnectionId)
    {
        if (_screenShareViewers.TryGetValue(sharerConnectionId, out var viewers))
        {
            lock (viewers)
            {
                viewers.Add(Context.ConnectionId);
            }

            // Notify the sharer of viewer count update
            await Clients.Client(sharerConnectionId)
                .SendAsync("ViewerCountUpdated", viewers.Count);

            // Notify the viewer they joined successfully
            if (_screenSharers.TryGetValue(sharerConnectionId, out var shareState))
            {
                await Clients.Caller.SendAsync("JoinedScreenShare", sharerConnectionId, shareState.SharerUsername);
            }
        }
    }

    // Viewer leaves a screen share
    public async Task LeaveScreenShare(string sharerConnectionId)
    {
        if (_screenShareViewers.TryGetValue(sharerConnectionId, out var viewers))
        {
            lock (viewers)
            {
                viewers.Remove(Context.ConnectionId);
            }

            // Notify the sharer of viewer count update
            await Clients.Client(sharerConnectionId)
                .SendAsync("ViewerCountUpdated", viewers.Count);
        }
    }

    // Get list of active screen shares in channel
    public async Task GetActiveScreenShares()
    {
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            var activeShares = _screenSharers.Values
                .Where(s => s.ChannelId == userState.ChannelId)
                .Select(s => new
                {
                    s.SharerConnectionId,
                    s.SharerUsername,
                    s.StartedAt,
                    ViewerCount = _screenShareViewers.TryGetValue(s.SharerConnectionId, out var v) ? v.Count : 0,
                    s.LastWidth,
                    s.LastHeight
                })
                .ToList();

            await Clients.Caller.SendAsync("ActiveScreenShares", activeShares);
        }
    }

    // Request specific quality from a sharer (for future adaptive streaming)
    public async Task RequestScreenQuality(string sharerConnectionId, string quality)
    {
        if (_screenSharers.ContainsKey(sharerConnectionId))
        {
            await Clients.Client(sharerConnectionId)
                .SendAsync("QualityRequested", Context.ConnectionId, quality);
        }
    }

    // === Voice Room Methods ===

    public async Task CreateVoiceRoom(CreateVoiceRoomDto dto, string userId, string username, string avatarUrl)
    {
        var room = new VoiceRoom
        {
            Name = dto.Name,
            Description = dto.Description,
            HostId = userId,
            HostUsername = username,
            HostAvatarUrl = avatarUrl,
            IsPublic = dto.IsPublic,
            PasswordHash = !string.IsNullOrEmpty(dto.Password)
                ? BCrypt.Net.BCrypt.HashPassword(dto.Password)
                : null,
            MaxParticipants = Math.Clamp(dto.MaxParticipants, 2, 50),
            Category = dto.Category,
            AllowScreenShare = dto.AllowScreenShare
        };

        // Add host as first participant
        var hostParticipant = new VoiceRoomParticipant
        {
            UserId = userId,
            ConnectionId = Context.ConnectionId,
            Username = username,
            AvatarUrl = avatarUrl,
            IsHost = true
        };
        room.Participants[userId] = hostParticipant;

        _voiceRooms[room.Id] = room;

        // Join the SignalR group for this room
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{room.Id}");

        // Also join the voice channel for audio
        await JoinVoiceChannel($"room_{room.Id}", userId, username, avatarUrl);

        await Clients.Caller.SendAsync("VoiceRoomCreated", room.ToDto());

        // Notify all clients that a new public room is available
        if (room.IsPublic)
        {
            await Clients.All.SendAsync("VoiceRoomAdded", room.ToDto());
        }
    }

    public async Task JoinVoiceRoom(string roomId, string userId, string username, string avatarUrl, string? password = null)
    {
        if (!_voiceRooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "Room not found");
            return;
        }

        if (!room.IsActive)
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "Room is no longer active");
            return;
        }

        if (room.Participants.Count >= room.MaxParticipants)
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "Room is full");
            return;
        }

        // Check password if required
        if (room.PasswordHash != null)
        {
            if (string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, room.PasswordHash))
            {
                await Clients.Caller.SendAsync("VoiceRoomError", "Incorrect password");
                return;
            }
        }

        var participant = new VoiceRoomParticipant
        {
            UserId = userId,
            ConnectionId = Context.ConnectionId,
            Username = username,
            AvatarUrl = avatarUrl,
            IsHost = false,
            IsModerator = room.Moderators.Contains(userId)
        };
        room.Participants[userId] = participant;

        // Join the SignalR group for this room
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");

        // Also join the voice channel for audio
        await JoinVoiceChannel($"room_{roomId}", userId, username, avatarUrl);

        // Notify room members
        await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomParticipantJoined", participant.ToDto());

        // Send room info to new participant
        await Clients.Caller.SendAsync("VoiceRoomJoined", room.ToDto());

        // Update public room list
        if (room.IsPublic)
        {
            await Clients.All.SendAsync("VoiceRoomUpdated", room.ToDto());
        }
    }

    public async Task LeaveVoiceRoom(string roomId)
    {
        if (!_voiceRooms.TryGetValue(roomId, out var room))
            return;

        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
        {
            // Try to find user by connection ID in room participants
            var participant = room.Participants.Values.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (participant == null) return;
            userId = participant.UserId;
        }

        if (room.Participants.TryRemove(userId, out var removedParticipant))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
            await LeaveVoiceChannel();

            await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomParticipantLeft", removedParticipant.ToDto());

            // If host left, transfer to next person or close room
            if (removedParticipant.IsHost)
            {
                if (room.Participants.Count > 0)
                {
                    // Transfer host to first participant
                    var newHost = room.Participants.Values.FirstOrDefault();
                    if (newHost != null)
                    {
                        newHost.IsHost = true;
                        room.HostId = newHost.UserId;
                        room.HostUsername = newHost.Username;
                        room.HostAvatarUrl = newHost.AvatarUrl;

                        await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomHostChanged", newHost.ToDto());
                    }
                }
                else
                {
                    // Close room
                    room.IsActive = false;
                    _voiceRooms.TryRemove(roomId, out _);

                    if (room.IsPublic)
                    {
                        await Clients.All.SendAsync("VoiceRoomRemoved", roomId);
                    }
                }
            }

            // Update public room list
            if (room.IsActive && room.IsPublic)
            {
                await Clients.All.SendAsync("VoiceRoomUpdated", room.ToDto());
            }
        }
    }

    public async Task GetPublicVoiceRooms(VoiceRoomCategory? category = null, string? query = null, int page = 1, int pageSize = 20)
    {
        var rooms = _voiceRooms.Values
            .Where(r => r.IsActive && r.IsPublic)
            .Where(r => !category.HasValue || r.Category == category.Value)
            .Where(r => string.IsNullOrEmpty(query) ||
                        r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Participants.Count)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.ToDto())
            .ToList();

        var totalCount = _voiceRooms.Values.Count(r => r.IsActive && r.IsPublic);

        await Clients.Caller.SendAsync("PublicVoiceRooms", rooms, totalCount, page, pageSize);
    }

    public async Task CloseVoiceRoom(string roomId)
    {
        if (!_voiceRooms.TryGetValue(roomId, out var room))
            return;

        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId) || room.HostId != userId)
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "Only the host can close the room");
            return;
        }

        room.IsActive = false;

        // Notify all participants
        await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomClosed", roomId, "Host closed the room");

        // Remove all participants from the group
        foreach (var participant in room.Participants.Values)
        {
            await Groups.RemoveFromGroupAsync(participant.ConnectionId, $"room_{roomId}");
        }

        _voiceRooms.TryRemove(roomId, out _);

        if (room.IsPublic)
        {
            await Clients.All.SendAsync("VoiceRoomRemoved", roomId);
        }
    }

    public async Task KickFromVoiceRoom(string roomId, string targetUserId, string? reason = null)
    {
        if (!_voiceRooms.TryGetValue(roomId, out var room))
            return;

        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        // Check if caller is host or moderator
        var callerParticipant = room.Participants.Values.FirstOrDefault(p => p.UserId == userId);
        if (callerParticipant == null || (!callerParticipant.IsHost && !callerParticipant.IsModerator))
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "You don't have permission to kick users");
            return;
        }

        if (room.Participants.TryRemove(targetUserId, out var kickedParticipant))
        {
            await Groups.RemoveFromGroupAsync(kickedParticipant.ConnectionId, $"room_{roomId}");

            await Clients.Client(kickedParticipant.ConnectionId)
                .SendAsync("VoiceRoomKicked", roomId, reason ?? "You were kicked from the room");

            await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomParticipantLeft", kickedParticipant.ToDto());

            if (room.IsPublic)
            {
                await Clients.All.SendAsync("VoiceRoomUpdated", room.ToDto());
            }
        }
    }

    public async Task PromoteToModerator(string roomId, string targetUserId)
    {
        if (!_voiceRooms.TryGetValue(roomId, out var room))
            return;

        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId) || room.HostId != userId)
        {
            await Clients.Caller.SendAsync("VoiceRoomError", "Only the host can promote moderators");
            return;
        }

        if (room.Participants.TryGetValue(targetUserId, out var participant))
        {
            participant.IsModerator = true;
            room.Moderators.Add(targetUserId);

            await Clients.Group($"room_{roomId}").SendAsync("VoiceRoomModeratorAdded", participant.ToDto());
        }
    }

    // === Nudge System ===

    public async Task SendNudge(string targetUserId, string? message = null)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var senderId))
        {
            await Clients.Caller.SendAsync("NudgeError", "You must be authenticated to send nudges");
            return;
        }

        // Get sender info from voice users or connection
        var senderInfo = _voiceUsers.Values.FirstOrDefault(u => u.UserId == senderId);
        if (senderInfo == null)
        {
            await Clients.Caller.SendAsync("NudgeError", "Could not find your user information");
            return;
        }

        // Check if target is online
        if (!_userConnections.TryGetValue(targetUserId, out var targetConnectionId))
        {
            await Clients.Caller.SendAsync("NudgeError", "User is not online");
            return;
        }

        var nudge = new NudgeDto
        {
            FromUserId = senderId,
            FromUsername = senderInfo.Username,
            FromAvatarUrl = senderInfo.AvatarUrl,
            ToUserId = targetUserId,
            Timestamp = DateTime.UtcNow,
            Message = message
        };

        await Clients.Client(targetConnectionId).SendAsync("NudgeReceived", nudge);
        await Clients.Caller.SendAsync("NudgeSent", nudge);
    }

    // WebRTC Signaling
    public async Task SendOffer(string targetConnectionId, string offer)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, offer);
    }

    public async Task SendAnswer(string targetConnectionId, string answer)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, answer);
    }

    public async Task SendIceCandidate(string targetConnectionId, string candidate)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up screen share FIRST (before leaving voice channel)
        // This prevents async streaming issues when the connection is terminated
        if (_screenSharers.TryRemove(Context.ConnectionId, out var shareState))
        {
            _screenShareViewers.TryRemove(Context.ConnectionId, out _);

            // Notify channel that screen share stopped
            try
            {
                await Clients.Group($"voice_{shareState.ChannelId}")
                    .SendAsync("UserScreenShareChanged", Context.ConnectionId, false);
                await Clients.Group($"voice_{shareState.ChannelId}")
                    .SendAsync("ScreenShareStopped", Context.ConnectionId);
            }
            catch
            {
                // Ignore errors during disconnect cleanup
            }
        }

        // Also remove this user from any viewer lists they're in
        foreach (var kvp in _screenShareViewers)
        {
            lock (kvp.Value)
            {
                kvp.Value.Remove(Context.ConnectionId);
            }
        }

        // Clean up viewer quality preferences for this connection
        _viewerQualityPrefs.TryRemove(Context.ConnectionId, out _);

        // Now leave voice channel
        try
        {
            await LeaveVoiceChannel();
        }
        catch
        {
            // Ignore errors during disconnect cleanup
            // Force cleanup the voice user state
            if (_voiceUsers.TryRemove(Context.ConnectionId, out var userState))
            {
                if (_voiceChannels.TryGetValue(userState.ChannelId, out var channel))
                {
                    channel.Users.TryRemove(Context.ConnectionId, out _);
                }
            }
        }

        // Clean up voice room participation
        // Find and remove this user from any voice rooms they're in
        foreach (var room in _voiceRooms.Values.ToArray())
        {
            var participant = room.Participants.Values.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (participant != null && room.Participants.TryRemove(participant.UserId, out var removedParticipant))
            {
                try
                {
                    await Clients.Group($"room_{room.Id}").SendAsync("VoiceRoomParticipantLeft", removedParticipant.ToDto());

                    // If host left, transfer to next person or close room
                    if (removedParticipant.IsHost)
                    {
                        if (room.Participants.Count > 0)
                        {
                            var newHost = room.Participants.Values.FirstOrDefault();
                            if (newHost != null)
                            {
                                newHost.IsHost = true;
                                room.HostId = newHost.UserId;
                                room.HostUsername = newHost.Username;
                                room.HostAvatarUrl = newHost.AvatarUrl;
                                await Clients.Group($"room_{room.Id}").SendAsync("VoiceRoomHostChanged", newHost.ToDto());
                            }
                        }
                        else
                        {
                            room.IsActive = false;
                            _voiceRooms.TryRemove(room.Id, out _);
                            if (room.IsPublic)
                            {
                                await Clients.All.SendAsync("VoiceRoomRemoved", room.Id);
                            }
                        }
                    }
                    else if (room.IsActive && room.IsPublic)
                    {
                        await Clients.All.SendAsync("VoiceRoomUpdated", room.ToDto());
                    }
                }
                catch
                {
                    // Ignore errors during disconnect cleanup
                }
            }
        }

        // Clean up bandwidth tracking for this connection (keyed by connectionId)
        _bandwidthUsage.TryRemove(Context.ConnectionId, out _);

        // Handle call disconnection
        if (_connectionUsers.TryRemove(Context.ConnectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);

            // End any active calls
            if (_callService != null)
            {
                try
                {
                    var activeCall = _callService.GetActiveCall(userId);
                    if (activeCall != null)
                    {
                        var (_, _, call) = _callService.EndCall(activeCall.Id, userId);
                        if (call != null)
                        {
                            var otherUserId = call.CallerId == userId ? call.RecipientId : call.CallerId;
                            if (_userConnections.TryGetValue(otherUserId, out var otherConnId))
                            {
                                await Clients.Client(otherConnId).SendAsync("CallEnded", call.Id, "User disconnected");
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore call cleanup errors during disconnect
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // === Call Methods ===

    public async Task AuthenticateForCalls(string token)
    {
        if (_authService == null) return;

        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("CallAuthFailed", "Invalid token");
            return;
        }

        _userConnections[user.Id] = Context.ConnectionId;
        _connectionUsers[Context.ConnectionId] = user.Id;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");
        await Clients.Caller.SendAsync("CallAuthSuccess");
    }

    public async Task StartCall(string recipientId)
    {
        if (_callService == null || !_connectionUsers.TryGetValue(Context.ConnectionId, out var callerId))
            return;

        var (success, message, call) = _callService.StartCall(callerId, recipientId);

        if (success && call != null)
        {
            await Clients.Caller.SendAsync("CallStarted", new VoiceCallDto
            {
                Id = call.Id,
                CallerId = call.CallerId,
                CallerUsername = call.CallerUsername,
                CallerAvatarUrl = call.CallerAvatarUrl,
                RecipientId = call.RecipientId,
                RecipientUsername = call.RecipientUsername,
                RecipientAvatarUrl = call.RecipientAvatarUrl,
                Status = call.Status,
                StartedAt = call.StartedAt
            });

            // Notify recipient
            if (_userConnections.TryGetValue(recipientId, out var recipientConnId))
            {
                await Clients.Client(recipientConnId).SendAsync("IncomingCall", new VoiceCallDto
                {
                    Id = call.Id,
                    CallerId = call.CallerId,
                    CallerUsername = call.CallerUsername,
                    CallerAvatarUrl = call.CallerAvatarUrl,
                    RecipientId = call.RecipientId,
                    RecipientUsername = call.RecipientUsername,
                    RecipientAvatarUrl = call.RecipientAvatarUrl,
                    Status = call.Status,
                    StartedAt = call.StartedAt
                });
            }
            else
            {
                // Recipient not online
                _callService.EndCall(call.Id, callerId);
                await Clients.Caller.SendAsync("CallFailed", "User is not online");
            }
        }
        else
        {
            await Clients.Caller.SendAsync("CallFailed", message);
        }
    }

    public async Task AnswerCall(string callId, bool accept)
    {
        if (_callService == null || !_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message, call) = _callService.AnswerCall(callId, userId, accept);

        if (success && call != null)
        {
            var dto = new VoiceCallDto
            {
                Id = call.Id,
                CallerId = call.CallerId,
                CallerUsername = call.CallerUsername,
                CallerAvatarUrl = call.CallerAvatarUrl,
                RecipientId = call.RecipientId,
                RecipientUsername = call.RecipientUsername,
                RecipientAvatarUrl = call.RecipientAvatarUrl,
                Status = call.Status,
                StartedAt = call.StartedAt,
                AnsweredAt = call.AnsweredAt
            };

            if (accept)
            {
                await Clients.Caller.SendAsync("CallAnswered", dto);

                if (_userConnections.TryGetValue(call.CallerId, out var callerConnId))
                {
                    await Clients.Client(callerConnId).SendAsync("CallAnswered", dto);
                }
            }
            else
            {
                await Clients.Caller.SendAsync("CallDeclined", dto);

                if (_userConnections.TryGetValue(call.CallerId, out var callerConnId))
                {
                    await Clients.Client(callerConnId).SendAsync("CallDeclined", dto);
                }
            }
        }
        else
        {
            await Clients.Caller.SendAsync("CallError", message);
        }
    }

    public async Task EndCall(string callId)
    {
        if (_callService == null || !_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var (success, message, call) = _callService.EndCall(callId, userId);

        if (success && call != null)
        {
            var dto = new VoiceCallDto
            {
                Id = call.Id,
                CallerId = call.CallerId,
                CallerUsername = call.CallerUsername,
                CallerAvatarUrl = call.CallerAvatarUrl,
                RecipientId = call.RecipientId,
                RecipientUsername = call.RecipientUsername,
                RecipientAvatarUrl = call.RecipientAvatarUrl,
                Status = call.Status,
                StartedAt = call.StartedAt,
                AnsweredAt = call.AnsweredAt,
                Duration = call.Duration
            };

            await Clients.Caller.SendAsync("CallEnded", dto.Id, "Call ended");

            var otherUserId = call.CallerId == userId ? call.RecipientId : call.CallerId;
            if (_userConnections.TryGetValue(otherUserId, out var otherConnId))
            {
                await Clients.Client(otherConnId).SendAsync("CallEnded", dto.Id, "Call ended");
            }
        }
    }

    // Send audio data during a call
    public async Task SendCallAudio(string callId, byte[] audioData)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_callService != null)
        {
            var activeCall = _callService.GetActiveCall(userId);
            if (activeCall != null && activeCall.Id == callId && activeCall.Status == VoiceCallStatus.InProgress)
            {
                var otherUserId = activeCall.CallerId == userId ? activeCall.RecipientId : activeCall.CallerId;
                if (_userConnections.TryGetValue(otherUserId, out var otherConnId))
                {
                    await Clients.Client(otherConnId).SendAsync("ReceiveCallAudio", audioData);
                }
            }
        }
    }

    // Send speaking state during a call
    public async Task SendCallSpeakingState(string callId, bool isSpeaking, double audioLevel)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_callService != null)
        {
            var activeCall = _callService.GetActiveCall(userId);
            if (activeCall != null && activeCall.Id == callId)
            {
                var otherUserId = activeCall.CallerId == userId ? activeCall.RecipientId : activeCall.CallerId;
                if (_userConnections.TryGetValue(otherUserId, out var otherConnId))
                {
                    await Clients.Client(otherConnId).SendAsync("CallUserSpeaking", userId, isSpeaking, audioLevel);
                }
            }
        }
    }

    #region Group Calls

    private static readonly ConcurrentDictionary<string, GroupCallState> _groupCalls = new();

    public async Task StartGroupCall(string name, List<string> invitedUserIds)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
            return;

        var callId = Guid.NewGuid().ToString();
        var call = new GroupCallState
        {
            Id = callId,
            Name = name,
            HostId = userId,
            HostUsername = userState.Username,
            HostAvatarUrl = userState.AvatarUrl,
            StartedAt = DateTime.UtcNow
        };

        // Add host as participant
        call.Participants[userId] = new GroupCallParticipantState
        {
            UserId = userId,
            Username = userState.Username,
            AvatarUrl = userState.AvatarUrl,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTime.UtcNow
        };

        _groupCalls[callId] = call;

        // Add host to call group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"groupcall_{callId}");

        // Send call started to host
        await Clients.Caller.SendAsync("GroupCallStarted", call.ToDto());

        // Send invites to invited users
        foreach (var invitedId in invitedUserIds)
        {
            if (_userConnections.TryGetValue(invitedId, out var invitedConnId))
            {
                var invite = new GroupCallInviteDto
                {
                    CallId = callId,
                    HostId = userId,
                    HostUsername = userState.Username,
                    HostAvatarUrl = userState.AvatarUrl,
                    CallName = name,
                    ParticipantCount = 1,
                    InvitedAt = DateTime.UtcNow
                };
                await Clients.Client(invitedConnId).SendAsync("GroupCallInvite", invite);
            }
        }
    }

    public async Task JoinGroupCall(string callId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
            return;

        if (!_groupCalls.TryGetValue(callId, out var call))
        {
            await Clients.Caller.SendAsync("GroupCallError", "Call not found or has ended");
            return;
        }

        if (call.Status != GroupCallStatus.Active && call.Status != GroupCallStatus.Starting)
        {
            await Clients.Caller.SendAsync("GroupCallError", "Call has ended");
            return;
        }

        // Add participant
        var participant = new GroupCallParticipantState
        {
            UserId = userId,
            Username = userState.Username,
            AvatarUrl = userState.AvatarUrl,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTime.UtcNow
        };
        call.Participants[userId] = participant;

        // Mark call as active if it was starting
        if (call.Status == GroupCallStatus.Starting)
        {
            call.Status = GroupCallStatus.Active;
        }

        // Add to call group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"groupcall_{callId}");

        // Send call info to joiner
        await Clients.Caller.SendAsync("GroupCallStarted", call.ToDto());

        // Notify all participants
        await Clients.Group($"groupcall_{callId}").SendAsync("GroupCallParticipantJoined", callId, participant.ToDto());
        await Clients.Group($"groupcall_{callId}").SendAsync("GroupCallUpdated", call.ToDto());
    }

    public async Task LeaveGroupCall(string callId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_groupCalls.TryGetValue(callId, out var call))
            return;

        // Remove participant
        call.Participants.TryRemove(userId, out _);

        // Remove from call group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"groupcall_{callId}");

        // Notify remaining participants
        await Clients.Group($"groupcall_{callId}").SendAsync("GroupCallParticipantLeft", callId, userId);

        // If host left or no participants, end the call
        if (userId == call.HostId || call.Participants.IsEmpty)
        {
            call.Status = GroupCallStatus.Ended;
            await Clients.Group($"groupcall_{callId}").SendAsync("GroupCallEnded", callId, userId == call.HostId ? "Host left the call" : "All participants left");
            _groupCalls.TryRemove(callId, out _);
        }
        else
        {
            await Clients.Group($"groupcall_{callId}").SendAsync("GroupCallUpdated", call.ToDto());
        }
    }

    public async Task InviteToGroupCall(string callId, string targetUserId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_groupCalls.TryGetValue(callId, out var call))
        {
            await Clients.Caller.SendAsync("GroupCallError", "Call not found");
            return;
        }

        // Only host or participants can invite
        if (!call.Participants.ContainsKey(userId))
        {
            await Clients.Caller.SendAsync("GroupCallError", "You are not in this call");
            return;
        }

        if (_userConnections.TryGetValue(targetUserId, out var targetConnId))
        {
            var invite = new GroupCallInviteDto
            {
                CallId = callId,
                HostId = call.HostId,
                HostUsername = call.HostUsername,
                HostAvatarUrl = call.HostAvatarUrl,
                CallName = call.Name,
                ParticipantCount = call.Participants.Count,
                InvitedAt = DateTime.UtcNow
            };
            await Clients.Client(targetConnId).SendAsync("GroupCallInvite", invite);
        }
    }

    public async Task DeclineGroupCall(string callId)
    {
        // Just acknowledge - nothing to do server-side for a decline
        // Could notify the host if desired
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_groupCalls.TryGetValue(callId, out var call))
        {
            if (_userConnections.TryGetValue(call.HostId, out var hostConnId))
            {
                await Clients.Client(hostConnId).SendAsync("GroupCallInviteDeclined", callId, userId);
            }
        }
    }

    public async Task SendGroupCallAudio(string callId, byte[] audioData)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_groupCalls.TryGetValue(callId, out var call))
            return;

        if (!call.Participants.ContainsKey(userId))
            return;

        // Broadcast audio to all other participants
        await Clients.OthersInGroup($"groupcall_{callId}").SendAsync("GroupCallAudioReceived", userId, audioData);
    }

    public async Task SendGroupCallSpeakingState(string callId, bool isSpeaking, double audioLevel)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_groupCalls.TryGetValue(callId, out var call))
            return;

        if (call.Participants.TryGetValue(userId, out var participant))
        {
            participant.IsSpeaking = isSpeaking;
            participant.AudioLevel = audioLevel;

            await Clients.OthersInGroup($"groupcall_{callId}").SendAsync("GroupCallUserSpeaking", callId, userId, isSpeaking, audioLevel);
        }
    }

    #endregion
}

public class VoiceChannelState
{
    public string ChannelId { get; set; } = string.Empty;
    public ConcurrentDictionary<string, VoiceUserState> Users { get; } = new();
}

public class VoiceUserState
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public double AudioLevel { get; set; }
    public bool IsScreenSharing { get; set; }
}

public class ScreenShareState
{
    public string SharerConnectionId { get; set; } = string.Empty;
    public string SharerUsername { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public int FramesSent { get; set; }
    public int FramesDropped { get; set; }
    public long BytesSent { get; set; }
    public int LastWidth { get; set; }
    public int LastHeight { get; set; }
    public DateTime LastFrameTime { get; set; }
    public string CurrentQuality { get; set; } = "720p";
}

public class VoiceRoom
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string HostAvatarUrl { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public string? PasswordHash { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public VoiceRoomCategory Category { get; set; } = VoiceRoomCategory.General;
    public bool AllowScreenShare { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public ConcurrentDictionary<string, VoiceRoomParticipant> Participants { get; } = new();
    public HashSet<string> Moderators { get; } = [];

    public VoiceRoomDto ToDto(bool includePassword = false) => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        HostId = HostId,
        HostUsername = HostUsername,
        HostAvatarUrl = HostAvatarUrl,
        IsPublic = IsPublic,
        Password = includePassword ? (PasswordHash != null ? "protected" : null) : null,
        MaxParticipants = MaxParticipants,
        CurrentParticipants = Participants.Count,
        Participants = Participants.Values.Select(p => p.ToDto()).ToList(),
        CreatedAt = CreatedAt,
        Category = Category,
        AllowScreenShare = AllowScreenShare,
        IsActive = IsActive
    };
}

public class VoiceRoomParticipant
{
    public string UserId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public bool IsModerator { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsScreenSharing { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public VoiceRoomParticipantDto ToDto() => new()
    {
        UserId = UserId,
        Username = Username,
        AvatarUrl = AvatarUrl,
        IsHost = IsHost,
        IsModerator = IsModerator,
        IsMuted = IsMuted,
        IsDeafened = IsDeafened,
        IsSpeaking = IsSpeaking,
        IsScreenSharing = IsScreenSharing,
        JoinedAt = JoinedAt
    };
}

// VoiceRoomCategory is defined in VeaMarketplace.Shared.DTOs

public class GroupCallState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;
    public string HostAvatarUrl { get; set; } = string.Empty;
    public GroupCallStatus Status { get; set; } = GroupCallStatus.Starting;
    public DateTime StartedAt { get; set; }
    public ConcurrentDictionary<string, GroupCallParticipantState> Participants { get; } = new();

    public GroupCallDto ToDto() => new()
    {
        Id = Id,
        Name = Name,
        HostId = HostId,
        HostUsername = HostUsername,
        HostAvatarUrl = HostAvatarUrl,
        Status = Status,
        StartedAt = StartedAt,
        Participants = Participants.Values.Select(p => p.ToDto()).ToList()
    };
}

public class GroupCallParticipantState
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public double AudioLevel { get; set; }
    public bool IsScreenSharing { get; set; }
    public DateTime JoinedAt { get; set; }

    public GroupCallParticipantDto ToDto() => new()
    {
        UserId = UserId,
        Username = Username,
        AvatarUrl = AvatarUrl,
        IsMuted = IsMuted,
        IsDeafened = IsDeafened,
        IsSpeaking = IsSpeaking,
        IsScreenSharing = IsScreenSharing,
        JoinedAt = JoinedAt,
        Status = GroupCallParticipantStatus.Connected
    };
}
