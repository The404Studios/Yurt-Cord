using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;
using System.Collections.Concurrent;

namespace VeaMarketplace.Server.Hubs;

public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly AuthService _authService;
    private static readonly ConcurrentDictionary<string, OnlineUserDto> _onlineUsers = new();
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new();
    private static readonly ConcurrentDictionary<string, List<string>> _userConnectionsMap = new(); // userId -> list of connectionIds

    public ChatHub(ChatService chatService, AuthService authService)
    {
        _chatService = chatService;
        _authService = authService;
    }

    /// <summary>
    /// Updates a user's profile across all connected clients.
    /// Called when a user changes their avatar, banner, or other profile info.
    /// </summary>
    public async Task UpdateUserProfile(UpdateProfileRequest request)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        // Get fresh user data from database
        var user = _authService.GetUserById(userId);
        if (user == null) return;

        // Update the cached online user
        if (_onlineUsers.TryGetValue(userId, out var onlineUser))
        {
            onlineUser.AvatarUrl = user.AvatarUrl ?? onlineUser.AvatarUrl;
            onlineUser.BannerUrl = user.BannerUrl;
            onlineUser.Username = user.Username;
            onlineUser.DisplayName = user.DisplayName;
            onlineUser.StatusMessage = user.StatusMessage;
            onlineUser.Bio = user.Bio;
            onlineUser.AccentColor = user.AccentColor;
            onlineUser.LastUpdated = DateTime.UtcNow;

            // Broadcast the profile update to all connected clients
            await Clients.All.SendAsync("UserProfileUpdated", onlineUser);
        }
    }

    /// <summary>
    /// Static method to broadcast profile updates from other services (e.g., ProfileHub, REST API)
    /// </summary>
    public static async Task BroadcastProfileUpdate(IHubContext<ChatHub> hubContext, User user)
    {
        // Update cached user if online, otherwise create a temporary DTO for broadcast
        OnlineUserDto userDto;

        if (_onlineUsers.TryGetValue(user.Id, out var onlineUser))
        {
            // Update existing online user
            onlineUser.AvatarUrl = user.AvatarUrl ?? onlineUser.AvatarUrl;
            onlineUser.BannerUrl = user.BannerUrl;
            onlineUser.Username = user.Username;
            onlineUser.DisplayName = user.DisplayName;
            onlineUser.StatusMessage = user.StatusMessage;
            onlineUser.Bio = user.Bio;
            onlineUser.AccentColor = user.AccentColor;
            onlineUser.LastUpdated = DateTime.UtcNow;
            userDto = onlineUser;
        }
        else
        {
            // Create a DTO for broadcasting even if user isn't in chat
            userDto = new OnlineUserDto
            {
                Id = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl ?? string.Empty,
                BannerUrl = user.BannerUrl,
                DisplayName = user.DisplayName,
                Role = user.Role,
                Rank = user.Rank,
                StatusMessage = user.StatusMessage,
                Bio = user.Bio,
                AccentColor = user.AccentColor,
                LastUpdated = DateTime.UtcNow
            };
        }

        // Always broadcast the update to all clients
        await hubContext.Clients.All.SendAsync("UserProfileUpdated", userDto);
    }

    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        var onlineUser = new OnlineUserDto
        {
            Id = user.Id,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl ?? string.Empty,
            BannerUrl = user.BannerUrl,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Rank = user.Rank,
            StatusMessage = user.StatusMessage,
            Bio = user.Bio,
            AccentColor = user.AccentColor,
            LastUpdated = DateTime.UtcNow
        };

        _onlineUsers[user.Id] = onlineUser;
        _connectionUserMap[Context.ConnectionId] = user.Id;

        await Groups.AddToGroupAsync(Context.ConnectionId, "general");

        // Send channel list based on user role
        var channels = _chatService.GetChannels(user.Role);
        await Clients.Caller.SendAsync("ChannelList", channels);

        // Send online users
        await Clients.All.SendAsync("UserJoined", onlineUser);
        await Clients.Caller.SendAsync("OnlineUsers", _onlineUsers.Values.ToList());

        // Send chat history
        var history = _chatService.GetChannelHistory("general");
        await Clients.Caller.SendAsync("ChatHistory", "general", history);

        // Broadcast join message
        var joinMessage = _chatService.CreateSystemMessage("general", $"{user.Username} joined the chat", MessageType.Join);
        await Clients.Group("general").SendAsync("ReceiveMessage", joinMessage);

        await Clients.Caller.SendAsync("AuthenticationSuccess", _authService.MapToDto(user));
    }

    public async Task JoinChannel(string channelName)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_onlineUsers.TryGetValue(userId, out var user))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, channelName);

        var history = _chatService.GetChannelHistory(channelName);
        await Clients.Caller.SendAsync("ChatHistory", channelName, history);

        var joinMessage = _chatService.CreateSystemMessage(channelName, $"{user.Username} joined #{channelName}", MessageType.Join);
        await Clients.Group(channelName).SendAsync("ReceiveMessage", joinMessage);
    }

    public async Task LeaveChannel(string channelName)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (!_onlineUsers.TryGetValue(userId, out var user))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelName);

        var leaveMessage = _chatService.CreateSystemMessage(channelName, $"{user.Username} left #{channelName}", MessageType.Leave);
        await Clients.Group(channelName).SendAsync("ReceiveMessage", leaveMessage);
    }

    public async Task SendMessage(string content, string channel = "general")
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var request = new SendMessageRequest
        {
            Content = content,
            Channel = channel
        };

        var message = _chatService.SaveMessage(userId, request);
        await Clients.Group(channel).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Send a message with file attachments
    /// </summary>
    public async Task SendMessageWithAttachments(string content, string channel, List<MessageAttachmentDto> attachments)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var request = new SendMessageRequest
        {
            Content = content,
            Channel = channel
        };

        var message = _chatService.SaveMessageWithAttachments(userId, request, attachments);
        await Clients.Group(channel).SendAsync("ReceiveMessage", message);
    }

    public async Task DeleteMessage(string messageId, string channel = "general")
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_chatService.DeleteMessage(messageId, userId))
        {
            // Only notify users in the same channel, not all connected clients
            await Clients.Group(channel).SendAsync("MessageDeleted", messageId);
        }
    }

    public async Task SendTyping(string channel)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_onlineUsers.TryGetValue(userId, out var user))
        {
            await Clients.OthersInGroup(channel).SendAsync("UserTyping", user.Username, channel);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUserMap.TryRemove(Context.ConnectionId, out var userId))
        {
            if (_onlineUsers.TryRemove(userId, out var user))
            {
                await Clients.All.SendAsync("UserLeft", user);

                var leaveMessage = _chatService.CreateSystemMessage("general", $"{user.Username} left the chat", MessageType.Leave);
                await Clients.Group("general").SendAsync("ReceiveMessage", leaveMessage);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<List<OnlineUserDto>> GetOnlineUsers()
    {
        return Task.FromResult(_onlineUsers.Values.ToList());
    }
}
