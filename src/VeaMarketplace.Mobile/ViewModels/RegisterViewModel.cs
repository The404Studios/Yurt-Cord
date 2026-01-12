using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    public RegisterViewModel(
        IAuthService authService,
        INavigationService navigationService,
        INotificationService notificationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password))
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

        IsLoading = true;
        ClearError();

        try
        {
            var success = await _authService.RegisterAsync(Username, Email, Password);
            if (success)
            {
                await _notificationService.ShowToastAsync("AGENT REGISTERED");
                await _navigationService.NavigateToMainAsync();
            }
            else
            {
                SetError("Registration failed. Username or email may already exist.");
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
    private async Task GoBackAsync()
    {
        await _navigationService.GoBackAsync();
    }
}
