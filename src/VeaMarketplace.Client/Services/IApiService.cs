using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Client.Services;

public interface IApiService : IDisposable
{
    string? AuthToken { get; }
    UserDto? CurrentUser { get; }
    bool IsAuthenticated { get; }

    Task<AuthResponse> LoginAsync(string username, string password);
    Task<AuthResponse> RegisterAsync(string username, string email, string password);
    Task<bool> ValidateTokenAsync();
    void Logout();

    Task<ProductListResponse> GetProductsAsync(int page = 1, ProductCategory? category = null, string? search = null);
    Task<ProductDto?> GetProductAsync(string productId);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto?> UpdateProductAsync(string productId, UpdateProductRequest request);
    Task<bool> PurchaseProductAsync(string productId);
    Task<List<ProductDto>> GetMyProductsAsync();

    Task<UserDto?> GetUserAsync(string userId);
    Task<UserDto?> GetUserProfileAsync(string userId);
    Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request);
    Task<List<CustomRoleDto>> GetUserRolesAsync(string userId);
    Task<List<CustomRoleDto>> GetAllRolesAsync();
    Task<List<UserSearchResultDto>> SearchUsersAsync(string query);
    Task<UserSearchResultDto?> LookupUserAsync(string query);

    // Notifications
    Task<List<NotificationDto>> GetNotificationsAsync();
    Task<bool> MarkNotificationReadAsync(string notificationId);
    Task<bool> MarkAllNotificationsReadAsync();
    Task<bool> DeleteNotificationAsync(string notificationId);

    // Wishlist
    Task<List<WishlistItemDto>> GetWishlistAsync();
    Task<bool> AddToWishlistAsync(string productId);
    Task<bool> RemoveFromWishlistAsync(string productId);
    Task<bool> ClearWishlistAsync();

    // Orders
    Task<List<OrderDto>> GetOrdersAsync();
    Task<OrderDto?> GetOrderAsync(string orderId);

    // Reviews
    Task<ProductReviewListDto> GetProductReviewsAsync(string productId, int page = 1);
    Task<ProductReviewDto> CreateReviewAsync(CreateReviewRequest request);
    Task<bool> MarkReviewHelpfulAsync(string reviewId);
    Task<bool> ReportReviewAsync(string reviewId, string reason);

    // Moderation
    Task<ModerationDashboardDto> GetModerationDashboardAsync();
    Task<List<UserBanDto>> GetBannedUsersAsync();
    Task<List<MessageReportDto>> GetPendingReportsAsync();
    Task<bool> BanUserAsync(BanUserRequest request);
    Task<bool> UnbanUserAsync(string banId, string reason);
    Task<bool> WarnUserAsync(WarnUserRequest request);
    Task<bool> MuteUserAsync(MuteUserRequest request);
    Task<bool> UnmuteUserAsync(string muteId, string reason);
    Task<bool> DismissReportAsync(string reportId);
    Task<bool> ResolveReportAsync(string reportId, string resolution);
    Task<bool> DeleteMessageAsync(string messageId, string reason);

    // Media Upload
    Task<string> UploadImageAsync(string filePath);
    Task<List<string>> UploadImagesAsync(IEnumerable<string> filePaths);

    // Cart
    Task<CartDto> GetCartAsync();
    Task<CartDto> AddToCartAsync(AddToCartRequest request);
    Task<CartDto> UpdateCartItemAsync(UpdateCartItemRequest request);
    Task<bool> RemoveFromCartAsync(string itemId);
    Task<bool> ClearCartAsync();
    Task<CouponResultDto> ApplyCouponAsync(string couponCode);
    Task<bool> RemoveCouponAsync();
    Task<CheckoutResultDto> CheckoutAsync(CheckoutRequest request);

    // Presence & Status
    Task<UserPresenceDto?> GetPresenceAsync(string userId);
    Task<bool> UpdatePresenceAsync(UpdatePresenceRequest request);
    Task<bool> SetCustomStatusAsync(SetCustomStatusRequest request);
    Task<bool> ClearCustomStatusAsync();

    // Products - additional
    Task<bool> DeleteProductAsync(string productId);
    Task<bool> LikeProductAsync(string productId);
    Task<CartDto> AddToCartAsync(string productId, int quantity);

    // Activity Feed
    Task<List<UserActivityDto>> GetActivityFeedAsync(string? filter = null, int page = 1, int pageSize = 20);
    Task<List<UserActivityDto>> GetFollowingActivityFeedAsync(int page = 1, int pageSize = 20);
    Task<List<UserActivityDto>> GetActivityFeedByTypeAsync(VeaMarketplace.Shared.Models.ActivityType type, int page = 1, int pageSize = 20);

    // Following
    Task<bool> FollowUserAsync(string userId);
    Task<bool> UnfollowUserAsync(string userId);
    Task<FollowStatusDto> GetFollowStatusAsync(string userId);

    // Discovery
    Task<List<SellerProfileDto>> GetTopSellersAsync(int count = 4);
    Task<List<ProductDto>> GetTrendingProductsAsync(int count = 8);
    Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10);
    Task<List<ProductDto>> GetNewArrivalsAsync(int count = 20);
    Task<List<ProductDto>> GetRecommendedProductsAsync(int count = 20);
    Task<List<ProductDto>> GetSimilarProductsAsync(string productId, int count = 10);

    // Admin API
    Task<AdminServerStatsDto?> GetAdminServerStatsAsync();
    Task<int> GetAdminOnlineCountAsync();
    Task<bool> AdminSendBroadcastAsync(string message, string? channel = null);
    Task<bool> AdminKickUserAsync(string userId, string reason);
    Task<bool> AdminPromoteUserAsync(string userId, UserRole newRole);
    Task<bool> AdminDemoteUserAsync(string userId, UserRole newRole);

    // Reporting API
    Task<ProductReportDto?> ReportProductAsync(string productId, ProductReportReason reason, string? details = null);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private static readonly string BaseUrl = AppConstants.DefaultServerUrl;
    private bool _disposed;

    // Shared JSON options for consistent enum serialization with server
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Simple in-memory cache with expiration for reducing API calls
    private readonly Dictionary<string, (object Data, DateTime Expiry)> _cache = new();
    private readonly object _cacheLock = new();

    // Cache durations for different data types
    private static readonly TimeSpan ShortCacheDuration = TimeSpan.FromSeconds(30); // Volatile data (cart, wishlist)
    private static readonly TimeSpan MediumCacheDuration = TimeSpan.FromMinutes(2); // Semi-static (products)
    private static readonly TimeSpan LongCacheDuration = TimeSpan.FromMinutes(10); // Static data (user profiles)

    public string? AuthToken { get; private set; }
    public UserDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && CurrentUser != null;

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10) // Fail fast if server unreachable
        };
        Debug.WriteLine($"[ApiService] Created with BaseUrl: {BaseUrl}");
    }

    #region Cache Helpers

    private T? GetCached<T>(string key) where T : class
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
            {
                return entry.Data as T;
            }
            // Remove expired entry
            _cache.Remove(key);
            return null;
        }
    }

    private void SetCache<T>(string key, T data, TimeSpan duration) where T : class
    {
        lock (_cacheLock)
        {
            _cache[key] = (data, DateTime.UtcNow + duration);
        }
    }

    private void InvalidateCache(string keyPrefix)
    {
        lock (_cacheLock)
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
    }

    private void ClearAllCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }

    #endregion

    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        Debug.WriteLine($"[ApiService] LoginAsync called for {username}");
        Debug.WriteLine($"[ApiService] Target URL: {BaseUrl}/api/auth/login");

        var request = new LoginRequest { Username = username, Password = password };

        HttpResponseMessage response;
        try
        {
            Debug.WriteLine("[ApiService] Sending HTTP POST...");
            response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, JsonOptions).ConfigureAwait(false);
            Debug.WriteLine($"[ApiService] Got response: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ApiService] HTTP Error: {ex.Message}");
            return new AuthResponse { Success = false, Message = $"Cannot connect to server ({BaseUrl}): {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("[ApiService] Request timed out");
            return new AuthResponse { Success = false, Message = $"Connection timed out - server at {BaseUrl} is not responding" };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiService] Unexpected error: {ex.GetType().Name}: {ex.Message}");
            return new AuthResponse { Success = false, Message = $"Connection error: {ex.Message}" };
        }

        // Read response body as string first to handle empty/invalid responses
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new AuthResponse { Success = false, Message = "Server returned empty response" };
        }

        AuthResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return new AuthResponse { Success = false, Message = $"Server error: {(int)response.StatusCode} {response.ReasonPhrase}" };
        }

        if (result == null)
        {
            return new AuthResponse { Success = false, Message = "Invalid response from server" };
        }

        if (result.Success && result.Token != null)
        {
            AuthToken = result.Token;
            CurrentUser = result.User;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
        }

        return result;
    }

    public async Task<AuthResponse> RegisterAsync(string username, string email, string password)
    {
        Debug.WriteLine($"[ApiService] RegisterAsync called for {username}");
        Debug.WriteLine($"[ApiService] Target URL: {BaseUrl}/api/auth/register");

        var request = new RegisterRequest { Username = username, Email = email, Password = password };

        HttpResponseMessage response;
        try
        {
            Debug.WriteLine("[ApiService] Sending HTTP POST...");
            response = await _httpClient.PostAsJsonAsync("/api/auth/register", request, JsonOptions).ConfigureAwait(false);
            Debug.WriteLine($"[ApiService] Got response: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ApiService] HTTP Error: {ex.Message}");
            return new AuthResponse { Success = false, Message = $"Cannot connect to server ({BaseUrl}): {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("[ApiService] Request timed out");
            return new AuthResponse { Success = false, Message = $"Connection timed out - server at {BaseUrl} is not responding" };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiService] Unexpected error: {ex.GetType().Name}: {ex.Message}");
            return new AuthResponse { Success = false, Message = $"Connection error: {ex.Message}" };
        }

        // Read response body as string first to handle empty/invalid responses
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new AuthResponse { Success = false, Message = "Server returned empty response" };
        }

        AuthResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return new AuthResponse { Success = false, Message = $"Server error: {(int)response.StatusCode} {response.ReasonPhrase}" };
        }

        if (result == null)
        {
            return new AuthResponse { Success = false, Message = "Invalid response from server" };
        }

        if (result.Success && result.Token != null)
        {
            AuthToken = result.Token;
            CurrentUser = result.User;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
        }

        return result;
    }

    public async Task<bool> ValidateTokenAsync()
    {
        if (string.IsNullOrEmpty(AuthToken)) return false;

        try
        {
            var response = await _httpClient.GetAsync("/api/auth/validate").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                CurrentUser = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions).ConfigureAwait(false);
                return CurrentUser != null;
            }
        }
        catch
        {
            // Token validation failed
        }

        return false;
    }

    public void Logout()
    {
        AuthToken = null;
        CurrentUser = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        ClearAllCache(); // Clear all cached data on logout
    }

    public async Task<ProductListResponse> GetProductsAsync(int page = 1, ProductCategory? category = null, string? search = null)
    {
        var url = $"/api/products?page={page}";
        if (category.HasValue) url += $"&category={category}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductListResponse>(JsonOptions).ConfigureAwait(false) ?? new ProductListResponse();
    }

    public async Task<ProductDto?> GetProductAsync(string productId)
    {
        var cacheKey = $"product:{productId}";
        var cached = GetCached<ProductDto>(cacheKey);
        if (cached != null) return cached;

        var response = await _httpClient.GetAsync($"/api/products/{productId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions).ConfigureAwait(false);
        if (product != null)
        {
            SetCache(cacheKey, product, MediumCacheDuration);
        }
        return product;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/products", request, JsonOptions).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions).ConfigureAwait(false) ?? new ProductDto();
    }

    public async Task<ProductDto?> UpdateProductAsync(string productId, UpdateProductRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/products/{productId}", request, JsonOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<bool> PurchaseProductAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/products/{productId}/purchase", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProductDto>> GetMyProductsAsync()
    {
        var response = await _httpClient.GetAsync("/api/products/my").ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<UserDto?> GetUserAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<UserDto?> GetUserProfileAsync(string userId)
    {
        var cacheKey = $"profile:{userId}";
        var cached = GetCached<UserDto>(cacheKey);
        if (cached != null) return cached;

        var response = await _httpClient.GetAsync($"/api/users/{userId}/profile").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var profile = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions).ConfigureAwait(false);
        if (profile != null)
        {
            SetCache(cacheKey, profile, LongCacheDuration);
        }
        return profile;
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query)
    {
        var response = await _httpClient.GetAsync($"/api/users/search?query={Uri.EscapeDataString(query)}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserSearchResultDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<UserSearchResultDto?> LookupUserAsync(string query)
    {
        var response = await _httpClient.GetAsync($"/api/users/lookup?query={Uri.EscapeDataString(query)}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/users/profile", request, JsonOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions).ConfigureAwait(false);
        if (user != null)
        {
            CurrentUser = user;
            // Invalidate cached profile so fresh data is fetched next time
            InvalidateCache($"profile:{user.Id}");
        }
        return user;
    }

    public async Task<List<CustomRoleDto>> GetUserRolesAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}/roles").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CustomRoleDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<CustomRoleDto>> GetAllRolesAsync()
    {
        var response = await _httpClient.GetAsync("/api/roles").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CustomRoleDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    // Notifications
    public async Task<List<NotificationDto>> GetNotificationsAsync()
    {
        var response = await _httpClient.GetAsync("/api/notifications").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<NotificationDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<bool> MarkNotificationReadAsync(string notificationId)
    {
        var response = await _httpClient.PostAsync($"/api/notifications/{notificationId}/read", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MarkAllNotificationsReadAsync()
    {
        var response = await _httpClient.PostAsync("/api/notifications/read-all", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteNotificationAsync(string notificationId)
    {
        var response = await _httpClient.DeleteAsync($"/api/notifications/{notificationId}").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Wishlist
    public async Task<List<WishlistItemDto>> GetWishlistAsync()
    {
        var cacheKey = "wishlist";
        var cached = GetCached<List<WishlistItemDto>>(cacheKey);
        if (cached != null) return cached;

        var response = await _httpClient.GetAsync("/api/wishlist").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];

        var wishlist = await response.Content.ReadFromJsonAsync<List<WishlistItemDto>>(JsonOptions).ConfigureAwait(false) ?? [];
        SetCache(cacheKey, wishlist, ShortCacheDuration);
        return wishlist;
    }

    public async Task<bool> AddToWishlistAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/wishlist/{productId}", null).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache("wishlist"); // Invalidate wishlist cache on modification
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveFromWishlistAsync(string productId)
    {
        var response = await _httpClient.DeleteAsync($"/api/wishlist/{productId}").ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache("wishlist"); // Invalidate wishlist cache on modification
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearWishlistAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/wishlist").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Orders
    public async Task<List<OrderDto>> GetOrdersAsync()
    {
        var response = await _httpClient.GetAsync("/api/orders").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<OrderDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<OrderDto?> GetOrderAsync(string orderId)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{orderId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).ConfigureAwait(false);
    }

    // Reviews
    public async Task<ProductReviewListDto> GetProductReviewsAsync(string productId, int page = 1)
    {
        var response = await _httpClient.GetAsync($"/api/products/{productId}/reviews?page={page}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new ProductReviewListDto();
        return await response.Content.ReadFromJsonAsync<ProductReviewListDto>(JsonOptions).ConfigureAwait(false) ?? new ProductReviewListDto();
    }

    public async Task<ProductReviewDto> CreateReviewAsync(CreateReviewRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/reviews", request, JsonOptions).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductReviewDto>(JsonOptions).ConfigureAwait(false) ?? new ProductReviewDto();
    }

    public async Task<bool> MarkReviewHelpfulAsync(string reviewId)
    {
        var response = await _httpClient.PostAsync($"/api/reviews/{reviewId}/helpful", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReportReviewAsync(string reviewId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/reviews/{reviewId}/report", new { Reason = reason }, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Moderation
    public async Task<ModerationDashboardDto> GetModerationDashboardAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/dashboard").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new ModerationDashboardDto();
        return await response.Content.ReadFromJsonAsync<ModerationDashboardDto>(JsonOptions).ConfigureAwait(false) ?? new ModerationDashboardDto();
    }

    public async Task<List<UserBanDto>> GetBannedUsersAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/bans").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserBanDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<MessageReportDto>> GetPendingReportsAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/reports?status=pending").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<MessageReportDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<bool> BanUserAsync(BanUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/bans", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnbanUserAsync(string banId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/bans/{banId}/unban", new { Reason = reason }, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> WarnUserAsync(WarnUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/warnings", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MuteUserAsync(MuteUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/mutes", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnmuteUserAsync(string muteId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/mutes/{muteId}/unmute", new { Reason = reason }, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DismissReportAsync(string reportId)
    {
        var response = await _httpClient.PostAsync($"/api/moderation/reports/{reportId}/dismiss", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResolveReportAsync(string reportId, string resolution)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/reports/{reportId}/resolve", new { Resolution = resolution }, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMessageAsync(string messageId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/messages/{messageId}/delete", new { Reason = reason }, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Media Upload
    public async Task<string> UploadImageAsync(string filePath)
    {
        using var form = new MultipartFormDataContent();
        using var fileStream = System.IO.File.OpenRead(filePath);
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(filePath));
        form.Add(streamContent, "file", System.IO.Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync("/api/media/upload", form).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return string.Empty;

        var result = await response.Content.ReadFromJsonAsync<MediaUploadResponse>(JsonOptions).ConfigureAwait(false);
        return result?.Url ?? string.Empty;
    }

    public async Task<List<string>> UploadImagesAsync(IEnumerable<string> filePaths)
    {
        var urls = new List<string>();
        foreach (var filePath in filePaths)
        {
            var url = await UploadImageAsync(filePath).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(url))
                urls.Add(url);
        }
        return urls;
    }

    private static string GetMimeType(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    // Cart - with caching and auto-invalidation
    public async Task<CartDto> GetCartAsync()
    {
        var cacheKey = "cart";
        var cached = GetCached<CartDto>(cacheKey);
        if (cached != null) return cached;

        var response = await _httpClient.GetAsync("/api/cart").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new CartDto();

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(JsonOptions).ConfigureAwait(false) ?? new CartDto();
        SetCache(cacheKey, cart, ShortCacheDuration);
        return cart;
    }

    public async Task<CartDto> AddToCartAsync(AddToCartRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/cart/items", request, JsonOptions).ConfigureAwait(false);
        InvalidateCache("cart"); // Invalidate cart cache on modification
        if (!response.IsSuccessStatusCode) return new CartDto();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>(JsonOptions).ConfigureAwait(false) ?? new CartDto();
        SetCache("cart", cart, ShortCacheDuration); // Cache the updated cart
        return cart;
    }

    public async Task<CartDto> UpdateCartItemAsync(UpdateCartItemRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/cart/items/{request.ItemId}", request, JsonOptions).ConfigureAwait(false);
        InvalidateCache("cart"); // Invalidate cart cache on modification
        if (!response.IsSuccessStatusCode) return new CartDto();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>(JsonOptions).ConfigureAwait(false) ?? new CartDto();
        SetCache("cart", cart, ShortCacheDuration); // Cache the updated cart
        return cart;
    }

    public async Task<bool> RemoveFromCartAsync(string itemId)
    {
        var response = await _httpClient.DeleteAsync($"/api/cart/items/{itemId}").ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache("cart"); // Invalidate cart cache on modification
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearCartAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/cart").ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache("cart"); // Invalidate cart cache on modification
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<CouponResultDto> ApplyCouponAsync(string couponCode)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/cart/coupon", new ApplyCouponRequest { CouponCode = couponCode }, JsonOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new CouponResultDto { IsValid = false, ErrorMessage = "Failed to apply coupon" };
        return await response.Content.ReadFromJsonAsync<CouponResultDto>(JsonOptions).ConfigureAwait(false) ?? new CouponResultDto();
    }

    public async Task<bool> RemoveCouponAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/cart/coupon").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<CheckoutResultDto> CheckoutAsync(CheckoutRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/cart/checkout", request, JsonOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new CheckoutResultDto { Success = false, ErrorMessage = error };
        }
        return await response.Content.ReadFromJsonAsync<CheckoutResultDto>(JsonOptions).ConfigureAwait(false) ?? new CheckoutResultDto();
    }

    // Presence & Status
    public async Task<UserPresenceDto?> GetPresenceAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}/presence").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserPresenceDto>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<bool> UpdatePresenceAsync(UpdatePresenceRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/users/me/presence", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetCustomStatusAsync(SetCustomStatusRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/users/me/status", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ClearCustomStatusAsync()
    {
        var response = await _httpClient.DeleteAsync("/api/users/me/status").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Products - additional
    public async Task<bool> DeleteProductAsync(string productId)
    {
        var response = await _httpClient.DeleteAsync($"/api/products/{productId}").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LikeProductAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/products/{productId}/like", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<CartDto> AddToCartAsync(string productId, int quantity)
    {
        var request = new AddToCartRequest { ProductId = productId, Quantity = quantity };
        return await AddToCartAsync(request).ConfigureAwait(false);
    }

    // Activity Feed
    public async Task<List<UserActivityDto>> GetActivityFeedAsync(string? filter = null, int page = 1, int pageSize = 20)
    {
        var url = $"/api/activity?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(filter))
            url += $"&filter={Uri.EscapeDataString(filter)}";

        var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserActivityDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<UserActivityDto>> GetFollowingActivityFeedAsync(int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"/api/activity/following?page={page}&pageSize={pageSize}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserActivityDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<UserActivityDto>> GetActivityFeedByTypeAsync(VeaMarketplace.Shared.Models.ActivityType type, int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"/api/activity?type={type}&page={page}&pageSize={pageSize}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserActivityDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    // Following
    public async Task<bool> FollowUserAsync(string userId)
    {
        var response = await _httpClient.PostAsync($"/api/activity/follow/{userId}", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnfollowUserAsync(string userId)
    {
        var response = await _httpClient.DeleteAsync($"/api/activity/follow/{userId}").ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<FollowStatusDto> GetFollowStatusAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/activity/follow/{userId}/status").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new FollowStatusDto();
        return await response.Content.ReadFromJsonAsync<FollowStatusDto>(JsonOptions).ConfigureAwait(false) ?? new FollowStatusDto();
    }

    // Discovery
    public async Task<List<SellerProfileDto>> GetTopSellersAsync(int count = 4)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/top-sellers?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<SellerProfileDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<ProductDto>> GetTrendingProductsAsync(int count = 8)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/trending?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/featured?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<ProductDto>> GetNewArrivalsAsync(int count = 20)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/new?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<ProductDto>> GetRecommendedProductsAsync(int count = 20)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/recommended?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    public async Task<List<ProductDto>> GetSimilarProductsAsync(string productId, int count = 10)
    {
        var response = await _httpClient.GetAsync($"/api/discovery/similar/{productId}?limit={count}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>(JsonOptions).ConfigureAwait(false) ?? [];
    }

    // Admin API
    public async Task<AdminServerStatsDto?> GetAdminServerStatsAsync()
    {
        var response = await _httpClient.GetAsync("/api/admin/stats").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AdminServerStatsDto>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<int> GetAdminOnlineCountAsync()
    {
        var response = await _httpClient.GetAsync("/api/admin/online-count").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return 0;
        return await response.Content.ReadFromJsonAsync<int>(JsonOptions).ConfigureAwait(false);
    }

    public async Task<bool> AdminSendBroadcastAsync(string message, string? channel = null)
    {
        var request = new { Message = message, Channel = channel };
        var response = await _httpClient.PostAsJsonAsync("/api/admin/broadcast", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AdminKickUserAsync(string userId, string reason)
    {
        var request = new { UserId = userId, Reason = reason };
        var response = await _httpClient.PostAsJsonAsync("/api/admin/kick", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AdminPromoteUserAsync(string userId, UserRole newRole)
    {
        var request = new { UserId = userId, NewRole = newRole };
        var response = await _httpClient.PostAsJsonAsync("/api/admin/promote", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AdminDemoteUserAsync(string userId, UserRole newRole)
    {
        var request = new { UserId = userId, NewRole = newRole };
        var response = await _httpClient.PostAsJsonAsync("/api/admin/demote", request, JsonOptions).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Reporting API
    public async Task<ProductReportDto?> ReportProductAsync(string productId, ProductReportReason reason, string? details = null)
    {
        var request = new ReportProductRequest
        {
            ProductId = productId,
            Reason = reason,
            AdditionalInfo = details
        };
        var response = await _httpClient.PostAsJsonAsync($"/api/products/{productId}/report", request, JsonOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductReportDto>(JsonOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the HttpClient to release network resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }
}

public class MediaUploadResponse
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}
