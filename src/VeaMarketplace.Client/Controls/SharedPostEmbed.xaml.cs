using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VeaMarketplace.Client.Controls;

public partial class SharedPostEmbed : UserControl
{
    private EmbeddedContent? _content;
    private List<EmbedReaction> _reactions = new();

    public event EventHandler<EmbedActionEventArgs>? ViewClicked;
    public event EventHandler<EmbedActionEventArgs>? ShareClicked;
    public event EventHandler<EmbedActionEventArgs>? LinkCopied;
    public event EventHandler<EmbedBidEventArgs>? BidClicked;
    public event EventHandler<EmbedReactionEventArgs>? ReactionClicked;
    public event EventHandler<EmbedActionEventArgs>? CardClicked;

    public SharedPostEmbed()
    {
        InitializeComponent();
    }

    public void SetContent(EmbeddedContent content)
    {
        _content = content;
        UpdateDisplay();
    }

    public void SetReactions(List<EmbedReaction> reactions)
    {
        _reactions = reactions;
        UpdateReactions();
    }

    private void UpdateDisplay()
    {
        if (_content == null) return;

        // Set type badge
        TypeText.Text = _content.Type.ToString().ToUpper();
        TypeBadge.Background = _content.Type switch
        {
            EmbedType.Product => new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            EmbedType.Auction => new LinearGradientBrush(
                Color.FromRgb(250, 166, 26),
                Color.FromRgb(245, 127, 23), 0),
            EmbedType.Profile => new SolidColorBrush(Color.FromRgb(67, 181, 129)),
            EmbedType.Listing => new SolidColorBrush(Color.FromRgb(235, 69, 158)),
            _ => new SolidColorBrush(Color.FromRgb(88, 101, 242))
        };

        // Set title
        TitleText.Text = _content.Title;
        TitleText.ToolTip = _content.Title;

        // Set price/subtitle
        if (_content.Price.HasValue)
        {
            PriceText.Text = $"${_content.Price:F2}";
            PriceText.Visibility = Visibility.Visible;

            if (_content.OriginalPrice.HasValue && _content.OriginalPrice > _content.Price)
            {
                OriginalPriceText.Text = $"${_content.OriginalPrice:F2}";
                OriginalPriceText.Visibility = Visibility.Visible;

                var discount = (int)((1 - _content.Price.Value / _content.OriginalPrice.Value) * 100);
                SaleText.Text = $"-{discount}%";
                SaleBadge.Visibility = Visibility.Visible;
            }
            else
            {
                OriginalPriceText.Visibility = Visibility.Collapsed;
                SaleBadge.Visibility = Visibility.Collapsed;
            }
        }
        else if (!string.IsNullOrEmpty(_content.Subtitle))
        {
            PriceText.Text = _content.Subtitle;
            PriceText.Foreground = new SolidColorBrush(Color.FromRgb(181, 186, 193));
            OriginalPriceText.Visibility = Visibility.Collapsed;
            SaleBadge.Visibility = Visibility.Collapsed;
        }

        // Set image
        if (!string.IsNullOrEmpty(_content.ImageUrl))
        {
            try
            {
                PostImage.Source = new BitmapImage(new Uri(_content.ImageUrl));
                ImagePlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ImagePlaceholder.Visibility = Visibility.Visible;
            }
        }
        else
        {
            ImagePlaceholder.Text = _content.Type switch
            {
                EmbedType.Product => "📦",
                EmbedType.Auction => "🔨",
                EmbedType.Profile => "👤",
                EmbedType.Listing => "📋",
                _ => "📦"
            };
            ImagePlaceholder.Visibility = Visibility.Visible;
        }

        // Set seller info
        if (!string.IsNullOrEmpty(_content.SellerUsername))
        {
            SellerName.Text = _content.SellerUsername;

            if (!string.IsNullOrEmpty(_content.SellerAvatarUrl))
            {
                try
                {
                    SellerAvatar.Source = new BitmapImage(new Uri(_content.SellerAvatarUrl));
                }
                catch { }
            }

            SellerBadge.Text = _content.SellerRole switch
            {
                "Owner" => "👑",
                "Admin" => "🛡️",
                "Moderator" => "⚔️",
                "VIP" => "💎",
                "Verified" => "✓",
                _ => ""
            };
        }

        // Show bid button for auctions
        if (_content.Type == EmbedType.Auction)
        {
            BidButton.Visibility = Visibility.Visible;
            if (_content.CurrentBid.HasValue)
            {
                BidButtonText.Text = $"Bid ${_content.CurrentBid + 1:F2}";
            }
        }
        else
        {
            BidButton.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateReactions()
    {
        ReactionsPanel.Children.Clear();

        foreach (var reaction in _reactions.Take(8)) // Limit visible reactions
        {
            var btn = new Button
            {
                Style = (Style)FindResource("ReactionButton"),
                Tag = reaction.Emoji,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = reaction.Emoji,
                FontSize = 14,
                Margin = new Thickness(0, 0, 4, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = reaction.Count.ToString(),
                FontSize = 12,
                Foreground = reaction.HasUserReacted
                    ? new SolidColorBrush(Color.FromRgb(88, 101, 242))
                    : new SolidColorBrush(Color.FromRgb(181, 186, 193))
            });

            btn.Content = stack;
            btn.ToolTip = string.Join(", ", reaction.Users.Take(10)) +
                (reaction.Users.Count > 10 ? $" +{reaction.Users.Count - 10} more" : "");
            btn.Click += (s, e) =>
            {
                ReactionClicked?.Invoke(this, new EmbedReactionEventArgs
                {
                    ContentId = _content?.Id ?? "",
                    Emoji = reaction.Emoji,
                    ShouldRemove = reaction.HasUserReacted
                });
            };

            ReactionsPanel.Children.Add(btn);
        }

        ReactionsPanel.Visibility = _reactions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Card_MouseEnter(object sender, MouseEventArgs e)
    {
        var storyboard = (Storyboard)FindResource("HoverEnter");
        storyboard.Begin(this);
    }

    private void Card_MouseLeave(object sender, MouseEventArgs e)
    {
        var storyboard = (Storyboard)FindResource("HoverLeave");
        storyboard.Begin(this);
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (_content != null)
        {
            CardClicked?.Invoke(this, new EmbedActionEventArgs { ContentId = _content.Id, ContentType = _content.Type });
        }
    }

    private void ReactButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // Show reaction picker - this would typically show a popup
        // For now, add a default reaction
        ReactionClicked?.Invoke(this, new EmbedReactionEventArgs
        {
            ContentId = _content?.Id ?? "",
            Emoji = "👍",
            ShouldRemove = false
        });
    }

    private async void CopyLinkButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_content == null) return;

        try
        {
            var link = $"vea://marketplace/{_content.Type.ToString().ToLower()}/{_content.Id}";
            Clipboard.SetText(link);

            // Show success overlay
            CopySuccessOverlay.Visibility = Visibility.Visible;
            CopySuccessOverlay.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            CopySuccessOverlay.BeginAnimation(OpacityProperty, fadeIn);

            await Task.Delay(1200);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, ev) => CopySuccessOverlay.Visibility = Visibility.Collapsed;
            CopySuccessOverlay.BeginAnimation(OpacityProperty, fadeOut);

            LinkCopied?.Invoke(this, new EmbedActionEventArgs
            {
                ContentId = _content.Id,
                ContentType = _content.Type
            });
        }
        catch { }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_content != null)
        {
            ShareClicked?.Invoke(this, new EmbedActionEventArgs
            {
                ContentId = _content.Id,
                ContentType = _content.Type
            });
        }
    }

    private void BidButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_content != null)
        {
            BidClicked?.Invoke(this, new EmbedBidEventArgs
            {
                ContentId = _content.Id,
                CurrentBid = _content.CurrentBid ?? 0,
                MinIncrement = _content.MinBidIncrement ?? 1
            });
        }
    }

    private void ViewButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_content != null)
        {
            ViewClicked?.Invoke(this, new EmbedActionEventArgs
            {
                ContentId = _content.Id,
                ContentType = _content.Type
            });
        }
    }
}

