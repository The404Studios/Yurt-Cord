using System.Net.Http;
using System.Net.Http.Json;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Services;

public interface IApiService
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
    Task<bool> UnbanUserAsync(string oderId, string reason);
    Task<bool> WarnUserAsync(WarnUserRequest request);
    Task<bool> MuteUserAsync(MuteUserRequest request);
    Task<bool> UnmuteUserAsync(string oderId, string reason);
    Task<bool> DismissReportAsync(string reportId);
    Task<bool> ResolveReportAsync(string reportId, string resolution);
    Task<bool> DeleteMessageAsync(string messageId, string reason);

    // Media Upload
    Task<string> UploadImageAsync(string filePath);
    Task<List<string>> UploadImagesAsync(IEnumerable<string> filePaths);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://162.248.94.23:5000";

    public string? AuthToken { get; private set; }
    public UserDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && CurrentUser != null;

    public ApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        var request = new LoginRequest { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>().ConfigureAwait(false) ?? new AuthResponse();

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
        var request = new RegisterRequest { Username = username, Email = email, Password = password };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>().ConfigureAwait(false) ?? new AuthResponse();

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
                CurrentUser = await response.Content.ReadFromJsonAsync<UserDto>().ConfigureAwait(false);
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
    }

    public async Task<ProductListResponse> GetProductsAsync(int page = 1, ProductCategory? category = null, string? search = null)
    {
        var url = $"/api/products?page={page}";
        if (category.HasValue) url += $"&category={category}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductListResponse>().ConfigureAwait(false) ?? new ProductListResponse();
    }

    public async Task<ProductDto?> GetProductAsync(string productId)
    {
        var response = await _httpClient.GetAsync($"/api/products/{productId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductDto>().ConfigureAwait(false);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/products", request).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductDto>().ConfigureAwait(false) ?? new ProductDto();
    }

    public async Task<bool> PurchaseProductAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/products/{productId}/purchase", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProductDto>> GetMyProductsAsync()
    {
        var response = await _httpClient.GetAsync("/api/products/my").ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<UserDto?> GetUserAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>().ConfigureAwait(false);
    }

    public async Task<UserDto?> GetUserProfileAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}/profile").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>().ConfigureAwait(false);
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query)
    {
        var response = await _httpClient.GetAsync($"/api/users/search?query={Uri.EscapeDataString(query)}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserSearchResultDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<UserSearchResultDto?> LookupUserAsync(string query)
    {
        var response = await _httpClient.GetAsync($"/api/users/lookup?query={Uri.EscapeDataString(query)}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>().ConfigureAwait(false);
    }

    public async Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/users/profile", request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<UserDto>().ConfigureAwait(false);
        if (user != null) CurrentUser = user;
        return user;
    }

    public async Task<List<CustomRoleDto>> GetUserRolesAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}/roles").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CustomRoleDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<List<CustomRoleDto>> GetAllRolesAsync()
    {
        var response = await _httpClient.GetAsync("/api/roles").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CustomRoleDto>>().ConfigureAwait(false) ?? [];
    }

    // Notifications
    public async Task<List<NotificationDto>> GetNotificationsAsync()
    {
        var response = await _httpClient.GetAsync("/api/notifications").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<NotificationDto>>().ConfigureAwait(false) ?? [];
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
        var response = await _httpClient.GetAsync("/api/wishlist").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<WishlistItemDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<bool> AddToWishlistAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/wishlist/{productId}", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveFromWishlistAsync(string productId)
    {
        var response = await _httpClient.DeleteAsync($"/api/wishlist/{productId}").ConfigureAwait(false);
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
        return await response.Content.ReadFromJsonAsync<List<OrderDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<OrderDto?> GetOrderAsync(string orderId)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{orderId}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<OrderDto>().ConfigureAwait(false);
    }

    // Reviews
    public async Task<ProductReviewListDto> GetProductReviewsAsync(string productId, int page = 1)
    {
        var response = await _httpClient.GetAsync($"/api/products/{productId}/reviews?page={page}").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new ProductReviewListDto();
        return await response.Content.ReadFromJsonAsync<ProductReviewListDto>().ConfigureAwait(false) ?? new ProductReviewListDto();
    }

    public async Task<ProductReviewDto> CreateReviewAsync(CreateReviewRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/reviews", request).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ProductReviewDto>().ConfigureAwait(false) ?? new ProductReviewDto();
    }

    public async Task<bool> MarkReviewHelpfulAsync(string reviewId)
    {
        var response = await _httpClient.PostAsync($"/api/reviews/{reviewId}/helpful", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReportReviewAsync(string reviewId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/reviews/{reviewId}/report", new { Reason = reason }).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // Moderation
    public async Task<ModerationDashboardDto> GetModerationDashboardAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/dashboard").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return new ModerationDashboardDto();
        return await response.Content.ReadFromJsonAsync<ModerationDashboardDto>().ConfigureAwait(false) ?? new ModerationDashboardDto();
    }

    public async Task<List<UserBanDto>> GetBannedUsersAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/bans").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UserBanDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<List<MessageReportDto>> GetPendingReportsAsync()
    {
        var response = await _httpClient.GetAsync("/api/moderation/reports?status=pending").ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<MessageReportDto>>().ConfigureAwait(false) ?? [];
    }

    public async Task<bool> BanUserAsync(BanUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/bans", request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnbanUserAsync(string oderId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/bans/{oderId}/unban", new { Reason = reason }).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> WarnUserAsync(WarnUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/warnings", request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MuteUserAsync(MuteUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/moderation/mutes", request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnmuteUserAsync(string oderId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/mutes/{oderId}/unmute", new { Reason = reason }).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DismissReportAsync(string reportId)
    {
        var response = await _httpClient.PostAsync($"/api/moderation/reports/{reportId}/dismiss", null).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResolveReportAsync(string reportId, string resolution)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/reports/{reportId}/resolve", new { Resolution = resolution }).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMessageAsync(string messageId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/moderation/messages/{messageId}/delete", new { Reason = reason }).ConfigureAwait(false);
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

        var result = await response.Content.ReadFromJsonAsync<MediaUploadResponse>().ConfigureAwait(false);
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
}

public class MediaUploadResponse
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}
