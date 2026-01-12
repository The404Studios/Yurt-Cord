using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private UserProfileDto? _profile;

    [ObservableProperty]
    private string? _statusMessage;

    public ProfileViewModel(IApiService apiService, IAuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        IsLoading = true;
        try
        {
            Profile = await _apiService.GetCurrentUserAsync();
            StatusMessage = Profile?.StatusMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateStatusAsync()
    {
        if (Profile == null) return;

        await _apiService.UpdateProfileAsync(new UpdateProfileDto
        {
            StatusMessage = StatusMessage
        });
    }
}
