using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class MarketplaceView : UserControl
{
    private readonly MarketplaceViewModel? _viewModel;
    private ProductDto? _selectedProduct;

    public MarketplaceView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (MarketplaceViewModel)App.ServiceProvider.GetService(typeof(MarketplaceViewModel))!;
        DataContext = _viewModel;

        ProductsItemsControl.ItemsSource = _viewModel.Products;

        _viewModel.PropertyChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(MarketplaceViewModel.IsLoading))
                    LoadingOverlay.Visibility = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

                if (e.PropertyName == nameof(MarketplaceViewModel.CurrentPage) ||
                    e.PropertyName == nameof(MarketplaceViewModel.TotalPages))
                {
                    PageInfoText.Text = $"Page {_viewModel.CurrentPage} of {_viewModel.TotalPages}";
                    PrevPageButton.IsEnabled = _viewModel.CurrentPage > 1;
                    NextPageButton.IsEnabled = _viewModel.CurrentPage < _viewModel.TotalPages;
                }
            });
        };
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Key == Key.Enter)
        {
            _viewModel.SearchQuery = SearchBox.Text;
            _ = _viewModel.SearchCommand.ExecuteAsync(null);
        }
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var button = (Button)sender;
        var tag = button.Tag?.ToString();

        ProductCategory? category = string.IsNullOrEmpty(tag)
            ? null
            : Enum.Parse<ProductCategory>(tag);

        _ = _viewModel.FilterByCategoryCommand.ExecuteAsync(category);
    }

    private async void ProductCard_Click(object sender, RoutedEventArgs e)
    {
        var card = (FrameworkElement)sender;
        if (card.DataContext is ProductDto product)
        {
            _selectedProduct = product;
            ShowProductDetail(product);
        }
    }

    private void ShowProductDetail(ProductDto product)
    {
        DetailTitleText.Text = product.Title;
        DetailSellerText.Text = $"Sold by {product.SellerUsername}";
        DetailDescriptionText.Text = string.IsNullOrEmpty(product.Description)
            ? "No description provided."
            : product.Description;
        DetailPriceText.Text = $"${product.Price:F2}";

        // Show seller badge
        if (product.SellerRole >= UserRole.VIP)
        {
            DetailSellerBadge.Visibility = Visibility.Visible;
            DetailSellerBadge.Background = new SolidColorBrush(GetRoleColor(product.SellerRole));
            DetailSellerBadgeText.Text = product.SellerRole.ToString().ToUpper();
        }
        else
        {
            DetailSellerBadge.Visibility = Visibility.Collapsed;
        }

        // Show image if available
        if (product.ImageUrls?.Count > 0)
        {
            try
            {
                DetailImage.Source = new BitmapImage(new Uri(product.ImageUrls.First()));
                DetailImageBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                DetailImageBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            DetailImageBorder.Visibility = Visibility.Collapsed;
        }

        // Show tags
        DetailTagsPanel.Children.Clear();
        foreach (var tag in product.Tags ?? [])
        {
            var tagBorder = new Border
            {
                Background = (SolidColorBrush)FindResource("QuaternaryDarkBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 8)
            };
            tagBorder.Child = new TextBlock
            {
                Text = tag,
                Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                FontSize = 12
            };
            DetailTagsPanel.Children.Add(tagBorder);
        }

        ProductDetailModal.Visibility = Visibility.Visible;
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        ProductDetailModal.Visibility = Visibility.Collapsed;
        _selectedProduct = null;
    }

    private async void BuyNow_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null || _viewModel == null) return;

        var result = await _viewModel.PurchaseProductCommand.ExecuteAsync(null);
        ProductDetailModal.Visibility = Visibility.Collapsed;
    }

    private void CreateListingButton_Click(object sender, RoutedEventArgs e)
    {
        ClearCreateForm();
        CreateListingModal.Visibility = Visibility.Visible;
    }

    private void CancelCreateListing_Click(object sender, RoutedEventArgs e)
    {
        CreateListingModal.Visibility = Visibility.Collapsed;
    }

    private async void ConfirmCreateListing_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        CreateErrorBorder.Visibility = Visibility.Collapsed;

        var title = NewTitleBox.Text?.Trim();
        var description = NewDescriptionBox.Text?.Trim();
        var priceText = NewPriceBox.Text?.Trim();
        var tags = NewTagsBox.Text?.Trim();
        var imageUrl = NewImageBox.Text?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            ShowCreateError("Title is required");
            return;
        }

        if (!decimal.TryParse(priceText, out var price) || price <= 0)
        {
            ShowCreateError("Please enter a valid price");
            return;
        }

        var categoryItem = NewCategoryBox.SelectedItem as ComboBoxItem;
        var categoryStr = categoryItem?.Tag?.ToString() ?? "Other";
        var category = Enum.Parse<ProductCategory>(categoryStr);

        _viewModel.NewTitle = title;
        _viewModel.NewDescription = description ?? "";
        _viewModel.NewPrice = priceText;
        _viewModel.NewCategory = category;
        _viewModel.NewTags = tags ?? "";
        _viewModel.NewImageUrl = imageUrl ?? "";

        await _viewModel.CreateListingCommand.ExecuteAsync(null);

        if (!_viewModel.HasError)
        {
            CreateListingModal.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowCreateError(_viewModel.ErrorMessage ?? "Failed to create listing");
        }
    }

    private void ShowCreateError(string message)
    {
        CreateErrorText.Text = message;
        CreateErrorBorder.Visibility = Visibility.Visible;
    }

    private void ClearCreateForm()
    {
        NewTitleBox.Text = "";
        NewDescriptionBox.Text = "";
        NewPriceBox.Text = "";
        NewTagsBox.Text = "";
        NewImageBox.Text = "";
        NewCategoryBox.SelectedIndex = 6; // Other
        CreateErrorBorder.Visibility = Visibility.Collapsed;
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _ = _viewModel.PreviousPageCommand.ExecuteAsync(null);
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _ = _viewModel.NextPageCommand.ExecuteAsync(null);
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
