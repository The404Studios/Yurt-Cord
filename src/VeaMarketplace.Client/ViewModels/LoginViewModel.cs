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
    private readonly INotificationHubService _notificationHubService;
    private readonly IRoomHubService _roomHubService;
    private readonly ISettingsService _settingsService;

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

    public LoginViewModel(
        IApiService apiService,
        IChatService chatService,
        IFriendService friendService,
        IProfileService profileService,
        IContentService contentService,
        INotificationHubService notificationHubService,
        IRoomHubService roomHubService,
        ISettingsService settingsService)
    {
        _apiService = apiService;
        _chatService = chatService;
        _friendService = friendService;
        _profileService = profileService;
        _contentService = contentService;
        _notificationHubService = notificationHubService;
        _roomHubService = roomHubService;
        _settingsService = settingsService;

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
            var result = await _apiService.LoginAsync(Username, Password);

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

                // Connect to all real-time SignalR services in parallel for faster login
                if (result.Token != null)
                {
                    var connectionTasks = new List<Task>
                    {
                        _chatService.ConnectAsync(result.Token),
                        _friendService.ConnectAsync(result.Token),
                        _profileService.ConnectAsync(result.Token),
                        _contentService.ConnectAsync(result.Token),
                        _notificationHubService.ConnectAsync(result.Token),
                        _roomHubService.ConnectAsync(result.Token)
                    };

                    try
                    {
                        await Task.WhenAll(connectionTasks);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Some services failed to connect: {ex.Message}");
                        // Continue with login - partial connectivity is acceptable
                    }
                }

                OnLoginSuccess?.Invoke();
            }
            else
            {
                SetError(result.Message);
                OnLoginFailed?.Invoke(result.Message);
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

        if (Password.Length < AppConstants.MinPasswordLength)
        {
            SetError($"Password must be at least {AppConstants.MinPasswordLength} characters");
            return;
        }

        ClearError();
        IsLoading = true;

        try
        {
            var result = await _apiService.RegisterAsync(Username, Email, Password);

            if (result.Success)
            {
                // Connect to all real-time SignalR services in parallel
                if (result.Token != null)
                {
                    var connectionTasks = new List<Task>
                    {
                        _chatService.ConnectAsync(result.Token),
                        _friendService.ConnectAsync(result.Token),
                        _profileService.ConnectAsync(result.Token),
                        _contentService.ConnectAsync(result.Token),
                        _notificationHubService.ConnectAsync(result.Token),
                        _roomHubService.ConnectAsync(result.Token)
                    };

                    try
                    {
                        await Task.WhenAll(connectionTasks);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Some services failed to connect: {ex.Message}");
                        // Continue with registration - partial connectivity is acceptable
                    }
                }

                OnLoginSuccess?.Invoke();
            }
            else
            {
                SetError(result.Message);
                OnLoginFailed?.Invoke(result.Message);
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
