using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly JsonSerializerOptions _jsonOptions;

    public string? AuthToken { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken);

    public ApiService(IHttpClientFactory httpClientFactory, ISettingsService settingsService)
    {
        _httpClient = httpClientFactory.CreateClient("OverseerApi");
        _settingsService = settingsService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Try to restore saved token
        var savedToken = _settingsService.GetSavedToken();
        if (!string.IsNullOrEmpty(savedToken))
        {
            SetAuthToken(savedToken);
        }
    }

    private void SetAuthToken(string? token)
    {
        AuthToken = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    #region Authentication

    public async Task<AuthResponseDto?> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new
            {
                Username = username,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
                if (result?.Token != null)
                {
                    SetAuthToken(result.Token);
                    await _settingsService.SaveTokenAsync(result.Token);
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
        }
        return null;
    }

    public async Task<AuthResponseDto?> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", new
            {
                Username = username,
                Email = email,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
                if (result?.Token != null)
                {
                    SetAuthToken(result.Token);
                    await _settingsService.SaveTokenAsync(result.Token);
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Register error: {ex.Message}");
        }
        return null;
    }

    public async Task LogoutAsync()
    {
        SetAuthToken(null);
        await _settingsService.ClearTokenAsync();
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tempClient = new HttpClient();
            tempClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await tempClient.GetAsync($"{_httpClient.BaseAddress}api/auth/validate");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region User Profile

    public async Task<UserProfileDto?> GetCurrentUserAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileDto>("api/users/me", _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCurrentUser error: {ex.Message}");
            return null;
        }
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(string userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/users/{userId}", _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetUserProfile error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileDto profile)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/users/me", profile);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateProfile error: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Chat

    public async Task<List<ChannelDto>> GetChannelsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ChannelDto>>("api/channels", _jsonOptions)
                   ?? new List<ChannelDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetChannels error: {ex.Message}");
            return new List<ChannelDto>();
        }
    }

    public async Task<List<MessageDto>> GetMessagesAsync(string channelId, int page = 1, int pageSize = 50)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<MessageDto>>(
                       $"api/channels/{channelId}/messages?page={page}&pageSize={pageSize}", _jsonOptions)
                   ?? new List<MessageDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMessages error: {ex.Message}");
            return new List<MessageDto>();
        }
    }

    public async Task<MessageDto?> SendMessageAsync(string channelId, string content)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/channels/{channelId}/messages", new
            {
                Content = content
            });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MessageDto>(_jsonOptions);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendMessage error: {ex.Message}");
        }
        return null;
    }

    #endregion

    #region Friends

    public async Task<List<FriendDto>> GetFriendsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<FriendDto>>("api/friends", _jsonOptions)
                   ?? new List<FriendDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFriends error: {ex.Message}");
            return new List<FriendDto>();
        }
    }

    public async Task<List<FriendRequestDto>> GetFriendRequestsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<FriendRequestDto>>("api/friends/requests", _jsonOptions)
                   ?? new List<FriendRequestDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFriendRequests error: {ex.Message}");
            return new List<FriendRequestDto>();
        }
    }

    public async Task<bool> SendFriendRequestAsync(string userId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/friends/request", new { UserId = userId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendFriendRequest error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AcceptFriendRequestAsync(string requestId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/friends/requests/{requestId}/accept", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AcceptFriendRequest error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeclineFriendRequestAsync(string requestId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/friends/requests/{requestId}/decline", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeclineFriendRequest error: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Direct Messages

    public async Task<List<DirectMessageDto>> GetDirectMessagesAsync(string friendId, int page = 1)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<DirectMessageDto>>(
                       $"api/friends/{friendId}/messages?page={page}", _jsonOptions)
                   ?? new List<DirectMessageDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDirectMessages error: {ex.Message}");
            return new List<DirectMessageDto>();
        }
    }

    public async Task<DirectMessageDto?> SendDirectMessageAsync(string friendId, string content)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/friends/{friendId}/messages", new
            {
                Content = content
            });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DirectMessageDto>(_jsonOptions);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendDirectMessage error: {ex.Message}");
        }
        return null;
    }

    #endregion

    #region Marketplace

    public async Task<ProductListResponseDto> GetProductsAsync(int page = 1, string? category = null, string? search = null)
    {
        try
        {
            var url = $"api/products?page={page}";
            if (!string.IsNullOrEmpty(category)) url += $"&category={category}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

            return await _httpClient.GetFromJsonAsync<ProductListResponseDto>(url, _jsonOptions)
                   ?? new ProductListResponseDto();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProducts error: {ex.Message}");
            return new ProductListResponseDto();
        }
    }

    public async Task<ProductDto?> GetProductAsync(string productId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ProductDto>($"api/products/{productId}", _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProduct error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ProductDto>>($"api/products/featured?count={count}", _jsonOptions)
                   ?? new List<ProductDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFeaturedProducts error: {ex.Message}");
            return new List<ProductDto>();
        }
    }

    public async Task<List<ProductDto>> GetTrendingProductsAsync(int count = 10)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ProductDto>>($"api/products/trending?count={count}", _jsonOptions)
                   ?? new List<ProductDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetTrendingProducts error: {ex.Message}");
            return new List<ProductDto>();
        }
    }

    #endregion
}
