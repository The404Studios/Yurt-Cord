using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Server.Hubs;

/// <summary>
/// Real-time hub for content updates including posts, products, auctions, and images
/// </summary>
public class ContentHub : Hub
{
    private readonly AuthService _authService;
    private readonly ProductService _productService;

    // Track user connections
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new(); // connectionId -> userId
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new(); // userId -> connectionIds

    // Track subscriptions
    private static readonly ConcurrentDictionary<string, ContentSubscription> _userSubscriptions = new(); // userId -> subscription
    private static readonly ConcurrentDictionary<string, HashSet<string>> _auctionWatchers = new(); // auctionId -> userIds
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userFollowers = new(); // userId -> follower userIds

    public ContentHub(AuthService authService, ProductService productService)
    {
        _authService = authService;
        _productService = productService;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ConnectionHandshake", new
        {
            ConnectionId = Context.ConnectionId,
            ServerTime = DateTime.UtcNow,
            Hub = "ContentHub"
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Authenticate and initialize the content hub connection
    /// </summary>
    public async Task Authenticate(string token)
    {
        var user = _authService.ValidateToken(token);
        if (user == null)
        {
            await Clients.Caller.SendAsync("AuthenticationFailed", "Invalid token");
            return;
        }

        // Track connection
        _connectionUserMap[Context.ConnectionId] = user.Id;

        if (!_userConnections.TryGetValue(user.Id, out var connections))
        {
            connections = new HashSet<string>();
            _userConnections[user.Id] = connections;
        }
        connections.Add(Context.ConnectionId);

        // Add to global feed group
        await Groups.AddToGroupAsync(Context.ConnectionId, "global_feed");

        // Add to user's personal group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");

        // Initialize default subscription
        if (!_userSubscriptions.ContainsKey(user.Id))
        {
            _userSubscriptions[user.Id] = new ContentSubscription
            {
                UserId = user.Id,
                ReceiveAllPublicPosts = true,
                ReceiveAuctionUpdates = true,
                ReceivePriceDrops = true
            };
        }

        await Clients.Caller.SendAsync("ContentHubConnected");
    }

    /// <summary>
    /// Subscribe to a specific user's posts and updates
    /// </summary>
    public async Task FollowUser(string userId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var currentUserId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"following_{userId}");

        // Update subscription
        if (_userSubscriptions.TryGetValue(currentUserId, out var sub))
        {
            if (!sub.FollowedUserIds.Contains(userId))
                sub.FollowedUserIds.Add(userId);
        }

        // Track followers
        if (!_userFollowers.TryGetValue(userId, out var followers))
        {
            followers = new HashSet<string>();
            _userFollowers[userId] = followers;
        }
        followers.Add(currentUserId);

        // Notify the followed user
        await Clients.Group($"user_{userId}").SendAsync("NewFollower", new FollowEvent
        {
            FollowerId = currentUserId,
            FollowerUsername = _authService.GetUserById(currentUserId)?.Username ?? "Unknown",
            FollowerAvatarUrl = _authService.GetUserById(currentUserId)?.AvatarUrl,
            FollowedUserId = userId,
            NewFollowerCount = followers.Count
        });
    }

    /// <summary>
    /// Unfollow a user
    /// </summary>
    public async Task UnfollowUser(string userId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var currentUserId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"following_{userId}");

        if (_userSubscriptions.TryGetValue(currentUserId, out var sub))
        {
            sub.FollowedUserIds.Remove(userId);
        }

        if (_userFollowers.TryGetValue(userId, out var followers))
        {
            followers.Remove(currentUserId);
        }
    }

    /// <summary>
    /// Watch an auction for bid updates
    /// </summary>
    public async Task WatchAuction(string auctionId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"auction_{auctionId}");

        if (!_auctionWatchers.TryGetValue(auctionId, out var watchers))
        {
            watchers = new HashSet<string>();
            _auctionWatchers[auctionId] = watchers;
        }
        watchers.Add(userId);

        if (_userSubscriptions.TryGetValue(userId, out var sub))
        {
            if (!sub.WatchedAuctionIds.Contains(auctionId))
                sub.WatchedAuctionIds.Add(auctionId);
        }

