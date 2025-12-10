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

    public ChatHub(ChatService chatService, AuthService authService)
    {
        _chatService = chatService;
        _authService = authService;
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
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            Rank = user.Rank
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

    public async Task DeleteMessage(string messageId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        if (_chatService.DeleteMessage(messageId, userId))
        {
            await Clients.All.SendAsync("MessageDeleted", messageId);
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
