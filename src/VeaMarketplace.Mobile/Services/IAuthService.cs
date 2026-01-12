namespace VeaMarketplace.Mobile.Services;

public interface IAuthService
{
    bool IsLoggedIn { get; }
    string? CurrentUserId { get; }
    string? CurrentUsername { get; }

    Task<bool> LoginAsync(string username, string password);
    Task<bool> RegisterAsync(string username, string email, string password);
    Task LogoutAsync();
    Task<bool> TryAutoLoginAsync();
}

public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;

    public bool IsLoggedIn => _apiService.IsAuthenticated;
    public string? CurrentUserId { get; private set; }
    public string? CurrentUsername { get; private set; }

    public AuthService(IApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var result = await _apiService.LoginAsync(username, password);
        if (result != null)
        {
            CurrentUserId = result.UserId;
            CurrentUsername = result.Username;
            return true;
        }
        return false;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        var result = await _apiService.RegisterAsync(username, email, password);
        if (result != null)
        {
            CurrentUserId = result.UserId;
            CurrentUsername = result.Username;
            return true;
        }
        return false;
    }

    public async Task LogoutAsync()
    {
        await _apiService.LogoutAsync();
        CurrentUserId = null;
        CurrentUsername = null;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        var savedToken = _settingsService.GetSavedToken();
        if (string.IsNullOrEmpty(savedToken))
            return false;

        if (await _apiService.ValidateTokenAsync(savedToken))
        {
            var profile = await _apiService.GetCurrentUserAsync();
            if (profile != null)
            {
                CurrentUserId = profile.UserId;
                CurrentUsername = profile.Username;
                return true;
            }
        }

        return false;
    }
}
