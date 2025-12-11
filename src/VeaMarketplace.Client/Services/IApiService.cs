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
    Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request);
    Task<List<CustomRoleDto>> GetUserRolesAsync(string userId);
    Task<List<CustomRoleDto>> GetAllRolesAsync();
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
}
