using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class CreateOutletRoomDialog : Window
{
    private readonly IFileUploadService? _fileUploadService;
    private readonly IApiService? _apiService;
    private string? _selectedIconPath;
    private string _selectedRoomType = "Community";

    public CreateRoomRequest? Result { get; private set; }

    public CreateOutletRoomDialog()
    {
        InitializeComponent();

        _fileUploadService = (IFileUploadService?)App.ServiceProvider.GetService(typeof(IFileUploadService));
        _apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));

        // Enable dragging the window
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SelectIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif",
            Title = "Select Room Icon"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedIconPath = dialog.FileName;

            try
            {
                var bitmap = new BitmapImage(new Uri(_selectedIconPath));
                RoomIconImage.Source = bitmap;
                RoomIconImage.Visibility = Visibility.Visible;
                RoomIconPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                MessageBox.Show("Failed to load image. Please select a valid image file.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RoomType_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string roomType)
        {
            _selectedRoomType = roomType;
            UpdateRoomTypeSelection();
        }
    }

    private void UpdateRoomTypeSelection()
    {
        // Reset all borders
        CommunityOption.BorderBrush = new SolidColorBrush(Colors.Transparent);
        OutletOption.BorderBrush = new SolidColorBrush(Colors.Transparent);
        PrivateOption.BorderBrush = new SolidColorBrush(Colors.Transparent);

        // Highlight selected
        var accentBrush = new SolidColorBrush(Color.FromRgb(88, 101, 242));
        switch (_selectedRoomType)
        {
            case "Community":
                CommunityOption.BorderBrush = accentBrush;
                OutletFeaturesSection.Visibility = Visibility.Collapsed;
                break;
            case "Outlet":
                OutletOption.BorderBrush = accentBrush;
                OutletFeaturesSection.Visibility = Visibility.Visible;
                break;
            case "Private":
                PrivateOption.BorderBrush = accentBrush;
                OutletFeaturesSection.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private async void CreateRoom_Click(object sender, RoutedEventArgs e)
    {
        // Validate input
        var roomName = RoomNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            ShowError("Please enter a room name.");
            return;
        }

        if (roomName.Length < 2)
        {
            ShowError("Room name must be at least 2 characters.");
            return;
        }

        // Disable button during creation
        CreateButton.IsEnabled = false;
        CreateButton.Content = "Creating...";

        try
        {
            // Upload icon if selected
            string? iconUrl = null;
            if (!string.IsNullOrEmpty(_selectedIconPath) && _fileUploadService != null && _apiService?.Token != null)
            {
                var uploadResult = await _fileUploadService.UploadAttachmentAsync(_selectedIconPath, _apiService.Token);
                if (uploadResult.Success)
                {
                    iconUrl = uploadResult.FileUrl;
                }
            }

            // Parse marketplace fee
            decimal marketplaceFee = 5;
            if (_selectedRoomType == "Outlet" && decimal.TryParse(MarketplaceFeeTextBox.Text, out var fee))
            {
                marketplaceFee = Math.Clamp(fee, 0, 20);
            }

            // Get streaming tier
            var streamingTier = StreamingTierComboBox.SelectedIndex switch
            {
                0 => StreamingTier.Basic,
                1 => StreamingTier.Standard,
                2 => StreamingTier.Premium,
                3 => StreamingTier.Ultra,
                _ => StreamingTier.Standard
            };

            // Create room request
            Result = new CreateRoomRequest
            {
                Name = roomName,
                Description = RoomDescriptionTextBox.Text?.Trim() ?? string.Empty,
                IconUrl = iconUrl,
                IsPublic = _selectedRoomType != "Private",
                AllowMarketplace = _selectedRoomType == "Outlet" && EnableMarketplaceCheckBox.IsChecked == true,
                AllowVoice = EnableVoiceCheckBox.IsChecked == true,
                AllowVideo = EnableVideoCheckBox.IsChecked == true,
                AllowScreenShare = EnableScreenShareCheckBox.IsChecked == true,
                MarketplaceFeePercent = marketplaceFee,
                StreamingTier = streamingTier
            };

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to create room: {ex.Message}");
        }
        finally
        {
            CreateButton.IsEnabled = true;
            CreateButton.Content = "Create Room";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

/// <summary>
/// Request to create a new room
/// </summary>
public class CreateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool AllowMarketplace { get; set; }
    public bool AllowVoice { get; set; } = true;
    public bool AllowVideo { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
    public decimal MarketplaceFeePercent { get; set; } = 5;
    public StreamingTier StreamingTier { get; set; } = StreamingTier.Standard;
}

public enum StreamingTier
{
    Basic,
    Standard,
    Premium,
    Ultra
}
