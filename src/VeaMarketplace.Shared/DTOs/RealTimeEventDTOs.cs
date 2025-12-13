using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Shared.DTOs;

/// <summary>
/// Real-time event types for content updates
/// </summary>
public enum RealTimeEventType
{
    // Profile Events
    ProfilePictureUpdated,
    BannerUpdated,
    ProfileUpdated,
    StatusUpdated,
    PresenceChanged,

    // Content Events
    NewPost,
    PostUpdated,
    PostDeleted,
    PostLiked,
    PostUnliked,

    // Product Events
    NewProduct,
    ProductUpdated,
    ProductDeleted,
    ProductPriceChanged,
    ProductSold,

    // Auction Events
    NewAuction,
    AuctionBidPlaced,
    AuctionUpdated,
    AuctionEnding,
    AuctionEnded,

    // Image Events
    ImageUploaded,
    GalleryUpdated,

    // Social Events
    NewFollower,
    NewComment,
    NewReaction
}

/// <summary>
/// Base class for all real-time events
/// </summary>
public class RealTimeEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public RealTimeEventType Type { get; set; }
    public string SourceUserId { get; set; } = string.Empty;
    public string SourceUsername { get; set; } = string.Empty;
    public string? SourceAvatarUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event when a user's profile picture is updated
/// </summary>
public class ProfilePictureUpdateEvent : RealTimeEvent
{
    public string UserId { get; set; } = string.Empty;
    public string? OldAvatarUrl { get; set; }
    public string NewAvatarUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }

    public ProfilePictureUpdateEvent()
    {
        Type = RealTimeEventType.ProfilePictureUpdated;
    }
}

/// <summary>
/// Event when a user's banner is updated
/// </summary>
public class BannerUpdateEvent : RealTimeEvent
{
    public string UserId { get; set; } = string.Empty;
    public string? OldBannerUrl { get; set; }
    public string NewBannerUrl { get; set; } = string.Empty;

    public BannerUpdateEvent()
    {
        Type = RealTimeEventType.BannerUpdated;
    }
}

/// <summary>
/// Event when a new post/listing is created
/// </summary>
public class NewPostEvent : RealTimeEvent
{
    public string PostId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImageUrl { get; set; }
    public PostContentType ContentType { get; set; }
    public decimal? Price { get; set; }
    public bool IsAuction { get; set; }
    public DateTime? AuctionEndsAt { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();

    public NewPostEvent()
    {
        Type = RealTimeEventType.NewPost;
    }
}

/// <summary>
/// Type of post content
/// </summary>
public enum PostContentType
{
    Product,
    Auction,
    Service,
    DigitalProduct,
    Bundle,
    StatusUpdate,
    Image,
    Video
}

/// <summary>
/// Event when a post is updated
/// </summary>
public class PostUpdateEvent : RealTimeEvent
{
    public string PostId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? OldPrice { get; set; }
    public List<string>? ImageUrls { get; set; }
    public string? Status { get; set; }
    public Dictionary<string, object> ChangedFields { get; set; } = new();

    public PostUpdateEvent()
    {
        Type = RealTimeEventType.PostUpdated;
    }
}

/// <summary>
/// Event when a new product is listed
/// </summary>
public class NewProductEvent : RealTimeEvent
{
    public ProductDto Product { get; set; } = new();
    public string? ShareLink { get; set; }

    public NewProductEvent()
    {
        Type = RealTimeEventType.NewProduct;
    }
}

/// <summary>
/// Event when a bid is placed on an auction
/// </summary>
public class AuctionBidEvent : RealTimeEvent
{
    public string AuctionId { get; set; } = string.Empty;
    public string AuctionTitle { get; set; } = string.Empty;
    public string BidderId { get; set; } = string.Empty;
    public string BidderUsername { get; set; } = string.Empty;
    public string? BidderAvatarUrl { get; set; }
    public decimal BidAmount { get; set; }
    public decimal PreviousBid { get; set; }
    public DateTime? AuctionEndsAt { get; set; }
    public int TotalBids { get; set; }

