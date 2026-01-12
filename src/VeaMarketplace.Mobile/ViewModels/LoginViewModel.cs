using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    public LoginViewModel(
        IAuthService authService,
        INavigationService navigationService,
        INotificationService notificationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Please enter username and password");
            return;
        }

        IsLoading = true;
        ClearError();

        try
        {
            var success = await _authService.LoginAsync(Username, Password);
            if (success)
            {
                await _notificationService.ShowToastAsync("ACCESS GRANTED");
                await _navigationService.NavigateToMainAsync();
            }
            else
            {
                SetError("Invalid credentials");
                await _notificationService.ShowToastAsync("ACCESS DENIED");
            }
        }
        catch (Exception ex)
        {
            SetError($"Connection error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToRegisterAsync()
    {
        await _navigationService.NavigateToAsync("register");
    }

    [RelayCommand]
    private async Task TryAutoLoginAsync()
    {
        IsLoading = true;
        try
        {
            if (await _authService.TryAutoLoginAsync())
            {
                await _navigationService.NavigateToMainAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