public class EmbeddedContent
{
    public string Id { get; set; } = "";
    public EmbedType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? CurrentBid { get; set; }
    public decimal? MinBidIncrement { get; set; }
    public DateTime? AuctionEndsAt { get; set; }
    public string? SellerUsername { get; set; }
    public string? SellerAvatarUrl { get; set; }
    public string? SellerRole { get; set; }
    public string ShareLink => $"vea://marketplace/{Type.ToString().ToLower()}/{Id}";
}

public class EmbedReaction
{
    public string Emoji { get; set; } = "";
    public int Count { get; set; }
    public bool HasUserReacted { get; set; }
    public List<string> Users { get; set; } = new();
}

public enum EmbedType
{
    Product,
    Auction,
    Profile,
    Listing,
    Message
}

public class EmbedActionEventArgs : EventArgs
{
    public string ContentId { get; set; } = "";
    public EmbedType ContentType { get; set; }
}

public class EmbedBidEventArgs : EventArgs
{
    public string ContentId { get; set; } = "";
    public decimal CurrentBid { get; set; }
    public decimal MinIncrement { get; set; }
}

public class EmbedReactionEventArgs : EventArgs
{
    public string ContentId { get; set; } = "";
    public string Emoji { get; set; } = "";
    public bool ShouldRemove { get; set; }
}
