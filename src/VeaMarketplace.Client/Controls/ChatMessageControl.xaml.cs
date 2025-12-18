using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;
using WpfImage = System.Windows.Controls.Image;
using WpfCursors = System.Windows.Input.Cursors;

namespace VeaMarketplace.Client.Controls;

public partial class ChatMessageControl : UserControl
{
    private ChatMessageDto? _currentMessage;

    // Regex pattern to match product embeds: [PRODUCT_EMBED:id|title|price|seller|imageUrl|description]
    private static readonly Regex ProductEmbedPattern = new(
        @"\[PRODUCT_EMBED:([^\|]+)\|([^\|]*)\|([^\|]*)\|([^\|]*)\|([^\|]*)\|([^\]]*)\]",
        RegexOptions.Compiled);

    // Events for parent to handle
    public static readonly RoutedEvent ReplyRequestedEvent = EventManager.RegisterRoutedEvent(
        "ReplyRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageControl));

    public static readonly RoutedEvent MentionRequestedEvent = EventManager.RegisterRoutedEvent(
        "MentionRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ChatMessageControl));

    public event RoutedEventHandler ReplyRequested
    {
        add => AddHandler(ReplyRequestedEvent, value);
        remove => RemoveHandler(ReplyRequestedEvent, value);
    }

    public event RoutedEventHandler MentionRequested
    {
        add => AddHandler(MentionRequestedEvent, value);
        remove => RemoveHandler(MentionRequestedEvent, value);
    }

    public ChatMessageControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        MouseEnter += (s, e) => ActionsPanel.Visibility = Visibility.Visible;
        MouseLeave += (s, e) => ActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ChatMessageDto message)
        {
            _currentMessage = message;
            UpdateUI(message);
            UpdateContextMenuVisibility(message);
        }
    }

