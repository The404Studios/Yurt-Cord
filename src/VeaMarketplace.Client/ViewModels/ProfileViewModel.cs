using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Controls;
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

    // Custom Status Properties
    [ObservableProperty]
    private bool _isCustomStatusDialogOpen;

    [ObservableProperty]
    private string _customStatusText = string.Empty;

    [ObservableProperty]
    private string _customStatusEmoji = string.Empty;

    [ObservableProperty]
    private int _customStatusDuration; // 0 = no expiry, otherwise minutes

    [ObservableProperty]
    private CustomStatusDto? _currentCustomStatus;

    [ObservableProperty]
    private UserPresenceStatus _currentPresenceStatus = UserPresenceStatus.Online;

    [ObservableProperty]
    private RichPresenceDto? _currentRichPresence;

    // Status Selector visibility
    [ObservableProperty]
    private bool _isStatusSelectorOpen;

    // Presence display
    [ObservableProperty]
    private string _presenceStatusText = "Online";

    [ObservableProperty]
    private string _activityText = string.Empty;

    public List<string> StatusDurationOptions { get; } = new()
    {
        "Don't clear",
        "30 minutes",
        "1 hour",
        "4 hours",
        "Today",
        "This week"
    };

    public ObservableCollection<string> CommonEmojis { get; } = new()
    {
        "😀", "😎", "🎮", "🎵", "💻", "📚", "🏠", "🌙",
        "☕", "🔥", "✨", "💪", "🎯", "🚀", "💤", "🔇"
    };

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

    // Status and Presence Commands
    [RelayCommand]
    private void ToggleStatusSelector()
    {
        IsStatusSelectorOpen = !IsStatusSelectorOpen;
    }

    [RelayCommand]
    private void CloseStatusSelector()
    {
        IsStatusSelectorOpen = false;
    }

    [RelayCommand]
    private async Task SetPresenceStatus(UserPresenceStatus status)
    {
        try
        {
            CurrentPresenceStatus = status;
            PresenceStatusText = status.ToString();
            IsStatusSelectorOpen = false;

            await _apiService.UpdatePresenceAsync(new UpdatePresenceRequest
            {
                Status = status
            });
        }
        catch (Exception ex)
        {
            SetError($"Failed to update status: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenCustomStatusDialog()
    {
        CustomStatusText = CurrentCustomStatus?.Text ?? string.Empty;
        CustomStatusEmoji = CurrentCustomStatus?.EmojiName ?? string.Empty;
        CustomStatusDuration = 0;
        IsCustomStatusDialogOpen = true;
        IsStatusSelectorOpen = false;
    }

    [RelayCommand]
    private void CloseCustomStatusDialog()
    {
        IsCustomStatusDialogOpen = false;
    }

    [RelayCommand]
    private void SelectEmoji(string emoji)
    {
        CustomStatusEmoji = emoji;
    }

    [RelayCommand]
    private async Task SaveCustomStatus()
    {
        try
        {
            var duration = CustomStatusDuration switch
            {
                1 => 30,        // 30 minutes
                2 => 60,        // 1 hour
                3 => 240,       // 4 hours
                4 => GetMinutesUntilEndOfDay(),
                5 => GetMinutesUntilEndOfWeek(),
                _ => (int?)null // Don't clear
            };

            var request = new SetCustomStatusRequest
            {
                Text = string.IsNullOrWhiteSpace(CustomStatusText) ? null : CustomStatusText,
                EmojiName = string.IsNullOrWhiteSpace(CustomStatusEmoji) ? null : CustomStatusEmoji,
                DurationMinutes = duration
            };

            await _apiService.SetCustomStatusAsync(request);

            CurrentCustomStatus = new CustomStatusDto
            {
                Text = request.Text,
                EmojiName = request.EmojiName,
                ExpiresAt = duration.HasValue ? DateTime.UtcNow.AddMinutes(duration.Value) : null
            };

            IsCustomStatusDialogOpen = false;
            UpdateActivityText();
        }
        catch (Exception ex)
        {
            SetError($"Failed to set custom status: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearCustomStatus()
    {
        try
        {
            await _apiService.SetCustomStatusAsync(new SetCustomStatusRequest());
            CurrentCustomStatus = null;
            CustomStatusText = string.Empty;
            CustomStatusEmoji = string.Empty;
            IsCustomStatusDialogOpen = false;
            UpdateActivityText();
        }
        catch (Exception ex)
        {
            SetError($"Failed to clear custom status: {ex.Message}");
        }
    }

    private void UpdateActivityText()
    {
        if (CurrentRichPresence != null && CurrentRichPresence.ActivityType != ActivityType.None)
        {
            var prefix = CurrentRichPresence.ActivityType switch
            {
                ActivityType.Playing => "Playing",
                ActivityType.Streaming => "Streaming",
                ActivityType.Listening => "Listening to",
                ActivityType.Watching => "Watching",
                ActivityType.Competing => "Competing in",
                _ => ""
            };
            ActivityText = $"{prefix} {CurrentRichPresence.ActivityName}";
        }
        else if (CurrentCustomStatus != null && !string.IsNullOrEmpty(CurrentCustomStatus.Text))
        {
            var emoji = !string.IsNullOrEmpty(CurrentCustomStatus.EmojiName)
                ? $"{CurrentCustomStatus.EmojiName} "
                : "";
            ActivityText = $"{emoji}{CurrentCustomStatus.Text}";
        }
        else
        {
            ActivityText = string.Empty;
        }
    }

    private static int GetMinutesUntilEndOfDay()
    {
        var now = DateTime.Now;
        var endOfDay = now.Date.AddDays(1);
        return (int)(endOfDay - now).TotalMinutes;
    }

    private static int GetMinutesUntilEndOfWeek()
    {
        var now = DateTime.Now;
        var daysUntilEndOfWeek = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilEndOfWeek == 0) daysUntilEndOfWeek = 7;
        var endOfWeek = now.Date.AddDays(daysUntilEndOfWeek);
        return (int)(endOfWeek - now).TotalMinutes;
    }

    partial void OnCurrentPresenceStatusChanged(UserPresenceStatus value)
    {
        PresenceStatusText = value switch
        {
            UserPresenceStatus.Online => "Online",
            UserPresenceStatus.Idle => "Idle",
            UserPresenceStatus.DoNotDisturb => "Do Not Disturb",
            UserPresenceStatus.Invisible => "Invisible",
            UserPresenceStatus.Offline => "Offline",
            _ => "Online"
        };
    }
}
