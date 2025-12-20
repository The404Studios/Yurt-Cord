using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IChatService _chatService;
    private readonly IFriendService _friendService;
    private readonly IProfileService _profileService;
    private readonly IContentService _contentService;
    private readonly ISettingsService _settingsService;
    private readonly HwidService _hwidService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _isRegistering;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    public event Action? OnLoginSuccess;
    public event Action<string>? OnLoginFailed;

    public LoginViewModel(IApiService apiService, IChatService chatService, IFriendService friendService, IProfileService profileService, IContentService contentService, ISettingsService settingsService, HwidService hwidService)
    {
        _apiService = apiService;
        _chatService = chatService;
        _friendService = friendService;
        _profileService = profileService;
        _contentService = contentService;
        _settingsService = settingsService;
        _hwidService = hwidService;

        // Load saved credentials
        if (_settingsService.Settings.RememberMe)
        {
            Username = _settingsService.Settings.SavedUsername ?? string.Empty;
            RememberMe = true;
        }
    }

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter username and password");
            return;
        }

        ClearError();
        IsLoading = true;

        try
        {
            // Get hardware ID for device binding verification
            var hwid = _hwidService.GetHwid();
            var result = await _apiService.LoginAsync(Username, Password, hwid);

            if (result.Success)
            {
                // Save credentials if remember me is checked
                if (RememberMe)
                {
                    _settingsService.Settings.SavedToken = result.Token;
                    _settingsService.Settings.SavedUsername = Username;
                    _settingsService.Settings.RememberMe = true;
                    _settingsService.SaveSettings();
                }

                // Connect to all real-time SignalR services
                if (result.Token != null)
                {
                    await _chatService.ConnectAsync(result.Token);
                    await _friendService.ConnectAsync(result.Token);
                    await _profileService.ConnectAsync(result.Token);
                    await _contentService.ConnectAsync(result.Token);
                }

                OnLoginSuccess?.Invoke();
            }
            else
            {
                // Provide more specific error for HWID mismatch
                var errorMessage = result.HwidMismatch
                    ? $"This account is bound to a different device (ID: {result.BoundHwidPrefix}...)"
                    : result.Message;
                SetError(errorMessage);
                OnLoginFailed?.Invoke(errorMessage);
            }
        }
        catch (Exception ex)
        {
            SetError("Connection failed. Is the server running?");
            OnLoginFailed?.Invoke(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            SetError("Please fill in all fields");
            return;
        }

        if (Password != ConfirmPassword)
        {
            SetError("Passwords do not match");
            return;
        }

        if (Password.Length < 6)
        {
            SetError("Password must be at least 6 characters");
            return;
        }

        ClearError();
        IsLoading = true;

        try
        {
            // Get hardware ID for device binding
            var hwid = _hwidService.GetHwid();
            var result = await _apiService.RegisterAsync(Username, Email, Password, null, hwid);

            if (result.Success)
            {
                // Connect to all real-time SignalR services
                if (result.Token != null)
                {
                    await _chatService.ConnectAsync(result.Token);
                    await _friendService.ConnectAsync(result.Token);
                    await _profileService.ConnectAsync(result.Token);
                    await _contentService.ConnectAsync(result.Token);
                }

                OnLoginSuccess?.Invoke();
            }
            else
            {
                // Provide more specific error for HWID already bound
                var errorMessage = result.HwidMismatch
                    ? "This device is already registered to another account"
                    : result.Message;
                SetError(errorMessage);
                OnLoginFailed?.Invoke(errorMessage);
            }
        }
        catch (Exception ex)
        {
            SetError("Connection failed. Is the server running?");
            OnLoginFailed?.Invoke(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegistering = !IsRegistering;
        ClearError();
    }
}
