using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Interface for real-time content updates (posts, products, auctions, images)
/// </summary>
public interface IContentService
{
    bool IsConnected { get; }
    ContentSubscription? CurrentSubscription { get; }
    ObservableCollection<FeedItemDto> FeedItems { get; }

    // Profile picture/banner updates
    event Action<ProfilePictureUpdateEvent>? OnProfilePictureUpdated;
    event Action<BannerUpdateEvent>? OnBannerUpdated;

    // Post events
    event Action<NewPostEvent>? OnNewPost;
    event Action<PostUpdateEvent>? OnPostUpdated;
    event Action<NewPostEvent>? OnFollowedUserPost;

    // Product events
    event Action<NewProductEvent>? OnNewProduct;
    event Action<NewProductEvent>? OnFollowedUserProduct;
    event Action<NewProductEvent>? OnNewCategoryProduct;
    event Action<PostUpdateEvent>? OnPriceDrop;

    // Auction events
    event Action<AuctionBidEvent>? OnAuctionBidPlaced;
    event Action<AuctionBidEvent>? OnBidOnYourAuction;
    event Action<AuctionEndingEvent>? OnAuctionEnding;
    event Action<AuctionEndingEvent>? OnAuctionEndingSoon;

    // Image events
    event Action<ImageUploadEvent>? OnImageUploaded;

    // Social events
    event Action<FollowEvent>? OnNewFollower;
    event Action<ReactionEvent>? OnNewReaction;
    event Action<ReactionEvent>? OnReactionUpdate;
    event Action<CommentEvent>? OnNewComment;
    event Action<CommentEvent>? OnCommentAdded;

    // Presence events
    event Action<PresenceUpdateEvent>? OnUserPresenceChanged;

    // Feed events
    event Action<FeedItemDto>? OnFeedUpdate;
    event Action<FeedItemDto>? OnFeedItem;

    // Connection events
    event Action? OnConnected;
    event Action<string>? OnError;

    // Connection methods
    Task ConnectAsync(string token);
    Task DisconnectAsync();

    // Subscription methods
    Task FollowUserAsync(string userId);
    Task UnfollowUserAsync(string userId);
    Task WatchAuctionAsync(string auctionId);
    Task UnwatchAuctionAsync(string auctionId);
    Task SubscribeToCategoryAsync(ProductCategory category);
    Task UpdateSubscriptionAsync(ContentSubscription subscription);
    Task<ContentSubscription?> GetSubscriptionAsync();
}

