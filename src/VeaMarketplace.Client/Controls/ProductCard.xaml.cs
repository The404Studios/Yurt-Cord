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
        ViewsText.Text = product.ViewCount.ToString();

        // Load image
        if (product.ImageUrls?.Any() == true)
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

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        Click?.Invoke(this, new RoutedEventArgs());
    }

    private Color GetRoleColor(UserRole role)
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