    public AuctionBidEvent()
    {
        Type = RealTimeEventType.AuctionBidPlaced;
    }
}

/// <summary>
/// Event when an auction is ending soon or has ended
/// </summary>
public class AuctionEndingEvent : RealTimeEvent
{
    public string AuctionId { get; set; } = string.Empty;
    public string AuctionTitle { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal CurrentBid { get; set; }
    public DateTime EndsAt { get; set; }
    public int MinutesRemaining { get; set; }
    public bool HasEnded { get; set; }
    public string? WinnerId { get; set; }
    public string? WinnerUsername { get; set; }

    public AuctionEndingEvent()
    {
        Type = RealTimeEventType.AuctionEnding;
    }
}

/// <summary>
/// Event when an image is uploaded to a gallery or post
/// </summary>
public class ImageUploadEvent : RealTimeEvent
{
    public string ImageId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public ImageUploadEvent()
    {
        Type = RealTimeEventType.ImageUploaded;
    }
}

/// <summary>
/// Event when a post receives a reaction
/// </summary>
public class ReactionEvent : RealTimeEvent
{
    public string PostId { get; set; } = string.Empty;
    public string ReactorId { get; set; } = string.Empty;
    public string ReactorUsername { get; set; } = string.Empty;
    public string? ReactorAvatarUrl { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public bool IsRemoved { get; set; }
    public int TotalReactions { get; set; }

    public ReactionEvent()
    {
        Type = RealTimeEventType.NewReaction;
    }
}

/// <summary>
/// Event when a comment is added to a post
/// </summary>
public class CommentEvent : RealTimeEvent
{
    public string CommentId { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string CommenterId { get; set; } = string.Empty;
    public string CommenterUsername { get; set; } = string.Empty;
    public string? CommenterAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ParentCommentId { get; set; }
    public int TotalComments { get; set; }

    public CommentEvent()
    {
        Type = RealTimeEventType.NewComment;
    }
}

/// <summary>
/// Event when a user follows another user
/// </summary>
public class FollowEvent : RealTimeEvent
{
    public string FollowerId { get; set; } = string.Empty;
    public string FollowerUsername { get; set; } = string.Empty;
    public string? FollowerAvatarUrl { get; set; }
    public string FollowedUserId { get; set; } = string.Empty;
    public int NewFollowerCount { get; set; }

    public FollowEvent()
    {
        Type = RealTimeEventType.NewFollower;
    }
}

/// <summary>
/// Event for presence/status changes
/// </summary>
public class PresenceUpdateEvent : RealTimeEvent
{
    public string UserId { get; set; } = string.Empty;
    public UserPresenceStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public CustomStatusDto? CustomStatus { get; set; }
    public RichPresenceDto? RichPresence { get; set; }

    public PresenceUpdateEvent()
    {
        Type = RealTimeEventType.PresenceChanged;
    }
}

/// <summary>
/// Subscription request for real-time updates
/// </summary>
public class ContentSubscription
{
    public string UserId { get; set; } = string.Empty;
    public List<string> FollowedUserIds { get; set; } = new();
    public List<string> WatchedAuctionIds { get; set; } = new();
    public List<string> WatchedProductIds { get; set; } = new();
    public List<ProductCategory> InterestedCategories { get; set; } = new();
    public bool ReceiveAllPublicPosts { get; set; } = true;
    public bool ReceiveAuctionUpdates { get; set; } = true;
    public bool ReceivePriceDrops { get; set; } = true;
}

/// <summary>
/// Feed item for real-time feed updates
/// </summary>
public class FeedItemDto
{
    public string Id { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorDisplayName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public UserRole AuthorRole { get; set; }
    public UserRank AuthorRank { get; set; }

    public PostContentType ContentType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? ThumbnailUrl { get; set; }

    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsAuction { get; set; }
    public decimal? CurrentBid { get; set; }
    public DateTime? AuctionEndsAt { get; set; }

    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public int ViewCount { get; set; }

    public bool HasLiked { get; set; }
    public bool HasSaved { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string ShareLink => $"vea://marketplace/{ContentType.ToString().ToLower()}/{Id}";
}
