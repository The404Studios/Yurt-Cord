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
    Task<UserDto> UpdateProfileAsync(string? bio, string? avatarUrl);
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
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse();

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
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse();

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
            var response = await _httpClient.GetAsync("/api/auth/validate");
            if (response.IsSuccessStatusCode)
            {
                CurrentUser = await response.Content.ReadFromJsonAsync<UserDto>();
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

        var response = await _httpClient.GetAsync(url);
        return await response.Content.ReadFromJsonAsync<ProductListResponse>() ?? new ProductListResponse();
    }

    public async Task<ProductDto?> GetProductAsync(string productId)
    {
        var response = await _httpClient.GetAsync($"/api/products/{productId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/products", request);
        return await response.Content.ReadFromJsonAsync<ProductDto>() ?? new ProductDto();
    }

    public async Task<bool> PurchaseProductAsync(string productId)
    {
        var response = await _httpClient.PostAsync($"/api/products/{productId}/purchase", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProductDto>> GetMyProductsAsync()
    {
        var response = await _httpClient.GetAsync("/api/products/my");
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>() ?? new List<ProductDto>();
    }

    public async Task<UserDto?> GetUserAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    public async Task<UserDto> UpdateProfileAsync(string? bio, string? avatarUrl)
    {
        var request = new { Bio = bio, AvatarUrl = avatarUrl };
        var response = await _httpClient.PutAsJsonAsync("/api/users/profile", request);
        return await response.Content.ReadFromJsonAsync<UserDto>() ?? CurrentUser!;
    }
}
