using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Client.Views;

public partial class ProfileEditDialog : Window
{
    private readonly IApiService _apiService;
    private readonly UserDto _originalUser;
    private ProfileVisibility _selectedVisibility = ProfileVisibility.Public;
    private string _selectedAccentColor = "#5865F2";

    public UserDto? UpdatedUser { get; private set; }

    public ProfileEditDialog(UserDto user)
    {
        InitializeComponent();

        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _originalUser = user;

        // Populate Profile tab fields
        UsernameInput.Text = user.Username;
        DisplayNameInput.Text = user.DisplayName;
        StatusInput.Text = user.StatusMessage;
        BioInput.Text = user.Bio;
        DescriptionInput.Text = user.Description;
        AvatarUrlInput.Text = user.AvatarUrl;
        BannerUrlInput.Text = user.BannerUrl;

        // Populate Appearance tab
        _selectedAccentColor = string.IsNullOrEmpty(user.AccentColor) ? "#5865F2" : user.AccentColor;
        AccentColorInput.Text = _selectedAccentColor;
        UpdateAccentColorPreview();

        // Populate Social Links tab
        DiscordInput.Text = user.DiscordUsername ?? "";
        TwitterInput.Text = user.TwitterHandle ?? "";
        TelegramInput.Text = user.TelegramUsername ?? "";
        WebsiteInput.Text = user.WebsiteUrl ?? "";

        // Populate Privacy tab
        _selectedVisibility = user.ProfileVisibility;
        UpdateVisibilitySelection();

        // Set image previews
        LoadImagePreview(user.AvatarUrl, isAvatar: true);
        LoadImagePreview(user.BannerUrl, isAvatar: false);

        // Update preview card
        PreviewName.Text = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName;
        PreviewStatus.Text = string.IsNullOrEmpty(user.StatusMessage) ? "No status..." : user.StatusMessage;

        // Wire up character counters
        StatusInput.TextChanged += (s, e) =>
        {
            StatusCharCount.Text = $"{StatusInput.Text.Length}/100";
            PreviewStatus.Text = string.IsNullOrEmpty(StatusInput.Text) ? "No status..." : StatusInput.Text;
        };
        BioInput.TextChanged += (s, e) => BioCharCount.Text = $"{BioInput.Text.Length}/150";
        DescriptionInput.TextChanged += (s, e) => DescCharCount.Text = $"{DescriptionInput.Text.Length}/500";
        DisplayNameInput.TextChanged += (s, e) =>
        {
            PreviewName.Text = string.IsNullOrEmpty(DisplayNameInput.Text) ? UsernameInput.Text : DisplayNameInput.Text;
        };
        UsernameInput.TextChanged += (s, e) =>
        {
            if (string.IsNullOrEmpty(DisplayNameInput.Text))
                PreviewName.Text = UsernameInput.Text;
        };

        // Update initial counters
        StatusCharCount.Text = $"{StatusInput.Text.Length}/100";
        BioCharCount.Text = $"{BioInput.Text.Length}/150";
        DescCharCount.Text = $"{DescriptionInput.Text.Length}/500";
    }

    #region Tab Navigation

    private void SwitchTab(string tabName)
    {
        // Reset all tabs to inactive style
        TabProfile.Style = (Style)FindResource("ProfileTab");
        TabAppearance.Style = (Style)FindResource("ProfileTab");
        TabSocial.Style = (Style)FindResource("ProfileTab");
        TabPrivacy.Style = (Style)FindResource("ProfileTab");

        // Hide all content
        ProfileContent.Visibility = Visibility.Collapsed;
        AppearanceContent.Visibility = Visibility.Collapsed;
        SocialContent.Visibility = Visibility.Collapsed;
        PrivacyContent.Visibility = Visibility.Collapsed;

        // Activate selected tab
        switch (tabName)
        {
            case "Profile":
                TabProfile.Style = (Style)FindResource("ProfileTabActive");
                ProfileContent.Visibility = Visibility.Visible;
                break;
            case "Appearance":
                TabAppearance.Style = (Style)FindResource("ProfileTabActive");
                AppearanceContent.Visibility = Visibility.Visible;
                break;
            case "Social":
                TabSocial.Style = (Style)FindResource("ProfileTabActive");
                SocialContent.Visibility = Visibility.Visible;
                break;
            case "Privacy":
                TabPrivacy.Style = (Style)FindResource("ProfileTabActive");
                PrivacyContent.Visibility = Visibility.Visible;
                break;
        }
    }

