using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
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
        if (_voiceUsers.TryGetValue(targetConnectionId, out var targetUser))
        {
            // Notify the user they've been disconnected
            await Clients.Client(targetConnectionId).SendAsync("DisconnectedByAdmin", "You have been disconnected by an administrator");

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

            // Notify old channel users
            await Clients.Group($"voice_{targetChannelId}").SendAsync("UserJoinedVoice", targetUser);

            // Notify the moved user
            await Clients.Client(targetConnectionId).SendAsync("MovedToChannel", targetChannelId, "Administrator");

            // Send the user the list of users in new channel
            await Clients.Client(targetConnectionId).SendAsync("VoiceChannelUsers", newChannel.Users.Values.ToList());
        }
    }

    // Admin: Kick user (just disconnects with a kick message)
    public async Task KickUser(string userId, string reason)
    {
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
        if (_voiceUsers.TryGetValue(Context.ConnectionId, out var userState))
        {
            // Update frame stats
            if (_screenSharers.TryGetValue(Context.ConnectionId, out var shareState))
            {
                shareState.FramesSent++;
                shareState.LastWidth = width;
                shareState.LastHeight = height;
                shareState.LastFrameTime = DateTime.UtcNow;
            }

            // Broadcast screen frame to all OTHER users in the channel
            await Clients.OthersInGroup($"voice_{userState.ChannelId}")
                .SendAsync("ReceiveScreenFrame", Context.ConnectionId, frameData, width, height);
        }
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
        await LeaveVoiceChannel();

        // Handle call disconnection
        if (_connectionUsers.TryRemove(Context.ConnectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);

            // End any active calls
            if (_callService != null)
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
    public int LastWidth { get; set; }
    public int LastHeight { get; set; }
    public DateTime LastFrameTime { get; set; }
}
