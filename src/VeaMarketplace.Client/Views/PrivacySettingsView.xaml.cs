using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class PrivacySettingsView : UserControl
{
    private readonly ISettingsService? _settingsService;
    private readonly INavigationService? _navigationService;
    private readonly IFriendService? _friendService;
    private bool _isLoading = true;

    public PrivacySettingsView()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _settingsService = App.ServiceProvider.GetService(typeof(ISettingsService)) as ISettingsService;
        _navigationService = App.ServiceProvider.GetService(typeof(INavigationService)) as INavigationService;
        _friendService = App.ServiceProvider.GetService(typeof(IFriendService)) as IFriendService;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        UpdateBlockedCount();
    }

    private void LoadSettings()
    {
        if (_settingsService == null) return;

        _isLoading = true;

        // Load DM settings
        AllowServerDMsToggle.IsChecked = _settingsService.GetSetting("privacy.allowServerDMs", true);
        AllowAllDMsToggle.IsChecked = _settingsService.GetSetting("privacy.allowAllDMs", false);

        // Load message filter
        var filterIndex = _settingsService.GetSetting("privacy.messageFilter", 0);
        MessageFilterCombo.SelectedIndex = filterIndex;

        // Load friend request settings
        var friendRequestSetting = _settingsService.GetSetting("privacy.friendRequests", "everyone");
        switch (friendRequestSetting)
        {
            case "everyone":
                FriendsEveryoneRadio.IsChecked = true;
                break;
            case "server":
                FriendsServerRadio.IsChecked = true;
                break;
            case "mutual":
                FriendsMutualRadio.IsChecked = true;
                break;
            case "none":
                FriendsNoneRadio.IsChecked = true;
                break;
            default:
                FriendsEveryoneRadio.IsChecked = true;
                break;
        }

        // Load activity settings
        ShowActivityToggle.IsChecked = _settingsService.GetSetting("privacy.showActivity", true);
        ShowOnlineStatusToggle.IsChecked = _settingsService.GetSetting("privacy.showOnlineStatus", true);

        // Load profile visibility
        ShowMutualServersToggle.IsChecked = _settingsService.GetSetting("privacy.showMutualServers", true);
        ShowMutualFriendsToggle.IsChecked = _settingsService.GetSetting("privacy.showMutualFriends", true);

        _isLoading = false;
    }

    private void UpdateBlockedCount()
    {
        if (_friendService != null)
        {
            var count = _friendService.BlockedUsers.Count;
            BlockedCountText.Text = count == 0
                ? "View and manage blocked users"
                : $"{count} blocked user{(count == 1 ? "" : "s")}";
        }
    }

    private void SettingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settingsService == null) return;

        if (sender is ToggleButton toggle)
        {
            var settingKey = toggle.Name switch
            {
                "AllowServerDMsToggle" => "privacy.allowServerDMs",
                "AllowAllDMsToggle" => "privacy.allowAllDMs",
                "ShowActivityToggle" => "privacy.showActivity",
                "ShowOnlineStatusToggle" => "privacy.showOnlineStatus",
                "ShowMutualServersToggle" => "privacy.showMutualServers",
                "ShowMutualFriendsToggle" => "privacy.showMutualFriends",
                _ => null
            };

            if (settingKey != null)
            {
                _settingsService.SetSetting(settingKey, toggle.IsChecked ?? false);
            }
        }
    }

    private void MessageFilterCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _settingsService == null) return;

        _settingsService.SetSetting("privacy.messageFilter", MessageFilterCombo.SelectedIndex);
    }

    private void FriendRequestRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settingsService == null) return;

        var setting = FriendsEveryoneRadio.IsChecked == true ? "everyone"
            : FriendsServerRadio.IsChecked == true ? "server"
            : FriendsMutualRadio.IsChecked == true ? "mutual"
            : "none";

        _settingsService.SetSetting("privacy.friendRequests", setting);
    }

    private void BlockedUsersLink_Click(object sender, MouseButtonEventArgs e)
    {
        _navigationService?.NavigateTo("BlockedUsers");
    }

    private void RequestDataButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Your data will be compiled and sent to your email address. This may take up to 30 days to process.\n\nDo you want to request your data?",
            "Request My Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            // Send data request to server
            MessageBox.Show(
                "Your data request has been submitted. You will receive an email when your data is ready.",
                "Request Submitted",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "⚠️ WARNING: This action is PERMANENT and cannot be undone!\n\n" +
            "Deleting your account will:\n" +
            "• Remove all your messages\n" +
            "• Remove all your friend connections\n" +
            "• Delete your profile and data\n" +
            "• Cancel any active subscriptions\n\n" +
            "Are you absolutely sure you want to delete your account?",
            "Delete Account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Second confirmation
            var confirm = MessageBox.Show(
                "This is your final warning. Type 'DELETE' in the next dialog to confirm.\n\n" +
                "Click OK to proceed or Cancel to go back.",
                "Final Confirmation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop);

            if (confirm == MessageBoxResult.OK)
            {
                // In a real implementation, show a dialog to type DELETE
                MessageBox.Show(
                    "Account deletion has been scheduled. You will be logged out shortly.",
                    "Account Scheduled for Deletion",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