    private void TabProfile_Click(object sender, RoutedEventArgs e) => SwitchTab("Profile");
    private void TabAppearance_Click(object sender, RoutedEventArgs e) => SwitchTab("Appearance");
    private void TabSocial_Click(object sender, RoutedEventArgs e) => SwitchTab("Social");
    private void TabPrivacy_Click(object sender, RoutedEventArgs e) => SwitchTab("Privacy");

    #endregion

    #region Image Handling

    private void LoadImagePreview(string url, bool isAvatar)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            if (isAvatar)
            {
                AvatarPreview.ImageSource = bitmap;
                PreviewAvatar.ImageSource = bitmap;
            }
            else
            {
                BannerPreview.ImageSource = bitmap;
            }
        }
        catch
        {
            // Failed to load image
        }
    }

    private void PreviewAvatar_Click(object sender, RoutedEventArgs e)
    {
        LoadImagePreview(AvatarUrlInput.Text, isAvatar: true);
    }

    private void PreviewBanner_Click(object sender, RoutedEventArgs e)
    {
        LoadImagePreview(BannerUrlInput.Text, isAvatar: false);
    }

    private void ClearAvatar_Click(object sender, RoutedEventArgs e)
    {
        AvatarUrlInput.Text = "";
        AvatarPreview.ImageSource = null;
        PreviewAvatar.ImageSource = null;
    }

    private void ClearBanner_Click(object sender, RoutedEventArgs e)
    {
        BannerUrlInput.Text = "";
        BannerPreview.ImageSource = null;
    }

    private void Avatar_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif",
            Title = "Select Avatar Image"
        };

        if (dialog.ShowDialog() == true)
        {
            // For now, just show the local file - in production this would upload
            AvatarUrlInput.Text = dialog.FileName;
            LoadImagePreview(dialog.FileName, isAvatar: true);
        }
    }

    private void Banner_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif",
            Title = "Select Banner Image"
        };

        if (dialog.ShowDialog() == true)
        {
            // For now, just show the local file - in production this would upload
            BannerUrlInput.Text = dialog.FileName;
            LoadImagePreview(dialog.FileName, isAvatar: false);
        }
    }

    #endregion

    #region Accent Color

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string color)
        {
            _selectedAccentColor = color;
            AccentColorInput.Text = color;
            UpdateAccentColorPreview();
        }
    }

    private void AccentColorInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = AccentColorInput.Text.Trim();
        if (text.StartsWith("#") && (text.Length == 7 || text.Length == 4))
        {
            _selectedAccentColor = text;
            UpdateAccentColorPreview();
        }
    }

    private void UpdateAccentColorPreview()
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedAccentColor));
            ColorPreview.Background = brush;
            AvatarAccentRing.Stroke = brush;
            PreviewAccentRing.Stroke = brush;
        }
        catch
        {
            // Invalid color format
        }
    }

    #endregion

    #region Privacy Settings

    private void UpdateVisibilitySelection()
    {
        var accentBrush = (Brush)FindResource("AccentBrush");
        var mutedBrush = (Brush)FindResource("TextMutedBrush");

        // Reset all
        VisibilityPublic.BorderBrush = Brushes.Transparent;
        VisibilityFriends.BorderBrush = Brushes.Transparent;
        VisibilityPrivate.BorderBrush = Brushes.Transparent;
        PublicCheck.Fill = Brushes.Transparent;
        PublicCheck.Stroke = mutedBrush;
        FriendsCheck.Fill = Brushes.Transparent;
        FriendsCheck.Stroke = mutedBrush;
        PrivateCheck.Fill = Brushes.Transparent;
        PrivateCheck.Stroke = mutedBrush;

        // Set selected
        switch (_selectedVisibility)
        {
            case ProfileVisibility.Public:
                VisibilityPublic.BorderBrush = accentBrush;
                PublicCheck.Fill = accentBrush;
                PublicCheck.Stroke = null;
                break;
            case ProfileVisibility.FriendsOnly:
                VisibilityFriends.BorderBrush = accentBrush;
                FriendsCheck.Fill = accentBrush;
                FriendsCheck.Stroke = null;
                break;
            case ProfileVisibility.Private:
                VisibilityPrivate.BorderBrush = accentBrush;
                PrivateCheck.Fill = accentBrush;
                PrivateCheck.Stroke = null;
                break;
        }
    }

    private void VisibilityPublic_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedVisibility = ProfileVisibility.Public;
        UpdateVisibilitySelection();
    }

    private void VisibilityFriends_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedVisibility = ProfileVisibility.FriendsOnly;
        UpdateVisibilitySelection();
    }

    private void VisibilityPrivate_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedVisibility = ProfileVisibility.Private;
        UpdateVisibilitySelection();
    }

    #endregion

    #region Actions

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset all fields to original values
        UsernameInput.Text = _originalUser.Username;
        DisplayNameInput.Text = _originalUser.DisplayName;
        StatusInput.Text = _originalUser.StatusMessage;
        BioInput.Text = _originalUser.Bio;
        DescriptionInput.Text = _originalUser.Description;
        AvatarUrlInput.Text = _originalUser.AvatarUrl;
        BannerUrlInput.Text = _originalUser.BannerUrl;

        _selectedAccentColor = string.IsNullOrEmpty(_originalUser.AccentColor) ? "#5865F2" : _originalUser.AccentColor;
        AccentColorInput.Text = _selectedAccentColor;
        UpdateAccentColorPreview();

        DiscordInput.Text = _originalUser.DiscordUsername ?? "";
        TwitterInput.Text = _originalUser.TwitterHandle ?? "";
        TelegramInput.Text = _originalUser.TelegramUsername ?? "";
        WebsiteInput.Text = _originalUser.WebsiteUrl ?? "";

        _selectedVisibility = _originalUser.ProfileVisibility;
        UpdateVisibilitySelection();

        LoadImagePreview(_originalUser.AvatarUrl, isAvatar: true);
        LoadImagePreview(_originalUser.BannerUrl, isAvatar: false);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        if (string.IsNullOrWhiteSpace(UsernameInput.Text))
        {
            ShowError("Username cannot be empty");
            return;
        }

        try
        {
            var request = new UpdateProfileRequest
            {
                Username = UsernameInput.Text.Trim(),
                DisplayName = DisplayNameInput.Text.Trim(),
                StatusMessage = StatusInput.Text.Trim(),
                Bio = BioInput.Text.Trim(),
                Description = DescriptionInput.Text.Trim(),
                AvatarUrl = AvatarUrlInput.Text.Trim(),
                BannerUrl = BannerUrlInput.Text.Trim(),
                AccentColor = _selectedAccentColor,
                ProfileVisibility = _selectedVisibility,
                DiscordUsername = string.IsNullOrWhiteSpace(DiscordInput.Text) ? null : DiscordInput.Text.Trim(),
                TwitterHandle = string.IsNullOrWhiteSpace(TwitterInput.Text) ? null : TwitterInput.Text.Trim(),
                TelegramUsername = string.IsNullOrWhiteSpace(TelegramInput.Text) ? null : TelegramInput.Text.Trim(),
                WebsiteUrl = string.IsNullOrWhiteSpace(WebsiteInput.Text) ? null : WebsiteInput.Text.Trim()
            };

            var result = await _apiService.UpdateProfileAsync(request);
            if (result != null)
            {
                UpdatedUser = result;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Failed to update profile. Username may already be taken.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Text = "";
        ErrorBorder.Visibility = Visibility.Collapsed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion
}
