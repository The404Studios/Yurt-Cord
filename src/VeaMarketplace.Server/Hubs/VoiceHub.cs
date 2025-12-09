using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Hubs;

public class VoiceHub : Hub
{
    private static readonly ConcurrentDictionary<string, VoiceChannelState> _voiceChannels = new();
    private static readonly ConcurrentDictionary<string, VoiceUserState> _voiceUsers = new();

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
        await base.OnDisconnectedAsync(exception);
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
}
