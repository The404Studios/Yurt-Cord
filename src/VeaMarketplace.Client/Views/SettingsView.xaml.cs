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
    private readonly INavigationService? _navigationService;
    private readonly IApiService? _apiService;
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

        // QoL Feature buttons
        _navButtons["Templates"] = TemplatesNavButton;
        _navButtons["Scheduled"] = ScheduledNavButton;
        _navButtons["StatusScheduler"] = StatusSchedulerNavButton;
        _navButtons["SmartDND"] = SmartDndNavButton;
        _navButtons["QuickActions"] = QuickActionsNavButton;
        _navButtons["Insights"] = InsightsNavButton;

        // Map panels
        _panels["Account"] = AccountPanel;
        _panels["Profile"] = ProfilePanel;
        _panels["Privacy"] = PrivacyPanel;
        _panels["Appearance"] = AppearancePanel;
        _panels["Notifications"] = NotificationsPanel;
        _panels["Voice"] = VoicePanel;
        _panels["Keybinds"] = KeybindsPanel;

        // QoL Feature panels
        _panels["Templates"] = TemplatesPanel;
        _panels["Scheduled"] = ScheduledPanel;
        _panels["StatusScheduler"] = StatusSchedulerPanel;
        _panels["SmartDND"] = SmartDNDPanel;
        _panels["QuickActions"] = QuickActionsPanel;
        _panels["Insights"] = InsightsPanel;

        // Load user data for Account section
        LoadAccountInfo();

        // Show admin section for moderators/admins
        CheckAdminAccess();

        // Load QoL service data
        LoadQoLData();
    }

    private void CheckAdminAccess()
    {
        // Check if current user has moderator, admin, or owner privileges
        var currentUser = _apiService.CurrentUser;
        if (currentUser != null)
        {
            // Owner, Admin, and Moderator roles can access moderation panel
            var isAdminOrMod = currentUser.Role == VeaMarketplace.Shared.Enums.UserRole.Owner ||
                               currentUser.Role == VeaMarketplace.Shared.Enums.UserRole.Admin ||
                               currentUser.Role == VeaMarketplace.Shared.Enums.UserRole.Moderator;

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
            "Templates" => "Message Templates",
            "Scheduled" => "Scheduled Messages",
            "StatusScheduler" => "Status Scheduler",
            "SmartDND" => "Smart Do Not Disturb",
            "QuickActions" => "Quick Actions",
            "Insights" => "Activity Insights",
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
            // Request logout through navigation service
            // This will disconnect all services, clear auth, and return to login
            _navigationService.RequestLogout();
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

    private void LoadQoLData()
    {
        // Try to get QoL service
        var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
        if (qolService != null)
        {
            // Load message templates
            TemplatesListControl.ItemsSource = qolService.Templates;

            // Load scheduled messages
            ScheduledMessagesControl.ItemsSource = qolService.ScheduledMessages;
            NoScheduledMessages.Visibility = qolService.ScheduledMessages.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Load activity insights
            var todayInsight = qolService.GetTodayInsight();
            TodayMessagesCount.Text = todayInsight.MessagesSent.ToString();
            TodayVoiceMinutes.Text = todayInsight.VoiceMinutes.ToString();
            TodayFriendsCount.Text = todayInsight.FriendsInteractedWith.ToString();
        }
    }

    private void NewTemplate_Click(object sender, RoutedEventArgs e)
    {
        // Show template creation dialog
        var dialog = new Window
        {
            Title = "Create Message Template",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Background = (SolidColorBrush)FindResource("SecondaryDarkBrush")
        };

        var panel = new StackPanel { Margin = new Thickness(20) };

        var nameLabel = new TextBlock { Text = "Template Name:", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 0, 0, 4) };
        var nameBox = new System.Windows.Controls.TextBox { Style = (Style)FindResource("ModernTextBox") };

        var shortcutLabel = new TextBlock { Text = "Shortcut (e.g., /afk):", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 12, 0, 4) };
        var shortcutBox = new System.Windows.Controls.TextBox { Style = (Style)FindResource("ModernTextBox") };

        var contentLabel = new TextBlock { Text = "Message Content:", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 12, 0, 4) };
        var contentBox = new System.Windows.Controls.TextBox { Style = (Style)FindResource("ModernTextBox"), AcceptsReturn = true, Height = 80, TextWrapping = TextWrapping.Wrap };

        var saveBtn = new System.Windows.Controls.Button { Content = "Save Template", Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        saveBtn.Click += (s, args) =>
        {
            var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
            if (qolService != null && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                qolService.AddTemplate(new MessageTemplate
                {
                    Name = nameBox.Text,
                    Shortcut = shortcutBox.Text,
                    Content = contentBox.Text
                });
                dialog.Close();
                LoadQoLData();
            }
        };

        panel.Children.Add(nameLabel);
        panel.Children.Add(nameBox);
        panel.Children.Add(shortcutLabel);
        panel.Children.Add(shortcutBox);
        panel.Children.Add(contentLabel);
        panel.Children.Add(contentBox);
        panel.Children.Add(saveBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    private void ScheduleMessage_Click(object sender, RoutedEventArgs e)
    {
        // Show schedule message dialog
        var dialog = new Window
        {
            Title = "Schedule Message",
            Width = 450,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Background = (SolidColorBrush)FindResource("SecondaryDarkBrush")
        };

        var panel = new StackPanel { Margin = new Thickness(20) };

        var messageLabel = new TextBlock { Text = "Message:", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 0, 0, 4) };
        var messageBox = new System.Windows.Controls.TextBox { Style = (Style)FindResource("ModernTextBox"), AcceptsReturn = true, Height = 80, TextWrapping = TextWrapping.Wrap };

        var channelLabel = new TextBlock { Text = "Target Channel:", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 12, 0, 4) };
        var channelBox = new System.Windows.Controls.TextBox { Style = (Style)FindResource("ModernTextBox"), Text = "#general" };

        var dateLabel = new TextBlock { Text = "Send Date & Time:", Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"), Margin = new Thickness(0, 12, 0, 4) };
        var datePicker = new DatePicker { SelectedDate = DateTime.Now.AddDays(1), Background = (SolidColorBrush)FindResource("PrimaryDarkBrush") };

        var scheduleBtn = new System.Windows.Controls.Button { Content = "Schedule Message", Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        scheduleBtn.Click += (s, args) =>
        {
            var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
            if (qolService != null && !string.IsNullOrWhiteSpace(messageBox.Text) && datePicker.SelectedDate.HasValue)
            {
                qolService.ScheduleMessage(new ScheduledMessage
                {
                    Content = messageBox.Text,
                    TargetChannelId = channelBox.Text,
                    ScheduledTime = datePicker.SelectedDate.Value.AddHours(12) // Default to noon
                });
                dialog.Close();
                LoadQoLData();
            }
        };

        panel.Children.Add(messageLabel);
        panel.Children.Add(messageBox);
        panel.Children.Add(channelLabel);
        panel.Children.Add(channelBox);
        panel.Children.Add(dateLabel);
        panel.Children.Add(datePicker);
        panel.Children.Add(scheduleBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }
}