/// <summary>
/// Real-time content service for receiving live updates
/// </summary>
public class ContentService : IContentService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly INotificationService _notificationService;
    private const string HubUrl = "http://localhost:5000/hubs/content";
    private string? _authToken;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public ContentSubscription? CurrentSubscription { get; private set; }
    public ObservableCollection<FeedItemDto> FeedItems { get; } = new();

    // Profile events
    public event Action<ProfilePictureUpdateEvent>? OnProfilePictureUpdated;
    public event Action<BannerUpdateEvent>? OnBannerUpdated;

    // Post events
    public event Action<NewPostEvent>? OnNewPost;
    public event Action<PostUpdateEvent>? OnPostUpdated;
    public event Action<NewPostEvent>? OnFollowedUserPost;

    // Product events
    public event Action<NewProductEvent>? OnNewProduct;
    public event Action<NewProductEvent>? OnFollowedUserProduct;
    public event Action<NewProductEvent>? OnNewCategoryProduct;
    public event Action<PostUpdateEvent>? OnPriceDrop;

    // Auction events
    public event Action<AuctionBidEvent>? OnAuctionBidPlaced;
    public event Action<AuctionBidEvent>? OnBidOnYourAuction;
    public event Action<AuctionEndingEvent>? OnAuctionEnding;
    public event Action<AuctionEndingEvent>? OnAuctionEndingSoon;

    // Image events
    public event Action<ImageUploadEvent>? OnImageUploaded;

    // Social events
    public event Action<FollowEvent>? OnNewFollower;
    public event Action<ReactionEvent>? OnNewReaction;
    public event Action<ReactionEvent>? OnReactionUpdate;
    public event Action<CommentEvent>? OnNewComment;
    public event Action<CommentEvent>? OnCommentAdded;

    // Presence events
    public event Action<PresenceUpdateEvent>? OnUserPresenceChanged;

    // Feed events
    public event Action<FeedItemDto>? OnFeedUpdate;
    public event Action<FeedItemDto>? OnFeedItem;

    // Connection events
    public event Action? OnConnected;
    public event Action<string>? OnError;

    public ContentService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task ConnectAsync(string token)
    {
        _authToken = token;

        // Dispose existing connection if any
        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // Handle reconnection
        _connection.Reconnected += async (connectionId) =>
        {
            if (_authToken != null)
            {
                await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
            }
        };

        RegisterHandlers();
        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Connection events
        _connection.On("ContentHubConnected", () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnConnected?.Invoke();
            });
        });

        _connection.On<string>("AuthenticationFailed", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnError?.Invoke(error);
            });
        });

        // Profile picture updates - visible everywhere
        _connection.On<ProfilePictureUpdateEvent>("ProfilePictureUpdated", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnProfilePictureUpdated?.Invoke(evt);
            });
        });

        // Banner updates
        _connection.On<BannerUpdateEvent>("BannerUpdated", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnBannerUpdated?.Invoke(evt);
            });
        });

        // New post events
        _connection.On<NewPostEvent>("NewPost", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnNewPost?.Invoke(evt);
            });
        });

        _connection.On<NewPostEvent>("FollowedUserPost", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMessageSound();
                OnFollowedUserPost?.Invoke(evt);
            });
        });

        _connection.On<NewPostEvent>("CategoryPost", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnNewPost?.Invoke(evt);
            });
        });

        // Post updates
        _connection.On<PostUpdateEvent>("PostUpdated", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPostUpdated?.Invoke(evt);
            });
        });

        _connection.On<PostUpdateEvent>("FollowedUserPostUpdated", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPostUpdated?.Invoke(evt);
            });
        });

        _connection.On<PostUpdateEvent>("PriceDrop", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMessageSound();
                OnPriceDrop?.Invoke(evt);
            });
        });

        // Product events
        _connection.On<NewProductEvent>("NewProduct", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnNewProduct?.Invoke(evt);
            });
        });

        _connection.On<NewProductEvent>("FollowedUserProduct", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMessageSound();
                OnFollowedUserProduct?.Invoke(evt);
            });
        });

        _connection.On<NewProductEvent>("NewCategoryProduct", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnNewCategoryProduct?.Invoke(evt);
            });
        });

        // Auction events
        _connection.On<AuctionBidEvent>("AuctionBidPlaced", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnAuctionBidPlaced?.Invoke(evt);
            });
        });

        _connection.On<AuctionBidEvent>("BidOnYourAuction", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMentionSound();
                OnBidOnYourAuction?.Invoke(evt);
            });
        });

        _connection.On<AuctionBidEvent>("AuctionActivity", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnAuctionBidPlaced?.Invoke(evt);
            });
        });

        _connection.On<AuctionEndingEvent>("AuctionEnding", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnAuctionEnding?.Invoke(evt);
            });
        });

        _connection.On<AuctionEndingEvent>("AuctionEndingSoon", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMentionSound();
                OnAuctionEndingSoon?.Invoke(evt);
            });
        });

        // Image events
        _connection.On<ImageUploadEvent>("ImageUploaded", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnImageUploaded?.Invoke(evt);
            });
        });

        _connection.On<ImageUploadEvent>("AuctionImageAdded", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnImageUploaded?.Invoke(evt);
            });
        });

        // Social events
        _connection.On<FollowEvent>("NewFollower", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayFriendRequestSound();
                OnNewFollower?.Invoke(evt);
            });
        });

        _connection.On<ReactionEvent>("NewReaction", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnNewReaction?.Invoke(evt);
            });
        });

        _connection.On<ReactionEvent>("ReactionUpdate", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnReactionUpdate?.Invoke(evt);
            });
        });

        _connection.On<CommentEvent>("NewComment", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMessageSound();
                OnNewComment?.Invoke(evt);
            });
        });

        _connection.On<CommentEvent>("CommentAdded", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnCommentAdded?.Invoke(evt);
            });
        });

        _connection.On<CommentEvent>("ReplyToComment", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _notificationService.PlayMentionSound();
                OnNewComment?.Invoke(evt);
            });
        });

        // Presence events
        _connection.On<PresenceUpdateEvent>("UserPresenceChanged", evt =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnUserPresenceChanged?.Invoke(evt);
            });
        });

        // Feed events
        _connection.On<FeedItemDto>("FeedUpdate", item =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnFeedUpdate?.Invoke(item);
            });
        });

        _connection.On<FeedItemDto>("FeedItem", item =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                FeedItems.Insert(0, item);
                OnFeedItem?.Invoke(item);
            });
        });

        // Subscription events
        _connection.On<ContentSubscription>("SubscriptionUpdated", sub =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentSubscription = sub;
            });
        });

        _connection.On<string>("WatchingAuction", auctionId =>
        {
            // Successfully watching auction
        });
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    public async Task FollowUserAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("FollowUser", userId).ConfigureAwait(false);
    }

    public async Task UnfollowUserAsync(string userId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UnfollowUser", userId).ConfigureAwait(false);
    }

    public async Task WatchAuctionAsync(string auctionId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("WatchAuction", auctionId).ConfigureAwait(false);
    }

    public async Task UnwatchAuctionAsync(string auctionId)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UnwatchAuction", auctionId).ConfigureAwait(false);
    }

    public async Task SubscribeToCategoryAsync(ProductCategory category)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("SubscribeToCategory", category).ConfigureAwait(false);
    }

    public async Task UpdateSubscriptionAsync(ContentSubscription subscription)
    {
        if (_connection != null && IsConnected)
            await _connection.InvokeAsync("UpdateSubscription", subscription).ConfigureAwait(false);
    }

    public async Task<ContentSubscription?> GetSubscriptionAsync()
    {
        if (_connection == null || !IsConnected)
            return null;

        return await _connection.InvokeAsync<ContentSubscription?>("GetSubscription").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