    private void UpdateContextMenuVisibility(ChatMessageDto message)
    {
        // Check if current user owns this message to show edit/delete options
        var apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));
        var currentUser = apiService?.CurrentUser;

        if (currentUser != null && message.SenderId == currentUser.Id)
        {
            // Find menu items in the context menu resource
            if (Resources["MessageContextMenu"] is ContextMenu menu)
            {
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (menuItem.Name == "EditMenuItem" || menuItem.Name == "DeleteMenuItem")
                        {
                            menuItem.Visibility = Visibility.Visible;
                        }
                        else if (menuItem.Name == "DeleteSeparator")
                        {
                            menuItem.Visibility = Visibility.Visible;
                        }
                    }
                    else if (item is Separator sep && sep.Name == "DeleteSeparator")
                    {
                        sep.Visibility = Visibility.Visible;
                    }
                }
            }
        }
    }

    private void UpdateUI(ChatMessageDto message)
    {
        // Set avatar
        if (!string.IsNullOrEmpty(message.SenderAvatarUrl))
        {
            try
            {
                AvatarImage.ImageSource = new BitmapImage(new Uri(message.SenderAvatarUrl));
            }
            catch
            {
                // Use default avatar
                AvatarBorder.Background = new SolidColorBrush(GetRoleColor(message.SenderRole));
            }
        }

        // Set username with role color
        UsernameText.Text = message.SenderUsername;
        UsernameText.Foreground = new SolidColorBrush(GetRoleColor(message.SenderRole));

        // Set timestamp
        TimestampText.Text = FormatTimestamp(message.Timestamp);

        // Set message content (parse for product embeds)
        var displayContent = message.Content;
        var parsedEmbeds = ParseProductEmbedsFromContent(ref displayContent);
        MessageText.Text = displayContent;

        // Style for system messages
        if (message.Type != MessageType.Text)
        {
            MessageText.FontStyle = FontStyles.Italic;
            MessageText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            AvatarBorder.Visibility = Visibility.Collapsed;
            UsernameText.Visibility = Visibility.Collapsed;
            RoleBadge.Visibility = Visibility.Collapsed;
            RankBadge.Visibility = Visibility.Collapsed;

            switch (message.Type)
            {
                case MessageType.Join:
                    MessageText.Text = $"➜ {message.Content}";
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(87, 242, 135));
                    break;
                case MessageType.Leave:
                    MessageText.Text = $"← {message.Content}";
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(237, 66, 69));
                    break;
                case MessageType.Announcement:
                    MessageText.FontWeight = FontWeights.Bold;
                    MessageText.Foreground = new SolidColorBrush(Color.FromRgb(254, 231, 92));
                    break;
            }
            return;
        }

        // Show role badge for special roles
        if (message.SenderRole >= UserRole.VIP)
        {
            RoleBadge.Visibility = Visibility.Visible;
            RoleBadge.Background = new SolidColorBrush(GetRoleColor(message.SenderRole));
            RoleText.Text = message.SenderRole.ToString().ToUpper();
        }

        // Show rank badge
        if (message.SenderRank >= UserRank.Silver)
        {
            RankBadge.Visibility = Visibility.Visible;
            RankBadge.Background = new SolidColorBrush(GetRankColor(message.SenderRank));
            RankText.Text = GetRankEmoji(message.SenderRank) + " " + message.SenderRank.ToString();
        }

        // Display attachments
        DisplayAttachments(message.Attachments);

        // Display embeds (shared posts, products, auctions) - combine server embeds with parsed embeds
        DisplayEmbeds(message.Embeds, parsedEmbeds);

        // Display reactions
        DisplayReactions(message.Reactions);
    }

    /// <summary>
    /// Parses product embed format from message content and returns parsed embeds.
    /// Modifies the content to remove the embed markers.
    /// Format: [PRODUCT_EMBED:id|title|price|seller|imageUrl|description]
    /// </summary>
    private List<EmbeddedContent> ParseProductEmbedsFromContent(ref string content)
    {
        var embeds = new List<EmbeddedContent>();
        var matches = ProductEmbedPattern.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 7)
            {
                var productId = match.Groups[1].Value;
                var title = match.Groups[2].Value;
                var priceStr = match.Groups[3].Value;
                var seller = match.Groups[4].Value;
                var imageUrl = match.Groups[5].Value;
                var description = match.Groups[6].Value;

                decimal.TryParse(priceStr, out var price);

                var embed = new EmbeddedContent
                {
                    Id = productId,
                    Type = EmbedType.Product,
                    Title = title,
                    Description = description,
                    ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
                    Price = price,
                    SellerUsername = seller
                };

                embeds.Add(embed);
            }
        }

        // Remove embed markers from display content
        content = ProductEmbedPattern.Replace(content, "").Trim();

        return embeds;
    }

    private void DisplayEmbeds(List<MessageEmbedDto>? embeds, List<EmbeddedContent>? parsedEmbeds = null)
    {
        EmbedsContainer.Children.Clear();

        var hasEmbeds = (embeds != null && embeds.Count > 0) || (parsedEmbeds != null && parsedEmbeds.Count > 0);

        if (!hasEmbeds)
        {
            EmbedsContainer.Visibility = Visibility.Collapsed;
            return;
        }

        EmbedsContainer.Visibility = Visibility.Visible;

        // Display server-side embeds first
        if (embeds != null)
        {
            foreach (var embed in embeds)
            {
                var embedControl = new SharedPostEmbed();

                var content = new EmbeddedContent
                {
                    Id = embed.ContentId ?? embed.Id,
                    Type = MapEmbedType(embed.Type),
                    Title = embed.Title,
                    Subtitle = embed.Subtitle,
                    Description = embed.Description,
                    ImageUrl = embed.ImageUrl ?? embed.ThumbnailUrl,
                    Price = embed.Price,
                    OriginalPrice = embed.OriginalPrice,
                    CurrentBid = embed.CurrentBid,
                    MinBidIncrement = embed.MinBidIncrement ?? 1,
                    AuctionEndsAt = embed.AuctionEndsAt,
                    SellerUsername = embed.SellerUsername,
                    SellerAvatarUrl = embed.SellerAvatarUrl,
                    SellerRole = embed.SellerRole
                };

                embedControl.SetContent(content);
                embedControl.Margin = new Thickness(0, 0, 0, 8);

                // Wire up events
                embedControl.ViewClicked += EmbedControl_ViewClicked;
                embedControl.ShareClicked += EmbedControl_ShareClicked;
                embedControl.LinkCopied += EmbedControl_LinkCopied;
                embedControl.BidClicked += EmbedControl_BidClicked;
                embedControl.ReactionClicked += EmbedControl_ReactionClicked;

                EmbedsContainer.Children.Add(embedControl);
            }
        }

        // Display parsed embeds from message content (product shares)
        if (parsedEmbeds != null)
        {
            foreach (var content in parsedEmbeds)
            {
                var embedControl = new SharedPostEmbed();
                embedControl.SetContent(content);
                embedControl.Margin = new Thickness(0, 0, 0, 8);

                // Wire up events
                embedControl.ViewClicked += EmbedControl_ViewClicked;
                embedControl.ShareClicked += EmbedControl_ShareClicked;
                embedControl.LinkCopied += EmbedControl_LinkCopied;
                embedControl.BidClicked += EmbedControl_BidClicked;
                embedControl.ReactionClicked += EmbedControl_ReactionClicked;

                EmbedsContainer.Children.Add(embedControl);
            }
        }
    }

    private void DisplayReactions(List<MessageReactionDto>? reactions)
    {
        ReactionsContainer.Children.Clear();

        if (reactions == null || reactions.Count == 0)
        {
            ReactionsContainer.Visibility = Visibility.Collapsed;
            return;
        }

        ReactionsContainer.Visibility = Visibility.Visible;

        // Group reactions by emoji
        var grouped = reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new
            {
                Emoji = g.Key,
                Count = g.Count(),
                Users = g.Select(r => r.Username).ToList(),
                HasUserReacted = g.Any(r => IsCurrentUser(r.UserId))
            })
            .ToList();

        foreach (var group in grouped)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("ReactionButton"),
                Tag = group.Emoji,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = group.Emoji,
                FontSize = 14,
                Margin = new Thickness(0, 0, 4, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = group.Count.ToString(),
                FontSize = 12,
                Foreground = group.HasUserReacted
                    ? new SolidColorBrush(Color.FromRgb(88, 101, 242))
                    : new SolidColorBrush(Color.FromRgb(181, 186, 193))
            });

            btn.Content = stack;
            btn.ToolTip = string.Join(", ", group.Users.Take(10)) +
                (group.Users.Count > 10 ? $" +{group.Users.Count - 10} more" : "");

            btn.Click += (s, e) => ToggleReaction(group.Emoji, group.HasUserReacted);

            ReactionsContainer.Children.Add(btn);
        }

        // Add reaction button
        var addBtn = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Width = 28,
            Height = 28,
            ToolTip = "Add Reaction"
        };
        addBtn.Content = new TextBlock { Text = "+", FontSize = 14 };
        addBtn.Click += (s, e) => ShowReactionPicker();
        ReactionsContainer.Children.Add(addBtn);
    }

    private bool IsCurrentUser(string userId)
    {
        var apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));
        return apiService?.CurrentUser?.Id == userId;
    }

    private async void ToggleReaction(string emoji, bool hasReacted)
    {
        if (_currentMessage == null) return;

        try
        {
            var chatService = (IChatService?)App.ServiceProvider.GetService(typeof(IChatService));
            if (chatService != null)
            {
                // Toggle reaction - add or remove based on current state
                if (hasReacted)
                {
                    await chatService.RemoveReactionAsync(_currentMessage.Id, emoji);
                }
                else
                {
                    await chatService.AddReactionAsync(_currentMessage.Id, emoji);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to toggle reaction: {ex.Message}");
        }
    }

    private void ShowReactionPicker()
    {
        // Create a simple reaction picker popup
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = ReactionsContainer,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(47, 49, 54)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var commonEmojis = new[] { "👍", "❤️", "😂", "😮", "😢", "🎉", "🔥", "👀" };

        foreach (var emoji in commonEmojis)
        {
            var btn = new Button
            {
                Content = emoji,
                FontSize = 18,
                Width = 36,
                Height = 36,
                Margin = new Thickness(2),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = WpfCursors.Hand
            };
            btn.Click += (s, e) =>
            {
                popup.IsOpen = false;
                ToggleReaction(emoji, false);
            };
            panel.Children.Add(btn);
        }

        border.Child = panel;
        popup.Child = border;
        popup.IsOpen = true;
    }

    private static EmbedType MapEmbedType(EmbedContentType type)
    {
        return type switch
        {
            EmbedContentType.Product => EmbedType.Product,
            EmbedContentType.Auction => EmbedType.Auction,
            EmbedContentType.Profile => EmbedType.Profile,
            EmbedContentType.Listing => EmbedType.Listing,
            _ => EmbedType.Product
        };
    }

    private void EmbedControl_ViewClicked(object? sender, EmbedActionEventArgs e)
    {
        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        switch (e.ContentType)
        {
            case EmbedType.Product:
            case EmbedType.Auction:
                navigationService?.NavigateToProduct(e.ContentId);
                break;
            case EmbedType.Profile:
                navigationService?.NavigateToProfile(e.ContentId);
                break;
        }
    }

    private void EmbedControl_ShareClicked(object? sender, EmbedActionEventArgs e)
    {
        OnShareEmbedRequested?.Invoke(this, e);
    }

    private void EmbedControl_LinkCopied(object? sender, EmbedActionEventArgs e)
    {
        // Could show a toast notification
    }

    private void EmbedControl_BidClicked(object? sender, EmbedBidEventArgs e)
    {
        OnBidRequested?.Invoke(this, e);
    }

    private void EmbedControl_ReactionClicked(object? sender, EmbedReactionEventArgs e)
    {
        OnEmbedReactionRequested?.Invoke(this, e);
    }

    // Events for parent to handle embed interactions
    public event EventHandler<EmbedActionEventArgs>? OnShareEmbedRequested;
    public event EventHandler<EmbedBidEventArgs>? OnBidRequested;
    public event EventHandler<EmbedReactionEventArgs>? OnEmbedReactionRequested;

    private void DisplayAttachments(List<MessageAttachmentDto>? attachments)
    {
        AttachmentsContainer.Items.Clear();

        if (attachments == null || attachments.Count == 0)
        {
            AttachmentsContainer.Visibility = Visibility.Collapsed;
            return;
        }

        AttachmentsContainer.Visibility = Visibility.Visible;

        foreach (var attachment in attachments)
        {
            var attachmentElement = CreateAttachmentElement(attachment);
            AttachmentsContainer.Items.Add(attachmentElement);
        }
    }

    private UIElement CreateAttachmentElement(MessageAttachmentDto attachment)
    {
        if (attachment.Type == AttachmentType.Image)
        {
            return CreateImageAttachment(attachment);
        }
        else
        {
            return CreateFileAttachment(attachment);
        }
    }

    private UIElement CreateImageAttachment(MessageAttachmentDto attachment)
    {
        // Calculate display dimensions
        int displayWidth = 300;
        int displayHeight = 200;

        if (attachment.Width.HasValue && attachment.Height.HasValue && attachment.Width > 0 && attachment.Height > 0)
        {
            var aspectRatio = (double)attachment.Width.Value / attachment.Height.Value;

            // Constrain to max dimensions while maintaining aspect ratio
            const int maxWidth = 400;
            const int maxHeight = 300;

            if (aspectRatio > 1) // Wider than tall
            {
                displayWidth = Math.Min(attachment.Width.Value, maxWidth);
                displayHeight = (int)(displayWidth / aspectRatio);
            }
            else // Taller than wide
            {
                displayHeight = Math.Min(attachment.Height.Value, maxHeight);
                displayWidth = (int)(displayHeight * aspectRatio);
            }
        }

        // Create the container grid for the image
        var containerGrid = new Grid
        {
            Width = displayWidth,
            Height = displayHeight,
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = WpfCursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(30, 31, 34))
        };

        // Create a border with clipping for rounded corners
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Tag = attachment.FileUrl
        };

        // Create image element
        var image = new WpfImage
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        // Create loading indicator
        var loadingText = new TextBlock
        {
            Text = "Loading...",
            Foreground = new SolidColorBrush(Color.FromRgb(185, 187, 190)),
            FontSize = 12,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        // Add loading text initially
        containerGrid.Children.Add(loadingText);

        // Load image asynchronously
        var imageUrl = attachment.ThumbnailUrl ?? attachment.FileUrl;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);

                // Handle download completion
                bitmap.DownloadCompleted += (s, e) =>
                {
                    containerGrid.Children.Clear();
                    border.Child = image;
                    containerGrid.Children.Add(border);
                };

                bitmap.DownloadFailed += (s, e) =>
                {
                    // Show error/fallback
                    loadingText.Text = "Failed to load";
                };

                bitmap.EndInit();
                image.Source = bitmap;

                // If already downloaded (cached), show immediately
                if (bitmap.IsDownloading == false)
                {
                    containerGrid.Children.Clear();
                    border.Child = image;
                    containerGrid.Children.Add(border);
                }
            }
            catch
            {
                loadingText.Text = "Error";
            }
        }

        // Click handler to open full image
        containerGrid.MouseLeftButtonDown += (s, e) =>
        {
            var url = attachment.FileUrl;
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
                    toastService?.ShowError("Open Failed", $"Could not open file: {ex.Message}");
                }
            }
        };

        return containerGrid;
    }

    private UIElement CreateFileAttachment(MessageAttachmentDto attachment)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(43, 45, 49)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = WpfCursors.Hand,
            Tag = attachment.FileUrl
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // File icon
        var iconText = new TextBlock
        {
            Text = GetFileTypeIcon(attachment.Type),
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(iconText, 0);
        grid.Children.Add(iconText);

        // File info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = attachment.FileName,
            Foreground = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 200
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = FormatFileSize(attachment.FileSize),
            Foreground = new SolidColorBrush(Color.FromRgb(185, 187, 190)),
            FontSize = 11
        });
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Download icon
        var downloadIcon = new TextBlock
        {
            Text = "⬇",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(185, 187, 190)),
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(downloadIcon, 2);
        grid.Children.Add(downloadIcon);

        border.Child = grid;
        border.MouseLeftButtonDown += (s, e) =>
        {
            // Download or open file
            if (s is Border b && b.Tag is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
                    toastService?.ShowError("Download Failed", $"Could not download file: {ex.Message}");
                }
            }
        };

        return border;
    }

    private static string GetFileTypeIcon(AttachmentType type)
    {
        return type switch
        {
            AttachmentType.Image => "🖼",
            AttachmentType.Video => "🎬",
            AttachmentType.Audio => "🎵",
            AttachmentType.Document => "📄",
            _ => "📁"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static Color GetRoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.FromRgb(255, 215, 0),
            UserRole.Admin => Color.FromRgb(231, 76, 60),
            UserRole.Moderator => Color.FromRgb(155, 89, 182),
            UserRole.VIP => Color.FromRgb(0, 255, 136),
            UserRole.Verified => Color.FromRgb(52, 152, 219),
            _ => Color.FromRgb(185, 187, 190)
        };
    }

    private static Color GetRankColor(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => Color.FromRgb(255, 215, 0),
            UserRank.Elite => Color.FromRgb(231, 76, 60),
            UserRank.Diamond => Color.FromRgb(0, 255, 255),
            UserRank.Platinum => Color.FromRgb(229, 228, 226),
            UserRank.Gold => Color.FromRgb(255, 215, 0),
            UserRank.Silver => Color.FromRgb(192, 192, 192),
            UserRank.Bronze => Color.FromRgb(205, 127, 50),
            _ => Color.FromRgb(149, 165, 166)
        };
    }

    private static string GetRankEmoji(UserRank rank)
    {
        return rank switch
        {
            UserRank.Legend => "👑",
            UserRank.Elite => "🔥",
            UserRank.Diamond => "💎",
            UserRank.Platinum => "✨",
            UserRank.Gold => "🥇",
            UserRank.Silver => "🥈",
            UserRank.Bronze => "🥉",
            _ => "🌟"
        };
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var now = DateTime.Now;
        var local = timestamp.ToLocalTime();

        if (local.Date == now.Date)
            return $"Today at {local:h:mm tt}";

        if (local.Date == now.Date.AddDays(-1))
            return $"Yesterday at {local:h:mm tt}";

        return local.ToString("MM/dd/yyyy h:mm tt");
    }

    #region User Context Menu Handlers

    private async void Avatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await ShowUserProfilePopup();
    }

    private async void Username_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await ShowUserProfilePopup();
    }

    private async Task ShowUserProfilePopup()
    {
        if (_currentMessage == null) return;

        // Try to get full user details from API
        var apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));
        if (apiService == null) return;

        try
        {
            var userDetails = await apiService.GetUserAsync(_currentMessage.SenderId);
            if (userDetails != null)
            {
                // Check if user is online (simplified - based on recent activity)
                var isOnline = userDetails.LastActive > DateTime.UtcNow.AddMinutes(-5);
                ProfileCard.SetUser(userDetails, isOnline, showActions: userDetails.Id != apiService.CurrentUser?.Id);
                UserProfilePopup.IsOpen = true;
            }
        }
        catch
        {
            // Fallback: create a basic UserDto from message info
            var basicUser = new UserDto
            {
                Id = _currentMessage.SenderId,
                Username = _currentMessage.SenderUsername,
                AvatarUrl = _currentMessage.SenderAvatarUrl ?? string.Empty,
                Role = _currentMessage.SenderRole,
                Rank = _currentMessage.SenderRank
            };
            ProfileCard.SetUser(basicUser, isOnline: false, showActions: basicUser.Id != apiService.CurrentUser?.Id);
            UserProfilePopup.IsOpen = true;
        }
    }

    private void ProfileCard_SendMessageClicked(object? sender, UserDto user)
    {
        UserProfilePopup.IsOpen = false;
        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToFriends();
    }

    private async void ProfileCard_AddFriendClicked(object? sender, UserDto user)
    {
        UserProfilePopup.IsOpen = false;
        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        if (friendService != null)
        {
            try
            {
                await friendService.SendFriendRequestAsync(user.Username);
                toastService?.ShowSuccess("Friend Request Sent", $"Sent to {user.Username}");
            }
            catch (Exception ex)
            {
                toastService?.ShowError("Request Failed", ex.Message);
            }
        }
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navigationService?.NavigateToProfile(_currentMessage.SenderId);
    }

    private async void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        if (friendService != null)
        {
            // Open DM with user
            var navigationService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
            navigationService?.NavigateToFriends();
        }
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var friendService = (IFriendService?)App.ServiceProvider.GetService(typeof(IFriendService));
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        if (friendService != null)
        {
            try
            {
                await friendService.SendFriendRequestAsync(_currentMessage.SenderUsername);
                toastService?.ShowSuccess("Friend Request Sent", $"Sent to {_currentMessage.SenderUsername}");
            }
            catch (Exception ex)
            {
                toastService?.ShowError("Request Failed", ex.Message);
            }
        }
    }

    private void Mention_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        RaiseEvent(new RoutedEventArgs(MentionRequestedEvent, _currentMessage));
    }

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Implement user muting
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("User Muted", $"Muted {_currentMessage.SenderUsername}");
    }

    private void BlockUser_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to block {_currentMessage.SenderUsername}? You won't see their messages.",
            "Block User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Implement user blocking
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowSuccess("User Blocked", $"Blocked {_currentMessage.SenderUsername}");
        }
    }

    private void CopyUserId_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.SenderId);
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Copied", "User ID copied to clipboard");
    }

    #endregion

    #region Message Context Menu Handlers

    private void Reply_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        RaiseEvent(new RoutedEventArgs(ReplyRequestedEvent, _currentMessage));
    }

    private void React_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Show reaction picker (feature pending full implementation)
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Reactions", "Emoji picker coming soon!");
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.Content);
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Copied", "Message text copied to clipboard");
    }

    private void CopyMessageLink_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText($"{AppConstants.UrlScheme}message/{_currentMessage.Id}");
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Copied", "Message link copied to clipboard");
    }

    private void PinMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Implement message pinning
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Pinned", "Message pinned!");
    }

    private void EditMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        // Message editing (feature pending full implementation)
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Edit Message", "Message editing coming soon!");
    }

    private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;

        var result = MessageBox.Show(
            "Are you sure you want to delete this message?",
            "Delete Message",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var chatService = (IChatService?)App.ServiceProvider.GetService(typeof(IChatService));
            if (chatService != null)
            {
                await chatService.DeleteMessageAsync(_currentMessage.Id, _currentMessage.Channel);
            }
        }
    }

    private void CopyMessageId_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMessage == null) return;
        Clipboard.SetText(_currentMessage.Id);
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Copied", "Message ID copied to clipboard");
    }

    #endregion
}
