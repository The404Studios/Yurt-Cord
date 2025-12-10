using System.Windows;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ProfileEditDialog : Window
{
    private readonly IApiService _apiService;
    public UserDto? UpdatedUser { get; private set; }

    public ProfileEditDialog(UserDto user)
    {
        InitializeComponent();

        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;

        // Populate fields
        UsernameInput.Text = user.Username;
        BioInput.Text = user.Bio;
        DescriptionInput.Text = user.Description;
        AvatarUrlInput.Text = user.AvatarUrl;
        BannerUrlInput.Text = user.BannerUrl;

        // Set previews
        LoadImagePreview(user.AvatarUrl, isAvatar: true);
        LoadImagePreview(user.BannerUrl, isAvatar: false);

        // Wire up character counters
        BioInput.TextChanged += (s, e) => BioCharCount.Text = $"{BioInput.Text.Length}/150";
        DescriptionInput.TextChanged += (s, e) => DescCharCount.Text = $"{DescriptionInput.Text.Length}/500";

        // Update counters
        BioCharCount.Text = $"{BioInput.Text.Length}/150";
        DescCharCount.Text = $"{DescriptionInput.Text.Length}/500";
    }

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
                AvatarPreview.ImageSource = bitmap;
            else
                BannerPreview.ImageSource = bitmap;
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

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        if (string.IsNullOrWhiteSpace(UsernameInput.Text))
        {
            ErrorText.Text = "Username cannot be empty";
            return;
        }

        try
        {
            var request = new UpdateProfileRequest
            {
                Username = UsernameInput.Text.Trim(),
                Bio = BioInput.Text.Trim(),
                Description = DescriptionInput.Text.Trim(),
                AvatarUrl = AvatarUrlInput.Text.Trim(),
                BannerUrl = BannerUrlInput.Text.Trim()
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
                ErrorText.Text = "Failed to update profile. Username may already be taken.";
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
        }
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
}
