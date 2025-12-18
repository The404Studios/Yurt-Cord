using System.Windows;
using System.Windows.Controls;

namespace VeaMarketplace.Client.Controls;

public partial class StatusUpdateDialog : UserControl
{
    public enum UserStatus
    {
        Online,
        Idle,
        DoNotDisturb,
        Invisible
    }

    public class StatusUpdate
    {
        public UserStatus Status { get; set; }
        public string? CustomStatusEmoji { get; set; }
        public string? CustomStatusText { get; set; }
        public int ClearAfterMinutes { get; set; }
        public string? ActivityType { get; set; }
        public string? ActivityText { get; set; }
    }

    private readonly string[] _quickEmojis = new[]
    {
        "😊", "😎", "🎮", "🎧", "💻", "📚", "🎬", "🎵",
        "☕", "🍕", "🏠", "💤", "🔥", "✨", "💡", "🎉",
        "😴", "🤔", "😤", "🥳", "🤗", "😈", "👀", "🙈"
    };

    public event EventHandler<StatusUpdate>? StatusSaved;
    public event EventHandler? StatusCleared;
    public event EventHandler? CloseRequested;

    public StatusUpdateDialog()
    {
        InitializeComponent();
        InitializeQuickEmojis();
    }

    private void InitializeQuickEmojis()
    {
        foreach (var emoji in _quickEmojis)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = emoji, FontSize = 20 },
                Width = 36,
                Height = 36,
                Margin = new Thickness(2),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            button.Click += (s, e) =>
            {
                SelectedEmoji.Text = emoji;
                EmojiPicker.Visibility = Visibility.Collapsed;
            };

            // Add hover effect
            button.MouseEnter += (s, e) =>
            {
                button.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(64, 68, 75));
            };
            button.MouseLeave += (s, e) =>
            {
                button.Background = System.Windows.Media.Brushes.Transparent;
            };

            QuickEmojis.Children.Add(button);
        }
    }

    public void SetCurrentStatus(StatusUpdate status)
    {
        // Set status option
        switch (status.Status)
        {
            case UserStatus.Online:
                OnlineOption.IsChecked = true;
                break;
            case UserStatus.Idle:
                IdleOption.IsChecked = true;
                break;
            case UserStatus.DoNotDisturb:
                DndOption.IsChecked = true;
                break;
            case UserStatus.Invisible:
                InvisibleOption.IsChecked = true;
                break;
        }

        // Set custom status
        if (!string.IsNullOrEmpty(status.CustomStatusEmoji))
        {
            SelectedEmoji.Text = status.CustomStatusEmoji;
        }

        if (!string.IsNullOrEmpty(status.CustomStatusText))
        {
            StatusTextBox.Text = status.CustomStatusText;
        }

        // Set clear after
        foreach (ComboBoxItem item in ClearAfterCombo.Items)
        {
            if (item.Tag is string tagStr && int.TryParse(tagStr, out var minutes) &&
                minutes == status.ClearAfterMinutes)
            {
                ClearAfterCombo.SelectedItem = item;
                break;
            }
        }

        // Set activity
        if (!string.IsNullOrEmpty(status.ActivityType))
        {
            foreach (ComboBoxItem item in ActivityTypeCombo.Items)
            {
                if (item.Content?.ToString() == status.ActivityType)
                {
                    ActivityTypeCombo.SelectedItem = item;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(status.ActivityText))
        {
            ActivityTextBox.Text = status.ActivityText;
        }
    }

    private StatusUpdate GetCurrentStatus()
    {
        var status = new StatusUpdate();

        // Get status
        if (OnlineOption.IsChecked == true)
            status.Status = UserStatus.Online;
        else if (IdleOption.IsChecked == true)
            status.Status = UserStatus.Idle;
        else if (DndOption.IsChecked == true)
            status.Status = UserStatus.DoNotDisturb;
        else if (InvisibleOption.IsChecked == true)
            status.Status = UserStatus.Invisible;

        // Get custom status
        status.CustomStatusEmoji = SelectedEmoji.Text;
        status.CustomStatusText = StatusTextBox.Text;

        // Get clear after
        if (ClearAfterCombo.SelectedItem is ComboBoxItem clearItem &&
            clearItem.Tag is string tagStr && int.TryParse(tagStr, out var minutes))
        {
            status.ClearAfterMinutes = minutes;
        }

        // Get activity
        if (ActivityTypeCombo.SelectedItem is ComboBoxItem activityItem)
        {
            status.ActivityType = activityItem.Content?.ToString();
        }
        status.ActivityText = ActivityTextBox.Text;

        return status;
    }

    private void StatusOption_Checked(object sender, RoutedEventArgs e)
    {
        // Update emoji placeholder based on status selection
        if (sender is System.Windows.Controls.RadioButton radio && radio.IsChecked == true)
        {
            var statusEmojis = new Dictionary<string, string>
            {
                { "OnlineOption", "😊" },
                { "IdleOption", "🌙" },
                { "DoNotDisturbOption", "⛔" },
                { "InvisibleOption", "👻" }
            };

            if (statusEmojis.TryGetValue(radio.Name, out var emoji) && SelectedEmoji != null)
            {
                SelectedEmoji.Text = emoji;
            }
        }
    }

    private void SelectEmoji_Click(object sender, RoutedEventArgs e)
    {
        EmojiPicker.Visibility = EmojiPicker.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void StatusText_Changed(object sender, TextChangedEventArgs e)
    {
        CharCountText.Text = $"{StatusTextBox.Text.Length}/128";

        // Change color when near limit
        if (StatusTextBox.Text.Length > 100)
        {
            CharCountText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(250, 166, 26));
        }
        else if (StatusTextBox.Text.Length > 120)
        {
            CharCountText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69));
        }
        else
        {
            CharCountText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(93, 98, 105));
        }
    }

    private void ClearAfter_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Calculate when status will clear for potential future UI display
        if (ClearAfterCombo?.SelectedItem is ComboBoxItem item)
        {
            var duration = item.Content?.ToString() ?? "";
            var clearTime = duration switch
            {
                "30 minutes" => DateTime.Now.AddMinutes(30),
                "1 hour" => DateTime.Now.AddHours(1),
                "4 hours" => DateTime.Now.AddHours(4),
                "Today" => DateTime.Today.AddDays(1),
                "Don't clear" or "Never" => (DateTime?)null,
                _ => (DateTime?)null
            };
            System.Diagnostics.Debug.WriteLine(clearTime.HasValue
                ? $"Status will clear at {clearTime:h:mm tt}"
                : "Status won't auto-clear");
        }
    }

    private void ClearStatus_Click(object sender, RoutedEventArgs e)
    {
        // Clear all fields
        OnlineOption.IsChecked = true;
        SelectedEmoji.Text = "😊";
        StatusTextBox.Text = "";
        ActivityTextBox.Text = "";
        ClearAfterCombo.SelectedIndex = 0;

        StatusCleared?.Invoke(this, EventArgs.Empty);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var status = GetCurrentStatus();
        StatusSaved?.Invoke(this, status);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