        await Clients.Caller.SendAsync("WatchingAuction", auctionId);
    }

    /// <summary>
    /// Stop watching an auction
    /// </summary>
    public async Task UnwatchAuction(string auctionId)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction_{auctionId}");

        if (_auctionWatchers.TryGetValue(auctionId, out var watchers))
        {
            watchers.Remove(userId);
        }

        if (_userSubscriptions.TryGetValue(userId, out var sub))
        {
            sub.WatchedAuctionIds.Remove(auctionId);
        }
    }

    /// <summary>
    /// Subscribe to a product category
    /// </summary>
    public async Task SubscribeToCategory(ProductCategory category)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"category_{category}");

        if (_userSubscriptions.TryGetValue(userId, out var sub))
        {
            if (!sub.InterestedCategories.Contains(category))
                sub.InterestedCategories.Add(category);
        }
    }

    /// <summary>
    /// Update subscription preferences
    /// </summary>
    public async Task UpdateSubscription(ContentSubscription subscription)
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return;

        subscription.UserId = userId;
        _userSubscriptions[userId] = subscription;

        await Clients.Caller.SendAsync("SubscriptionUpdated", subscription);
    }

    /// <summary>
    /// Get current subscription
    /// </summary>
    public Task<ContentSubscription?> GetSubscription()
    {
        if (!_connectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
            return Task.FromResult<ContentSubscription?>(null);

        _userSubscriptions.TryGetValue(userId, out var sub);
        return Task.FromResult(sub);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionUserMap.TryRemove(Context.ConnectionId, out var userId))
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    #region Static Broadcast Methods

    /// <summary>
    /// Broadcast a new post to all subscribers
    /// </summary>
    public static async Task BroadcastNewPost(IHubContext<ContentHub> hubContext, NewPostEvent postEvent)
    {
        // Send to global feed
        await hubContext.Clients.Group("global_feed").SendAsync("NewPost", postEvent);

        // Send to followers of the author
        await hubContext.Clients.Group($"following_{postEvent.SourceUserId}").SendAsync("FollowedUserPost", postEvent);

        // Send to category subscribers if applicable
        if (!string.IsNullOrEmpty(postEvent.Category) && Enum.TryParse<ProductCategory>(postEvent.Category, out var category))
        {
            await hubContext.Clients.Group($"category_{category}").SendAsync("CategoryPost", postEvent);
        }
    }

    /// <summary>
    /// Broadcast a profile picture update
    /// </summary>
    public static async Task BroadcastProfilePictureUpdate(IHubContext<ContentHub> hubContext, ProfilePictureUpdateEvent updateEvent)
    {
        // Notify everyone so profile pictures update everywhere in the app
        await hubContext.Clients.All.SendAsync("ProfilePictureUpdated", updateEvent);
    }

    /// <summary>
    /// Broadcast a banner update
    /// </summary>
    public static async Task BroadcastBannerUpdate(IHubContext<ContentHub> hubContext, BannerUpdateEvent updateEvent)
    {
        await hubContext.Clients.All.SendAsync("BannerUpdated", updateEvent);
    }

    /// <summary>
    /// Broadcast an auction bid update
    /// </summary>
    public static async Task BroadcastAuctionBid(IHubContext<ContentHub> hubContext, AuctionBidEvent bidEvent)
    {
        // Notify auction watchers
        await hubContext.Clients.Group($"auction_{bidEvent.AuctionId}").SendAsync("AuctionBidPlaced", bidEvent);

        // Also notify the auction owner
        await hubContext.Clients.Group($"user_{bidEvent.SourceUserId}").SendAsync("BidOnYourAuction", bidEvent);

        // Notify global feed for high-value bids or trending auctions
        await hubContext.Clients.Group("global_feed").SendAsync("AuctionActivity", bidEvent);
    }

    /// <summary>
    /// Broadcast auction ending soon notification
    /// </summary>
    public static async Task BroadcastAuctionEnding(IHubContext<ContentHub> hubContext, AuctionEndingEvent endingEvent)
    {
        await hubContext.Clients.Group($"auction_{endingEvent.AuctionId}").SendAsync("AuctionEnding", endingEvent);

        // Also broadcast to global feed for urgency
        if (endingEvent.MinutesRemaining <= 5)
        {
            await hubContext.Clients.Group("global_feed").SendAsync("AuctionEndingSoon", endingEvent);
        }
    }

    /// <summary>
    /// Broadcast a post update (price change, edit, etc.)
    /// </summary>
    public static async Task BroadcastPostUpdate(IHubContext<ContentHub> hubContext, PostUpdateEvent updateEvent)
    {
        await hubContext.Clients.Group("global_feed").SendAsync("PostUpdated", updateEvent);
        await hubContext.Clients.Group($"following_{updateEvent.SourceUserId}").SendAsync("FollowedUserPostUpdated", updateEvent);

        // Special notification for price drops
        if (updateEvent.OldPrice.HasValue && updateEvent.Price.HasValue && updateEvent.Price < updateEvent.OldPrice)
        {
            await hubContext.Clients.Group("global_feed").SendAsync("PriceDrop", updateEvent);
        }
    }

    /// <summary>
    /// Broadcast a new product listing
    /// </summary>
    public static async Task BroadcastNewProduct(IHubContext<ContentHub> hubContext, NewProductEvent productEvent)
    {
        await hubContext.Clients.Group("global_feed").SendAsync("NewProduct", productEvent);
        await hubContext.Clients.Group($"following_{productEvent.SourceUserId}").SendAsync("FollowedUserProduct", productEvent);

        // Category-specific notification
        if (productEvent.Product?.Category != null)
        {
            await hubContext.Clients.Group($"category_{productEvent.Product.Category}").SendAsync("NewCategoryProduct", productEvent);
        }
    }

    /// <summary>
    /// Broadcast an image upload event
    /// </summary>
    public static async Task BroadcastImageUpload(IHubContext<ContentHub> hubContext, ImageUploadEvent imageEvent)
    {
        // Notify followers
        await hubContext.Clients.Group($"following_{imageEvent.SourceUserId}").SendAsync("ImageUploaded", imageEvent);

        // If related to a specific entity, notify watchers
        if (!string.IsNullOrEmpty(imageEvent.RelatedEntityId))
        {
            if (imageEvent.RelatedEntityType == "auction")
            {
                await hubContext.Clients.Group($"auction_{imageEvent.RelatedEntityId}").SendAsync("AuctionImageAdded", imageEvent);
            }
        }
    }

    /// <summary>
    /// Broadcast a reaction event
    /// </summary>
    public static async Task BroadcastReaction(IHubContext<ContentHub> hubContext, ReactionEvent reactionEvent, string postOwnerId)
    {
        // Notify the post owner
        await hubContext.Clients.Group($"user_{postOwnerId}").SendAsync("NewReaction", reactionEvent);

        // Notify others viewing the post
        await hubContext.Clients.Group($"post_{reactionEvent.PostId}").SendAsync("ReactionUpdate", reactionEvent);
    }

    /// <summary>
    /// Broadcast a comment event
    /// </summary>
    public static async Task BroadcastComment(IHubContext<ContentHub> hubContext, CommentEvent commentEvent, string postOwnerId)
    {
        // Notify the post owner
        await hubContext.Clients.Group($"user_{postOwnerId}").SendAsync("NewComment", commentEvent);

        // Notify others viewing the post
        await hubContext.Clients.Group($"post_{commentEvent.PostId}").SendAsync("CommentAdded", commentEvent);

        // Notify parent comment author if it's a reply
        if (!string.IsNullOrEmpty(commentEvent.ParentCommentId))
        {
            await hubContext.Clients.Group($"user_{postOwnerId}").SendAsync("ReplyToComment", commentEvent);
        }
    }

    /// <summary>
    /// Broadcast presence update
    /// </summary>
    public static async Task BroadcastPresenceUpdate(IHubContext<ContentHub> hubContext, PresenceUpdateEvent presenceEvent)
    {
        // Notify followers
        if (_userFollowers.TryGetValue(presenceEvent.UserId, out var followers))
        {
            foreach (var followerId in followers)
            {
                await hubContext.Clients.Group($"user_{followerId}").SendAsync("UserPresenceChanged", presenceEvent);
            }
        }
    }

    /// <summary>
    /// Send a feed update to a specific user
    /// </summary>
    public static async Task SendFeedUpdate(IHubContext<ContentHub> hubContext, string userId, FeedItemDto feedItem)
    {
        await hubContext.Clients.Group($"user_{userId}").SendAsync("FeedUpdate", feedItem);
    }

    /// <summary>
    /// Broadcast feed items to all connected users
    /// </summary>
    public static async Task BroadcastFeedItem(IHubContext<ContentHub> hubContext, FeedItemDto feedItem)
    {
        await hubContext.Clients.Group("global_feed").SendAsync("FeedItem", feedItem);
    }

    #endregion

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
