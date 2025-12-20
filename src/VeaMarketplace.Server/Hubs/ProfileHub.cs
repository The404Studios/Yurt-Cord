using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Hubs;

public class ProfileHub : Hub
{
    private readonly AuthService _authService;
    private readonly FriendService _friendService;
    private static readonly ConcurrentDictionary<string, string> _userConnections = new(); // userId -> connectionId
    private static readonly ConcurrentDictionary<string, string> _connectionUsers = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, UserDto> _onlineUsers = new(); // userId -> UserDto (cached)

    public ProfileHub(AuthService authService, FriendService friendService)
    {
        _authService = authService;
        _friendService = friendService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = Context.ConnectionId,
            ServerTime = DateTime.UtcNow,
            Hub = "ProfileHub"
        });

        await base.OnConnectedAsync();
    }

    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        // Track connection
        _userConnections[user.Id] = Context.ConnectionId;
        _connectionUsers[Context.ConnectionId] = user.Id;

        // Cache user profile
        var userDto = _authService.MapToDto(user);
        _onlineUsers[user.Id] = userDto;

        // Add to personal group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");

        // Add to global online users group
        await Groups.AddToGroupAsync(Context.ConnectionId, "online_users");

        // Send current user's profile
        await Clients.Caller.SendAsync("ProfileLoaded", userDto);

        // Send list of all online users
        var onlineUsersList = _onlineUsers.Values.ToList();
        await Clients.Caller.SendAsync("OnlineUsersList", onlineUsersList);

        // Notify all other users that this user came online
        await Clients.OthersInGroup("online_users").SendAsync("UserOnline", userDto);

        await Clients.Caller.SendAsync("AuthenticationSuccess");
    }

    // Get another user's public profile
    public async Task GetUserProfile(string userId)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var requesterId))
            return;

        var user = _authService.GetUserById(userId);
        if (user == null)
        {
            await Clients.Caller.SendAsync("ProfileError", "User not found");
            return;
        }

        // Check profile visibility
        var canView = CanViewProfile(requesterId, user);
        if (!canView)
        {
            await Clients.Caller.SendAsync("ProfileError", "This profile is private");
            return;
        }

        var userDto = _authService.MapToDto(user);

        // If not friends and profile is FriendsOnly, limit what we show
        if (user.ProfileVisibility == ProfileVisibility.FriendsOnly &&
            !_friendService.AreFriends(requesterId, userId))
        {
            // Return limited profile
            var limitedProfile = new UserDto
            {
                Id = userDto.Id,
                Username = userDto.Username,
                DisplayName = userDto.DisplayName,
                AvatarUrl = userDto.AvatarUrl,
                Role = userDto.Role,
                Rank = userDto.Rank,
                IsOnline = userDto.IsOnline,
                CreatedAt = userDto.CreatedAt
                // Bio, Description, StatusMessage, social links etc are hidden
            };
            await Clients.Caller.SendAsync("UserProfileLoaded", limitedProfile);
            return;
        }

        await Clients.Caller.SendAsync("UserProfileLoaded", userDto);
    }

    // Update own profile and broadcast to relevant users
    public async Task UpdateProfile(UpdateProfileRequest request)
    {
        if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            return;

        var updatedUser = _authService.UpdateProfile(userId, request);
        if (updatedUser == null)
        {
            await Clients.Caller.SendAsync("ProfileError", "Failed to update profile");
            return;
        }

        // Update cache
        _onlineUsers[userId] = updatedUser;

        // Send updated profile back to the user
        await Clients.Caller.SendAsync("ProfileUpdated", updatedUser);

        // Notify all online users of the profile update
        await Clients.OthersInGroup("online_users").SendAsync("UserProfileUpdated", updatedUser);

        // Also notify friends specifically (in case they have the profile view open)
        var friends = _friendService.GetFriends(userId);
        foreach (var friend in friends)
        {
            if (_userConnections.TryGetValue(friend.UserId, out var friendConnId))
            {
                await Clients.Client(friendConnId).SendAsync("FriendProfileUpdated", updatedUser);
            }
        }
    }

    // Get all online users
    public async Task GetOnlineUsers()
    {
        var onlineUsersList = _onlineUsers.Values.ToList();
        await Clients.Caller.SendAsync("OnlineUsersList", onlineUsersList);
    }

    // Subscribe to a specific user's profile updates (for viewing their profile)
    public async Task SubscribeToProfile(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"profile_watchers_{userId}");
    }

    public async Task UnsubscribeFromProfile(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"profile_watchers_{userId}");
    }

    private bool CanViewProfile(string requesterId, User targetUser)
    {
        // Own profile is always viewable
        if (requesterId == targetUser.Id) return true;

        switch (targetUser.ProfileVisibility)
        {
            case ProfileVisibility.Public:
                return true;
            case ProfileVisibility.FriendsOnly:
                return _friendService.AreFriends(requesterId, targetUser.Id);
            case ProfileVisibility.Private:
                return false;
            default:
                return true;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUsers.TryRemove(Context.ConnectionId, out var userId))
        {
            _userConnections.TryRemove(userId, out _);
            _onlineUsers.TryRemove(userId, out var userDto);

            // Notify all users that this user went offline
            if (userDto != null)
            {
                await Clients.Group("online_users").SendAsync("UserOffline", userId, userDto.Username);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Static method to broadcast profile updates from other places (e.g., REST API)
    public static void NotifyProfileUpdate(IHubContext<ProfileHub> hubContext, UserDto updatedUser)
    {
        if (_userConnections.TryGetValue(updatedUser.Id, out var connId))
        {
            hubContext.Clients.Client(connId).SendAsync("ProfileUpdated", updatedUser);
        }
        hubContext.Clients.Group("online_users").SendAsync("UserProfileUpdated", updatedUser);
    }

    /// <summary>
    /// Heartbeat ping from client
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new
        {
            ServerTime = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        });
    }
}
