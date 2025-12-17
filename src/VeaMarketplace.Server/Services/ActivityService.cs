using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class ActivityService
{
    private readonly DatabaseService _db;

    public ActivityService(DatabaseService db)
    {
        _db = db;
    }

    public List<UserActivityDto> GetActivityFeed(string userId, ActivityType? type = null, int page = 1, int pageSize = 50)
    {
        var query = _db.UserActivities.Query()
            .Where(a => a.UserId == userId);

        if (type.HasValue)
        {
            query = query.Where(a => a.Type == type.Value);
        }

        return query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    public List<UserActivityDto> GetGlobalFeed(int page = 1, int pageSize = 50)
    {
        return _db.UserActivities.Query()
            .Where(a => a.IsPublic)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList()
            .Select(MapToDto)
            .ToList();
    }

    public List<UserActivityDto> GetFriendsFeed(string userId, int page = 1, int pageSize = 50)
    {
        // Get friend IDs
        var friendships = _db.Friendships.Find(f =>
            (f.RequesterId == userId || f.AddresseeId == userId) && f.Status == FriendshipStatus.Accepted)
            .ToList();

        var friendIds = friendships.Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId).ToList();
        friendIds.Add(userId); // Include own activities

        return _db.UserActivities.Query()
            .Where(a => a.IsPublic)
            .ToEnumerable()
            .Where(a => friendIds.Contains(a.UserId))
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();
    }

    public UserActivityDto? LogActivity(
        string userId,
        ActivityType type,
        string? targetId = null,
        string? targetName = null,
        string? description = null,
        string? imageUrl = null,
        bool isPublic = true,
        Dictionary<string, string>? metadata = null)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return null;

        var activity = new UserActivity
        {
            UserId = userId,
            Username = user.Username,
            UserAvatarUrl = user.AvatarUrl ?? "",
            Type = type,
            TargetId = targetId,
            TargetName = targetName,
            Description = description ?? GetDefaultDescription(type, targetName),
            ImageUrl = imageUrl,
            IsPublic = isPublic,
            Metadata = metadata ?? new(),
            CreatedAt = DateTime.UtcNow
        };

        _db.UserActivities.Insert(activity);
        return MapToDto(activity);
    }

    public void LogProductView(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return;

        LogActivity(userId, ActivityType.ViewedProduct, productId, product.Title,
            imageUrl: product.ImageUrls.FirstOrDefault(), isPublic: false);
    }

    public void LogProductListed(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return;

        LogActivity(userId, ActivityType.ListedProduct, productId, product.Title,
            $"Listed a new product: {product.Title}",
            product.ImageUrls.FirstOrDefault());
    }

    public void LogProductPurchased(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return;

        LogActivity(userId, ActivityType.PurchasedProduct, productId, product.Title,
            $"Purchased {product.Title}",
            product.ImageUrls.FirstOrDefault());
    }

    public void LogProductSold(string userId, string productId)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return;

        LogActivity(userId, ActivityType.SoldProduct, productId, product.Title,
            $"Sold {product.Title}",
            product.ImageUrls.FirstOrDefault());
    }

    public void LogReviewPosted(string userId, string productId, int rating)
    {
        var product = _db.Products.FindById(productId);
        if (product == null) return;

        LogActivity(userId, ActivityType.PostedReview, productId, product.Title,
            $"Left a {rating}-star review on {product.Title}");
    }

    public void LogAchievementUnlocked(string userId, string achievementName, string? iconUrl = null)
    {
        LogActivity(userId, ActivityType.UnlockedAchievement, null, achievementName,
            $"Unlocked achievement: {achievementName}", iconUrl);
    }

    public void LogFriendAdded(string userId, string friendId)
    {
        var friend = _db.Users.FindById(friendId);
        if (friend == null) return;

        LogActivity(userId, ActivityType.AddedFriend, friendId, friend.Username,
            $"Became friends with {friend.Username}",
            friend.AvatarUrl);
    }

    public void LogStatusUpdate(string userId, string status)
    {
        LogActivity(userId, ActivityType.UpdatedStatus, null, null,
            status, isPublic: true);
    }

    public void LogRankUp(string userId, string newRank)
    {
        LogActivity(userId, ActivityType.RankedUp, null, newRank,
            $"Reached {newRank} rank!");
    }

    private static string GetDefaultDescription(ActivityType type, string? targetName)
    {
        return type switch
        {
            ActivityType.ListedProduct => $"Listed {targetName}",
            ActivityType.PurchasedProduct => $"Purchased {targetName}",
            ActivityType.SoldProduct => $"Sold {targetName}",
            ActivityType.ViewedProduct => $"Viewed {targetName}",
            ActivityType.PostedReview => $"Reviewed {targetName}",
            ActivityType.AddedFriend => $"Became friends with {targetName}",
            ActivityType.JoinedRoom => $"Joined {targetName}",
            ActivityType.UpdatedStatus => "Updated status",
            ActivityType.UnlockedAchievement => $"Unlocked {targetName}",
            ActivityType.RankedUp => $"Reached {targetName}",
            _ => "Activity"
        };
    }

    private static UserActivityDto MapToDto(UserActivity activity)
    {
        return new UserActivityDto
        {
            Id = activity.Id,
            UserId = activity.UserId,
            Username = activity.Username,
            UserAvatarUrl = activity.UserAvatarUrl,
            Type = activity.Type,
            TargetId = activity.TargetId,
            TargetName = activity.TargetName,
            Description = activity.Description,
            ImageUrl = activity.ImageUrl,
            IsPublic = activity.IsPublic,
            Metadata = activity.Metadata,
            CreatedAt = activity.CreatedAt,
            Icon = GetActivityIcon(activity.Type),
            ActionText = GetActionText(activity.Type)
        };
    }

    private static string GetActivityIcon(ActivityType type)
    {
        return type switch
        {
            ActivityType.ListedProduct => "📦",
            ActivityType.PurchasedProduct => "🛒",
            ActivityType.SoldProduct => "💰",
            ActivityType.ViewedProduct => "👁️",
            ActivityType.PostedReview => "⭐",
            ActivityType.AddedFriend => "🤝",
            ActivityType.JoinedRoom => "🚪",
            ActivityType.UpdatedStatus => "💬",
            ActivityType.UnlockedAchievement => "🏆",
            ActivityType.RankedUp => "📈",
            _ => "📌"
        };
    }

    private static string GetActionText(ActivityType type)
    {
        return type switch
        {
            ActivityType.ListedProduct => "listed a product",
            ActivityType.PurchasedProduct => "made a purchase",
            ActivityType.SoldProduct => "made a sale",
            ActivityType.ViewedProduct => "viewed",
            ActivityType.PostedReview => "left a review",
            ActivityType.AddedFriend => "added a friend",
            ActivityType.JoinedRoom => "joined",
            ActivityType.UpdatedStatus => "updated status",
            ActivityType.UnlockedAchievement => "unlocked",
            ActivityType.RankedUp => "ranked up to",
            _ => "activity"
        };
    }
}
