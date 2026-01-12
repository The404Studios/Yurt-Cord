using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.Services;

public interface IApiService
{
    string? AuthToken { get; }
    bool IsAuthenticated { get; }

    // Authentication
    Task<AuthResponseDto?> LoginAsync(string username, string password);
    Task<AuthResponseDto?> RegisterAsync(string username, string email, string password);
    Task LogoutAsync();
    Task<bool> ValidateTokenAsync(string token);

    // User Profile
    Task<UserProfileDto?> GetCurrentUserAsync();
    Task<UserProfileDto?> GetUserProfileAsync(string userId);
    Task<bool> UpdateProfileAsync(UpdateProfileDto profile);

    // Chat
    Task<List<ChannelDto>> GetChannelsAsync();
    Task<List<MessageDto>> GetMessagesAsync(string channelId, int page = 1, int pageSize = 50);
    Task<MessageDto?> SendMessageAsync(string channelId, string content);

    // Friends
    Task<List<FriendDto>> GetFriendsAsync();
    Task<List<FriendRequestDto>> GetFriendRequestsAsync();
    Task<bool> SendFriendRequestAsync(string userId);
    Task<bool> AcceptFriendRequestAsync(string requestId);
    Task<bool> DeclineFriendRequestAsync(string requestId);

    // Direct Messages
    Task<List<DirectMessageDto>> GetDirectMessagesAsync(string friendId, int page = 1);
    Task<DirectMessageDto?> SendDirectMessageAsync(string friendId, string content);

    // Marketplace
    Task<ProductListResponseDto> GetProductsAsync(int page = 1, string? category = null, string? search = null);
    Task<ProductDto?> GetProductAsync(string productId);
    Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10);
    Task<List<ProductDto>> GetTrendingProductsAsync(int count = 10);
}
