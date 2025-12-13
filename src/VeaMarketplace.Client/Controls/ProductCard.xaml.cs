using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Controls;

public partial class ProductCard : UserControl
{
    public event RoutedEventHandler? Click;
    public event RoutedEventHandler? AddToCartClick;
    public event RoutedEventHandler? AddToWishlistClick;
    public event RoutedEventHandler? QuickViewClick;

    private bool _isInWishlist;

    public ProductCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ProductDto product)
        {
            UpdateUI(product);
        }
    }

    private void UpdateUI(ProductDto product)
    {
        TitleText.Text = product.Title;
        SellerText.Text = product.SellerUsername;
        CategoryText.Text = product.Category.ToString();
        PriceText.Text = $"${product.Price:F2}";
        ViewsText.Text = FormatViewCount(product.ViewCount);

        // Rating display
        RatingText.Text = product.AverageRating.ToString("F1");
        ReviewCountText.Text = $"({product.ReviewCount})";

        // Load image
        if (product.ImageUrls?.Count > 0)
        {
            try
            {
                ProductImage.Source = new BitmapImage(new Uri(product.ImageUrls.First()));
                NoImageText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                NoImageText.Visibility = Visibility.Visible;
            }
        }
        else
        {
            NoImageText.Visibility = Visibility.Visible;
        }

        // Featured badge
        FeaturedBadge.Visibility = product.IsFeatured ? Visibility.Visible : Visibility.Collapsed;

        // New badge (products created in last 7 days)
        var isNew = (DateTime.UtcNow - product.CreatedAt).TotalDays <= 7;
        NewBadge.Visibility = isNew && !product.IsFeatured ? Visibility.Visible : Visibility.Collapsed;

        // Sale badge and original price
        if (product.OriginalPrice > product.Price)
        {
            var discount = (int)((1 - product.Price / product.OriginalPrice) * 100);
            SaleBadge.Visibility = Visibility.Visible;
            SaleBadgeText.Text = $"-{discount}%";
            OriginalPriceText.Text = $"${product.OriginalPrice:F2}";
            OriginalPriceText.Visibility = Visibility.Visible;
        }
        else
        {
            SaleBadge.Visibility = Visibility.Collapsed;
            OriginalPriceText.Visibility = Visibility.Collapsed;
        }

        // Out of stock
        OutOfStockOverlay.Visibility = product.Stock <= 0 ? Visibility.Visible : Visibility.Collapsed;

        // Seller badge
        if (product.SellerRole >= UserRole.VIP)
        {
            SellerBadge.Visibility = Visibility.Visible;
            SellerBadge.Background = new SolidColorBrush(GetRoleColor(product.SellerRole));
            SellerBadgeText.Text = product.SellerRole.ToString().ToUpper();
        }
        else
        {
            SellerBadge.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatViewCount(int count)
    {
        return count switch
        {
            >= 1000000 => $"{count / 1000000.0:F1}M",
            >= 1000 => $"{count / 1000.0:F1}K",
            _ => count.ToString()
        };
    }

    public void SetWishlistState(bool isInWishlist)
    {
        _isInWishlist = isInWishlist;
        WishlistIcon.Text = isInWishlist ? "\u2665" : "\u2661"; // Filled vs empty heart
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        Click?.Invoke(this, new RoutedEventArgs());
    }

    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent card click
        AddToCartClick?.Invoke(this, e);
    }

    private void AddToWishlist_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent card click
        _isInWishlist = !_isInWishlist;
        WishlistIcon.Text = _isInWishlist ? "\u2665" : "\u2661";
        AddToWishlistClick?.Invoke(this, e);
    }

    private void QuickView_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent card click
        QuickViewClick?.Invoke(this, e);
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
}
