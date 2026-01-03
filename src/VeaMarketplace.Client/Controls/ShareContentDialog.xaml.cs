using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Controls;

public partial class ShareContentDialog : UserControl
{
    public enum ShareContentType
    {
        Product,
        Profile,
        Message,
        Link,
        Image
    }

    public class ShareableContent
    {
        public ShareContentType Type { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string ShareLink { get; set; } = string.Empty;
    }

    public class ShareFriend
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }

    private ShareableContent? _content;
    private readonly ObservableCollection<ShareFriend> _friends = new();
    private readonly ObservableCollection<ShareFriend> _filteredFriends = new();
    private readonly DispatcherTimer _copySuccessTimer;

    public event EventHandler? CloseRequested;
    public event EventHandler<ShareFriend>? ContentSharedToFriend;
    public event EventHandler? ContentSharedToGroup;
    public event EventHandler? LinkCopied;

    public ShareContentDialog()
    {
        InitializeComponent();

        FriendsListControl.ItemsSource = _filteredFriends;

        _copySuccessTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _copySuccessTimer.Tick += OnCopySuccessTimerTick;

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup timer
        if (_copySuccessTimer != null)
        {
            _copySuccessTimer.Stop();
            _copySuccessTimer.Tick -= OnCopySuccessTimerTick;
        }

        Unloaded -= OnUnloaded;
    }

    private void OnCopySuccessTimerTick(object? sender, EventArgs e)
    {
        CopySuccessIcon.Visibility = Visibility.Collapsed;
        _copySuccessTimer.Stop();
    }

    public void SetContent(ShareableContent content)
    {
        _content = content;

        ContentTitle.Text = content.Title;
        ContentSubtitle.Text = content.Subtitle;
        ShareLinkText.Text = content.ShareLink;

        if (!string.IsNullOrEmpty(content.Description))
        {
            ContentDescription.Text = content.Description;
            ContentDescription.Visibility = Visibility.Visible;
        }
        else
        {
            ContentDescription.Visibility = Visibility.Collapsed;
        }

        // Set icon based on type
        ContentIcon.Text = content.Type switch
        {
            ShareContentType.Product => "🛒",
            ShareContentType.Profile => "👤",
            ShareContentType.Message => "💬",
            ShareContentType.Link => "🔗",
            ShareContentType.Image => "🖼",
            _ => "📦"
        };

        // Load image if available
        if (!string.IsNullOrEmpty(content.ImageUrl))
        {
            try
            {
                ContentImage.Source = new BitmapImage(new Uri(content.ImageUrl));
                ContentImage.Visibility = Visibility.Visible;
                ContentIcon.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ContentImage.Visibility = Visibility.Collapsed;
                ContentIcon.Visibility = Visibility.Visible;
            }
        }
    }

    public void SetFriends(IEnumerable<ShareFriend> friends)
    {
        _friends.Clear();
        _filteredFriends.Clear();

        foreach (var friend in friends.OrderByDescending(f => f.IsOnline))
        {
            _friends.Add(friend);
            _filteredFriends.Add(friend);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        if (_content == null) return;

        try
        {
            Clipboard.SetText(_content.ShareLink);
            CopySuccessIcon.Visibility = Visibility.Visible;
            _copySuccessTimer.Start();
            LinkCopied?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Clipboard error
        }
    }

    private void ShareToGroup_Click(object sender, RoutedEventArgs e)
    {
        ContentSharedToGroup?.Invoke(this, EventArgs.Empty);
    }

    private void ShareTwitter_Click(object sender, RoutedEventArgs e)
    {
        if (_content == null) return;

        var text = Uri.EscapeDataString($"Check out {_content.Title}! {_content.ShareLink}");
        var url = $"https://twitter.com/intent/tweet?text={text}";

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser launch error
        }
    }

    private void ShareDiscord_Click(object sender, RoutedEventArgs e)
    {
        // Copy to clipboard for Discord sharing
        if (_content == null) return;

        try
        {
            var message = $"**{_content.Title}**\n{_content.Subtitle}\n{_content.ShareLink}";
            Clipboard.SetText(message);
            CopySuccessIcon.Visibility = Visibility.Visible;
            _copySuccessTimer.Start();
        }
        catch
        {
            // Clipboard error
        }
    }

    private void ShareMore_Click(object sender, RoutedEventArgs e)
    {
        // Show additional share options
        if (_content == null) return;

        // For now, copy link
        try
        {
            Clipboard.SetText(_content.ShareLink);
            CopySuccessIcon.Visibility = Visibility.Visible;
            _copySuccessTimer.Start();
        }
        catch
        {
            // Clipboard error
        }
    }

    private void FriendSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = FriendSearchBox.Text.ToLower();

        _filteredFriends.Clear();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _friends
            : _friends.Where(f => f.Username.ToLower().Contains(query));

        foreach (var friend in filtered.OrderByDescending(f => f.IsOnline))
        {
            _filteredFriends.Add(friend);
        }
    }

    private void Friend_Click(object sender, MouseButtonEventArgs e)
    {
        // Visual selection feedback - highlight the clicked friend card
        if (sender is Border border)
        {
            // Reset all friend borders to default
            foreach (var item in FriendsListControl.Items)
            {
                if (FriendsListControl.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter presenter)
                {
                    var childBorder = FindVisualChild<Border>(presenter);
                    if (childBorder != null)
                    {
                        childBorder.BorderThickness = new Thickness(0);
                    }
                }
            }

            // Highlight selected friend
            border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            border.BorderThickness = new Thickness(2);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var found = FindVisualChild<T>(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private void SendToFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ShareFriend friend) return;

        ContentSharedToFriend?.Invoke(this, friend);
    }

    public string GetMessage()
    {
        return MessageBox.Text;
    }
}
