using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Controls;

public partial class TypingIndicator : UserControl
{
    private readonly ObservableCollection<TypingUser> _typingUsers = [];
    private Storyboard? _typingAnimation;

    public TypingIndicator()
    {
        InitializeComponent();
        TypingAvatars.ItemsSource = _typingUsers;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _typingAnimation = (Storyboard)FindResource("TypingAnimation");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _typingAnimation?.Stop();
    }

    public void AddTypingUser(string userId, string username, string? avatarUrl)
    {
        if (_typingUsers.Any(u => u.UserId == userId))
            return;

        _typingUsers.Add(new TypingUser
        {
            UserId = userId,
            Username = username,
            AvatarUrl = avatarUrl ?? "/Assets/default-avatar.png"
        });

        UpdateDisplay();
    }

    public void RemoveTypingUser(string userId)
    {
        var user = _typingUsers.FirstOrDefault(u => u.UserId == userId);
        if (user != null)
        {
            _typingUsers.Remove(user);
            UpdateDisplay();
        }
    }

    public void ClearTypingUsers()
    {
        _typingUsers.Clear();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_typingUsers.Count == 0)
        {
            Visibility = Visibility.Collapsed;
            _typingAnimation?.Stop();
            return;
        }

        Visibility = Visibility.Visible;
        _typingAnimation?.Begin();

        // Update typing text
        TypingText.Text = _typingUsers.Count switch
        {
            1 => $"{_typingUsers[0].Username} is typing...",
            2 => $"{_typingUsers[0].Username} and {_typingUsers[1].Username} are typing...",
            3 => $"{_typingUsers[0].Username}, {_typingUsers[1].Username}, and {_typingUsers[2].Username} are typing...",
            _ => "Several people are typing..."
        };
    }
}

public class TypingUser
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}
