using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Controls;

public partial class UserProfilePopup : UserControl
{
    private readonly INavigationService? _navigationService;
    private readonly IFriendService? _friendService;
    private UserDto? _user;
    private string _noteText = string.Empty;
    private bool _isNotePlaceholder = true;

    public event EventHandler<string>? MessageRequested;
    public event EventHandler<string>? CallRequested;
    public event EventHandler<string>? VideoCallRequested;

    public UserProfilePopup()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _navigationService = App.ServiceProvider.GetService(typeof(INavigationService)) as INavigationService;
        _friendService = App.ServiceProvider.GetService(typeof(IFriendService)) as IFriendService;
    }

    public void SetUser(UserDto user)
    {
        _user = user;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_user == null) return;

        // Set display name and username
        DisplayNameText.Text = _user.DisplayName ?? _user.Username;
        UsernameText.Text = $"@{_user.Username}";

        // Set avatar
        if (!string.IsNullOrEmpty(_user.AvatarUrl))
        {
            try
            {
                AvatarBrush.ImageSource = new BitmapImage(new Uri(_user.AvatarUrl));
            }
            catch
            {
                // Keep default avatar
            }
        }

        // Set online status
        UpdateStatusIndicator(_user.Status);

        // Set verified badge
        VerifiedBadge.Visibility = _user.IsVerified ? Visibility.Visible : Visibility.Collapsed;

        // Set custom status
        if (!string.IsNullOrEmpty(_user.CustomStatus))
        {
            CustomStatusBorder.Visibility = Visibility.Visible;
            CustomStatusText.Text = _user.CustomStatus;
            StatusEmoji.Text = _user.StatusEmoji ?? "💭";
        }
        else
        {
            CustomStatusBorder.Visibility = Visibility.Collapsed;
        }

        // Set bio/about me
        if (!string.IsNullOrEmpty(_user.Bio))
        {
            AboutSection.Visibility = Visibility.Visible;
            BioText.Text = _user.Bio;
        }
        else
        {
            AboutSection.Visibility = Visibility.Collapsed;
        }

        // Set member since date
        MemberSinceText.Text = _user.CreatedAt.ToString("MMM d, yyyy");

        // Set banner colors based on user preferences or defaults
        if (!string.IsNullOrEmpty(_user.BannerColor1) && !string.IsNullOrEmpty(_user.BannerColor2))
        {
            try
            {
                BannerColor1.Color = (Color)ColorConverter.ConvertFromString(_user.BannerColor1);
                BannerColor2.Color = (Color)ColorConverter.ConvertFromString(_user.BannerColor2);
            }
            catch
            {
                // Keep default colors
            }
        }

        // Load additional data asynchronously
        _ = LoadAdditionalDataAsync();
    }

    private void UpdateStatusIndicator(UserStatus status)
    {
        var statusBrush = status switch
        {
            UserStatus.Online => FindResource("AccentGreenBrush") as Brush,
            UserStatus.Idle => FindResource("AccentYellowBrush") as Brush,
            UserStatus.DoNotDisturb => FindResource("AccentRedBrush") as Brush,
            UserStatus.Invisible or UserStatus.Offline => FindResource("TextMutedBrush") as Brush,
            _ => FindResource("TextMutedBrush") as Brush
        };

        StatusIndicator.Background = statusBrush ?? Brushes.Gray;
    }

    private async Task LoadAdditionalDataAsync()
    {
        if (_user == null || _friendService == null) return;

        try
        {
            // Load mutual friends
            var mutualFriends = await _friendService.GetMutualFriendsAsync(_user.Id);
            if (mutualFriends.Any())
            {
                MutualFriendsSection.Visibility = Visibility.Visible;
                MutualFriendsCountText.Text = mutualFriends.Count.ToString();

                MutualFriendsAvatars.Children.Clear();
                foreach (var friend in mutualFriends.Take(5))
                {
                    var avatar = CreateMutualFriendAvatar(friend);
                    MutualFriendsAvatars.Children.Add(avatar);
                }

                if (mutualFriends.Count > 5)
                {
                    var moreIndicator = new Border
                    {
                        Width = 28,
                        Height = 28,
                        CornerRadius = new CornerRadius(14),
                        Background = FindResource("TertiaryDarkBrush") as Brush,
                        Margin = new Thickness(-8, 0, 0, 0)
                    };
                    moreIndicator.Child = new TextBlock
                    {
                        Text = $"+{mutualFriends.Count - 5}",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = FindResource("TextPrimaryBrush") as Brush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    MutualFriendsAvatars.Children.Add(moreIndicator);
                }
            }

            // Load roles if available
            if (_user.Roles?.Any() == true)
            {
                RolesSection.Visibility = Visibility.Visible;
                RolesItemsControl.ItemsSource = _user.Roles;
            }

            // Update friend action button based on relationship
            UpdateFriendActionButton();

            // Load user note
            var note = await _friendService.GetUserNoteAsync(_user.Id);
            if (!string.IsNullOrEmpty(note))
            {
                NoteSection.Visibility = Visibility.Visible;
                NoteTextBox.Text = note;
                NoteTextBox.Foreground = FindResource("TextPrimaryBrush") as Brush;
                _noteText = note;
                _isNotePlaceholder = false;
            }
            else
            {
                NoteSection.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Silently handle errors for additional data
        }
    }

    private Border CreateMutualFriendAvatar(UserDto friend)
    {
        var border = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(2),
            BorderBrush = FindResource("PrimaryDarkBrush") as Brush,
            Margin = new Thickness(-8, 0, 0, 0),
            ToolTip = friend.DisplayName ?? friend.Username
        };

        var ellipse = new Ellipse
        {
            Width = 24,
            Height = 24
        };

        if (!string.IsNullOrEmpty(friend.AvatarUrl))
        {
            try
            {
                ellipse.Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(friend.AvatarUrl)),
                    Stretch = Stretch.UniformToFill
                };
            }
            catch
            {
                ellipse.Fill = FindResource("AccentBrush") as Brush;
            }
        }
        else
        {
            ellipse.Fill = FindResource("AccentBrush") as Brush;
        }

        border.Child = ellipse;
        return border;
    }

    private async void UpdateFriendActionButton()
    {
        if (_user == null || _friendService == null) return;

        try
        {
            var relationship = await _friendService.GetRelationshipAsync(_user.Id);

            switch (relationship)
            {
                case FriendRelationship.Friends:
                    FriendActionButton.Visibility = Visibility.Collapsed;
                    break;
                case FriendRelationship.PendingOutgoing:
                    FriendActionButton.Visibility = Visibility.Visible;
                    FriendActionButton.Content = "Request Sent";
                    FriendActionButton.IsEnabled = false;
                    break;
                case FriendRelationship.PendingIncoming:
                    FriendActionButton.Visibility = Visibility.Visible;
                    FriendActionButton.Content = "Accept Request";
                    FriendActionButton.IsEnabled = true;
                    break;
                case FriendRelationship.None:
                default:
                    FriendActionButton.Visibility = Visibility.Visible;
                    FriendActionButton.Content = "Add Friend";
                    FriendActionButton.IsEnabled = true;
                    break;
            }
        }
        catch
        {
            FriendActionButton.Visibility = Visibility.Collapsed;
        }
    }

    private void MessageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        MessageRequested?.Invoke(this, _user.Id);
        _navigationService?.NavigateTo($"DirectMessage:{_user.Id}");
    }

    private void CallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        CallRequested?.Invoke(this, _user.Id);
    }

    private void VideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        VideoCallRequested?.Invoke(this, _user.Id);
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu();

        var blockItem = new MenuItem { Header = "Block User" };
        blockItem.Click += async (s, args) =>
        {
            if (_user != null && _friendService != null)
            {
                await _friendService.BlockUserAsync(_user.Id);
            }
        };

        var reportItem = new MenuItem { Header = "Report User" };
        reportItem.Click += (s, args) =>
        {
            _navigationService?.NavigateTo($"Report:User:{_user?.Id}");
        };

        var copyIdItem = new MenuItem { Header = "Copy User ID" };
        copyIdItem.Click += (s, args) =>
        {
            if (_user != null)
            {
                Clipboard.SetText(_user.Id);
            }
        };

        contextMenu.Items.Add(blockItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(reportItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(copyIdItem);

        contextMenu.PlacementTarget = sender as Button;
        contextMenu.IsOpen = true;
    }

    private async void FriendActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null || _friendService == null) return;

        try
        {
            var relationship = await _friendService.GetRelationshipAsync(_user.Id);

            if (relationship == FriendRelationship.PendingIncoming)
            {
                await _friendService.AcceptFriendRequestAsync(_user.Id);
            }
            else if (relationship == FriendRelationship.None)
            {
                await _friendService.SendFriendRequestAsync(_user.Id);
            }

            UpdateFriendActionButton();
        }
        catch
        {
            // Handle error
        }
    }

    private void NoteTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_isNotePlaceholder)
        {
            NoteTextBox.Text = string.Empty;
            NoteTextBox.Foreground = FindResource("TextPrimaryBrush") as Brush;
            _isNotePlaceholder = false;
        }
    }

    private async void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoteTextBox.Text))
        {
            NoteTextBox.Text = "Click to add a note";
            NoteTextBox.Foreground = FindResource("TextMutedBrush") as Brush;
            _isNotePlaceholder = true;
        }
        else if (NoteTextBox.Text != _noteText && _user != null && _friendService != null)
        {
            _noteText = NoteTextBox.Text;
            await _friendService.SetUserNoteAsync(_user.Id, _noteText);
        }
    }
}

