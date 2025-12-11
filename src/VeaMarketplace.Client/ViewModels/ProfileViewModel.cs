using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private UserDto? _user;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isOwnProfile = true;

    [ObservableProperty]
    private string _editBio = string.Empty;

    [ObservableProperty]
    private string _editAvatarUrl = string.Empty;

    public ProfileViewModel(IApiService apiService, INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;

        // Subscribe to profile view changes
        _navigationService.OnViewUserProfile += OnViewUserProfile;

        // Initialize with current user
        User = _apiService.CurrentUser;
    }

    private async void OnViewUserProfile(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            // View own profile
            IsOwnProfile = true;
            User = _apiService.CurrentUser;
        }
        else
        {
            // View another user's profile
            IsOwnProfile = userId == _apiService.CurrentUser?.Id;

            if (!IsOwnProfile)
            {
                IsLoading = true;
                try
                {
                    var userProfile = await _apiService.GetUserProfileAsync(userId);
                    if (userProfile != null)
                    {
                        User = userProfile;
                    }
                    else
                    {
                        // Fallback to basic user info
                        User = await _apiService.GetUserAsync(userId);
                    }
                }
                catch (Exception ex)
                {
                    SetError($"Failed to load profile: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            }
            else
            {
                User = _apiService.CurrentUser;
            }
        }

        IsEditing = false;
    }

    [RelayCommand]
    private void StartEditing()
    {
        if (User == null || !IsOwnProfile) return;
        EditBio = User.Bio;
        EditAvatarUrl = User.AvatarUrl;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEditing()
    {
        IsEditing = false;
        ClearError();
    }

    [RelayCommand]
    private async Task SaveProfile()
    {
        if (!IsOwnProfile) return;

        IsLoading = true;
        ClearError();

        try
        {
            var request = new UpdateProfileRequest
            {
                Bio = EditBio,
                AvatarUrl = EditAvatarUrl
            };
            var updatedUser = await _apiService.UpdateProfileAsync(request);
            if (updatedUser != null)
            {
                User = updatedUser;
                IsEditing = false;
            }
            else
            {
                SetError("Failed to update profile");
            }
        }
        catch (Exception ex)
        {
            SetError("Failed to save profile: " + ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshProfile()
    {
        if (User == null) return;

        IsLoading = true;
        try
        {
            var refreshedUser = await _apiService.GetUserAsync(User.Id);
            if (refreshedUser != null)
                User = refreshedUser;
        }
        catch
        {
            // Ignore refresh errors
        }
        finally
        {
            IsLoading = false;
        }
    }
}
