using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class FriendService
{
    private readonly DatabaseService _db;

    public FriendService(DatabaseService db)
    {
        _db = db;
    }

    public List<FriendDto> GetFriends(string userId)
    {
        var friendships = _db.Friendships
            .Find(f => (f.RequesterId == userId || f.AddresseeId == userId)
                       && f.Status == FriendshipStatus.Accepted)
            .ToList();

        var friends = new List<FriendDto>();
        foreach (var friendship in friendships)
        {
            var friendId = friendship.RequesterId == userId
                ? friendship.AddresseeId
                : friendship.RequesterId;
            var friend = _db.Users.FindById(friendId);
            if (friend != null)
            {
                friends.Add(new FriendDto
                {
                    Id = friendship.Id,
                    UserId = friend.Id,
                    Username = friend.Username,
                    DisplayName = friend.DisplayName,
                    AvatarUrl = friend.AvatarUrl,
                    Bio = friend.Bio,
                    StatusMessage = friend.StatusMessage,
                    AccentColor = friend.AccentColor,
                    Role = friend.Role,
                    Rank = friend.Rank,
                    IsOnline = friend.IsOnline,
                    FriendsSince = friendship.AcceptedAt ?? friendship.CreatedAt
                });
            }
        }

        return friends.OrderByDescending(f => f.IsOnline).ThenBy(f => f.Username).ToList();
    }

    public List<FriendRequestDto> GetPendingRequests(string userId)
    {
        var requests = _db.Friendships
            .Find(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
            .ToList();

        var result = new List<FriendRequestDto>();
        foreach (var request in requests)
        {
            var requester = _db.Users.FindById(request.RequesterId);
            if (requester != null)
            {
                result.Add(new FriendRequestDto
                {
                    Id = request.Id,
                    RequesterId = requester.Id,
                    RequesterUsername = requester.Username,
                    RequesterAvatarUrl = requester.AvatarUrl,
                    RequesterRole = requester.Role,
                    RequestedAt = request.CreatedAt
                });
            }
        }

        return result.OrderByDescending(r => r.RequestedAt).ToList();
    }

    public List<FriendRequestDto> GetOutgoingRequests(string userId)
    {
        var requests = _db.Friendships
            .Find(f => f.RequesterId == userId && f.Status == FriendshipStatus.Pending)
            .ToList();

        var result = new List<FriendRequestDto>();
        foreach (var request in requests)
        {
            var addressee = _db.Users.FindById(request.AddresseeId);
            if (addressee != null)
            {
                result.Add(new FriendRequestDto
                {
                    Id = request.Id,
                    RequesterId = addressee.Id,
                    RequesterUsername = addressee.Username,
                    RequesterAvatarUrl = addressee.AvatarUrl,
                    RequesterRole = addressee.Role,
                    RequestedAt = request.CreatedAt
                });
            }
        }

        return result.OrderByDescending(r => r.RequestedAt).ToList();
    }

    public (bool Success, string Message, Friendship? Friendship) SendFriendRequest(string requesterId, string addresseeUsername)
    {
        var addressee = _db.Users.FindOne(u => u.Username == addresseeUsername);
        if (addressee == null)
            return (false, "User not found", null);

        return SendFriendRequestById(requesterId, addressee.Id);
    }

    public (bool Success, string Message, Friendship? Friendship) SendFriendRequestById(string requesterId, string addresseeId)
    {
        var addressee = _db.Users.FindById(addresseeId);
        if (addressee == null)
            return (false, "User not found", null);

        if (addressee.Id == requesterId)
            return (false, "You cannot add yourself as a friend", null);

        // Check if friendship already exists
        var existing = _db.Friendships.FindOne(f =>
            (f.RequesterId == requesterId && f.AddresseeId == addressee.Id) ||
            (f.RequesterId == addressee.Id && f.AddresseeId == requesterId));

        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                return (false, "You are already friends", null);
            if (existing.Status == FriendshipStatus.Pending)
                return (false, "Friend request already pending", null);
            if (existing.Status == FriendshipStatus.Blocked)
                return (false, "Unable to send friend request", null);
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addressee.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Friendships.Insert(friendship);
        return (true, "Friend request sent", friendship);
    }

    public User? SearchUserByIdOrUsername(string query)
    {
        // Try to find by exact ID first
        var user = _db.Users.FindById(query);
        if (user != null) return user;

        // Try by exact username (case-insensitive)
        user = _db.Users.FindOne(u => u.Username.ToLower() == query.ToLower());
        return user;
    }

    public List<User> SearchUsers(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<User>();

        var queryLower = query.ToLower();
        return _db.Users
            .Find(u => u.Username.ToLower().Contains(queryLower) ||
                       u.DisplayName.ToLower().Contains(queryLower) ||
                       u.Id == query)
            .Take(limit)
            .ToList();
    }

    public (bool Success, string Message) RespondToFriendRequest(string userId, string requestId, bool accept)
    {
        var friendship = _db.Friendships.FindById(requestId);
        if (friendship == null)
            return (false, "Friend request not found");

        if (friendship.AddresseeId != userId)
            return (false, "Not authorized to respond to this request");

        if (friendship.Status != FriendshipStatus.Pending)
            return (false, "Friend request is no longer pending");

        if (accept)
        {
            friendship.Status = FriendshipStatus.Accepted;
            friendship.AcceptedAt = DateTime.UtcNow;
            _db.Friendships.Update(friendship);
            return (true, "Friend request accepted");
        }
        else
        {
            friendship.Status = FriendshipStatus.Declined;
            _db.Friendships.Update(friendship);
            return (true, "Friend request declined");
        }
    }

    public (bool Success, string Message) RemoveFriend(string userId, string friendId)
    {
        var friendship = _db.Friendships.FindOne(f =>
            ((f.RequesterId == userId && f.AddresseeId == friendId) ||
             (f.RequesterId == friendId && f.AddresseeId == userId)) &&
            f.Status == FriendshipStatus.Accepted);

        if (friendship == null)
            return (false, "Friendship not found");

        _db.Friendships.Delete(friendship.Id);
        return (true, "Friend removed");
    }

    public bool AreFriends(string userId1, string userId2)
    {
        return _db.Friendships.Exists(f =>
            ((f.RequesterId == userId1 && f.AddresseeId == userId2) ||
             (f.RequesterId == userId2 && f.AddresseeId == userId1)) &&
            f.Status == FriendshipStatus.Accepted);
    }

    public (bool Success, string Message) CancelFriendRequest(string userId, string requestId)
    {
        var friendship = _db.Friendships.FindById(requestId);
        if (friendship == null)
            return (false, "Friend request not found");

        if (friendship.RequesterId != userId)
            return (false, "Not authorized to cancel this request");

        if (friendship.Status != FriendshipStatus.Pending)
            return (false, "Friend request is no longer pending");

        _db.Friendships.Delete(requestId);
        return (true, "Friend request cancelled");
    }

    public (bool Success, string Message) BlockUser(string userId, string targetUserId, string? reason = null)
    {
        if (userId == targetUserId)
            return (false, "Cannot block yourself");

        var target = _db.Users.FindById(targetUserId);
        if (target == null)
            return (false, "User not found");

        // Check if already blocked
        var existing = _db.Friendships.FindOne(f =>
            f.RequesterId == userId && f.AddresseeId == targetUserId && f.Status == FriendshipStatus.Blocked);

        if (existing != null)
            return (false, "User is already blocked");

        // Remove any existing friendship
        var friendship = _db.Friendships.FindOne(f =>
            (f.RequesterId == userId && f.AddresseeId == targetUserId) ||
            (f.RequesterId == targetUserId && f.AddresseeId == userId));

        if (friendship != null)
        {
            _db.Friendships.Delete(friendship.Id);
        }

        // Create block record
        var block = new Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUserId,
            Status = FriendshipStatus.Blocked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Friendships.Insert(block);
        return (true, "User blocked");
    }

    public (bool Success, string Message) UnblockUser(string userId, string targetUserId)
    {
        var block = _db.Friendships.FindOne(f =>
            f.RequesterId == userId && f.AddresseeId == targetUserId && f.Status == FriendshipStatus.Blocked);

        if (block == null)
            return (false, "User is not blocked");

        _db.Friendships.Delete(block.Id);
        return (true, "User unblocked");
    }

    public List<BlockedUserDto> GetBlockedUsers(string userId)
    {
        var blocks = _db.Friendships
            .Find(f => f.RequesterId == userId && f.Status == FriendshipStatus.Blocked)
            .ToList();

        var result = new List<BlockedUserDto>();
        foreach (var block in blocks)
        {
            var blockedUser = _db.Users.FindById(block.AddresseeId);
            if (blockedUser != null)
            {
                result.Add(new BlockedUserDto
                {
                    UserId = blockedUser.Id,
                    Username = blockedUser.Username,
                    AvatarUrl = blockedUser.AvatarUrl,
                    BlockedAt = block.CreatedAt
                });
            }
        }

        return result;
    }

    public bool IsBlocked(string userId, string targetUserId)
    {
        return _db.Friendships.Exists(f =>
            ((f.RequesterId == userId && f.AddresseeId == targetUserId) ||
             (f.RequesterId == targetUserId && f.AddresseeId == userId)) &&
            f.Status == FriendshipStatus.Blocked);
    }

    public string? GetUserNote(string userId, string targetUserId)
    {
        var nickname = _db.FriendNicknames.FindOne(n =>
            n.UserId == userId && n.FriendUserId == targetUserId);
        return nickname?.Note;
    }

    public void SetUserNote(string userId, string targetUserId, string note)
    {
        var existing = _db.FriendNicknames.FindOne(n =>
            n.UserId == userId && n.FriendUserId == targetUserId);

        if (existing != null)
        {
            existing.Note = note;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.FriendNicknames.Update(existing);
        }
        else
        {
            var nickname = new FriendNickname
            {
                UserId = userId,
                FriendUserId = targetUserId,
                Note = note
            };
            _db.FriendNicknames.Insert(nickname);
        }
    }
}
