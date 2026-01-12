using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.Services;

// Type aliases for mobile compatibility
public class AuthResponseDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Message { get; set; }
}

public class UserProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? StatusMessage { get; set; }
    public string? Bio { get; set; }
    public int FriendsCount { get; set; }
    public int MessageCount { get; set; }
    public int DaysActive { get; set; }
    public bool IsOnline { get; set; }
}

public class UpdateProfileDto
{
    public string? DisplayName { get; set; }
    public string? StatusMessage { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}

public class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsEdited { get; set; }
}

public class FriendDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? StatusMessage { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
}

public class ProductListResponseDto
{
    public List<ProductDto>? Products { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
}

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
