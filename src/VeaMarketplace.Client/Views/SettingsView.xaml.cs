using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;
    private readonly INavigationService _navigationService;
    private readonly IApiService _apiService;
    private string _currentSection = "Voice";
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, StackPanel> _panels = new();

    public SettingsView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (SettingsViewModel)App.ServiceProvider.GetService(typeof(SettingsViewModel))!;
        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        DataContext = _viewModel;

        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Map nav buttons
        _navButtons["Account"] = AccountNavButton;
        _navButtons["Profile"] = ProfileNavButton;
        _navButtons["Privacy"] = PrivacyNavButton;
        _navButtons["Appearance"] = AppearanceNavButton;
        _navButtons["Notifications"] = NotificationsNavButton;
        _navButtons["Voice"] = VoiceNavButton;
        _navButtons["Keybinds"] = KeybindsNavButton;

        // Map panels
        _panels["Account"] = AccountPanel;
        _panels["Profile"] = ProfilePanel;
        _panels["Privacy"] = PrivacyPanel;
        _panels["Appearance"] = AppearancePanel;
        _panels["Notifications"] = NotificationsPanel;
        _panels["Voice"] = VoicePanel;
        _panels["Keybinds"] = KeybindsPanel;

        // Load user data for Account section
        LoadAccountInfo();

        // Show admin section for moderators/admins
        CheckAdminAccess();
    }

    private void CheckAdminAccess()
    {
        // Check if current user has moderator or admin privileges
        var currentUser = _apiService.CurrentUser;
        if (currentUser != null)
        {
            var role = currentUser.Role?.ToLowerInvariant();
            var isAdminOrMod = role == "admin" || role == "moderator" || role == "owner";

            if (isAdminOrMod)
            {
                AdminSectionHeader.Visibility = Visibility.Visible;
                ModerationNavButton.Visibility = Visibility.Visible;
                AdminDivider.Visibility = Visibility.Visible;
            }
        }
    }

    private void LoadAccountInfo()
    {
        var apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        if (apiService.CurrentUser != null)
        {
            AccountUsername.Text = apiService.CurrentUser.Username;
            AccountEmail.Text = apiService.CurrentUser.Email ?? "Not set";
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string section)
        {
            NavigateToSection(section);
        }
    }

    private void NavigateToSection(string section)
    {
        _currentSection = section;

        // Update header
        SettingsHeader.Text = section switch
        {
            "Account" => "My Account",
            "Profile" => "Profile",
            "Privacy" => "Privacy & Safety",
            "Appearance" => "Appearance",
            "Notifications" => "Notifications",
            "Voice" => "Voice & Audio",
            "Keybinds" => "Keybinds",
            _ => section
        };

        // Update button backgrounds
        var activeBrush = (SolidColorBrush)FindResource("QuaternaryDarkBrush");
        var inactiveBrush = new SolidColorBrush(Colors.Transparent);

        foreach (var kvp in _navButtons)
        {
            kvp.Value.Background = kvp.Key == section ? activeBrush : inactiveBrush;
        }

        // Show/hide panels
        foreach (var kvp in _panels)
        {
            kvp.Value.Visibility = kvp.Key == section ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSection("Profile");
    }

    private void LogOut_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to log out?",
            "Log Out",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // TODO: Clear auth and return to login
            Application.Current.Shutdown();
        }
    }

    private void Moderation_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateToModeration();
    }

    private void BlockedUsers_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("BlockedUsers");
    }

    private void PttKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.StartRecordingPttKeyCommand.Execute(null);
        Focus();
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.IsRecordingPttKey == true)
        {
            _viewModel.SetPushToTalkKey(e.Key);
            e.Handled = true;
        }
    }
}
