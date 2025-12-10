using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private UserDto? _user;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editBio = string.Empty;

    [ObservableProperty]
    private string _editAvatarUrl = string.Empty;

    public ProfileViewModel(IApiService apiService)
    {
        _apiService = apiService;
        User = _apiService.CurrentUser;
    }

    [RelayCommand]
    private void StartEditing()
    {
        if (User == null) return;
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
        IsLoading = true;
        ClearError();

        try
        {
            var updatedUser = await _apiService.UpdateProfileAsync(EditBio, EditAvatarUrl);
            User = updatedUser;
            IsEditing = false;
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
