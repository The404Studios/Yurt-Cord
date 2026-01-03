using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class MarketplaceView : UserControl
{
    private readonly MarketplaceViewModel? _viewModel;
    private readonly IChatService? _chatService;
    private ProductDto? _selectedProduct;

    public MarketplaceView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (MarketplaceViewModel)App.ServiceProvider.GetService(typeof(MarketplaceViewModel))!;
        _chatService = (IChatService?)App.ServiceProvider.GetService(typeof(IChatService));
        DataContext = _viewModel;

        ProductsItemsControl.ItemsSource = _viewModel.Products;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize page info
        if (_viewModel != null)
        {
            PageInfoText.Text = $"Page {_viewModel.CurrentPage} of {_viewModel.TotalPages}";
            PrevPageButton.IsEnabled = _viewModel.CurrentPage > 1;
            NextPageButton.IsEnabled = _viewModel.CurrentPage < _viewModel.TotalPages;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup to prevent memory leaks
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Cleanup toast timer
        if (_toastTimer != null)
        {
            _toastTimer.Stop();
            _toastTimer.Tick -= ToastTimer_Tick;
            _toastTimer = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_viewModel == null) return;

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

    private async void ProductCard_ShareClick(object sender, RoutedEventArgs e)
    {
        var card = (FrameworkElement)sender;
        if (card.DataContext is ProductDto product)
        {
            await ShareProductToChat(product);
        }
    }

    private async Task ShareProductToChat(ProductDto product)
    {
        if (_chatService == null)
        {
            ShowToast("Chat service not available");
            return;
        }

        // Create a product embed message format
        // Format: [PRODUCT_EMBED:id|title|price|seller|imageUrl|description]
        var descPreview = string.IsNullOrEmpty(product.Description)
            ? ""
            : (product.Description.Length > 100 ? product.Description[..100] + "..." : product.Description);
        var embedData = $"[PRODUCT_EMBED:{product.Id}|{product.Title}|{product.Price:F2}|{product.SellerUsername}|{product.ImageUrls?.FirstOrDefault() ?? ""}|{descPreview}]";

        // Also include a user-friendly message
        var message = $"Check out this listing: {product.Title} - ${product.Price:F2}";

        try
        {
            await _chatService.SendMessageAsync(embedData);
            ShowToast($"Shared \"{product.Title}\" to chat!");
        }
        catch
        {
            // Fallback: show share dialog instead
            ShowShareDialog(product);
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

        await _viewModel.PurchaseProductCommand.ExecuteAsync(null);
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

    // Product action buttons
    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _selectedProduct == null) return;
        _viewModel.AddToCartCommand.Execute(_selectedProduct);
        ShowToast("Added to cart!");
    }

    private void ShareProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null) return;
        ShowShareDialog(_selectedProduct);
    }

    private void AddToWishlist_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _selectedProduct == null) return;
        _viewModel.AddToWishlistCommand.Execute(_selectedProduct);
    }

    private void ViewSeller_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _selectedProduct == null) return;
        _viewModel.ViewSellerProfileCommand.Execute(_selectedProduct);
    }

    private void ReportProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _selectedProduct == null) return;
        _viewModel.ReportProductCommand.Execute(_selectedProduct);
    }

    // Share dialog
    private void ShowShareDialog(ProductDto product)
    {
        var content = new Controls.ShareContentDialog.ShareableContent
        {
            Type = Controls.ShareContentDialog.ShareContentType.Product,
            Id = product.Id,
            Title = product.Title,
            Subtitle = $"${product.Price:F2}",
            Description = product.Description,
            ImageUrl = product.ImageUrls?.FirstOrDefault(),
            ShareLink = $"vea://marketplace/product/{product.Id}"
        };

        ShareContentDialogControl.SetContent(content);

        // Load sample friends for demo (in production, fetch from FriendService)
        var sampleFriends = new List<Controls.ShareContentDialog.ShareFriend>
        {
            new() { UserId = "1", Username = "JohnDoe", IsOnline = true, AvatarUrl = "pack://application:,,,/Assets/default-avatar.png" },
            new() { UserId = "2", Username = "JaneSmith", IsOnline = true, AvatarUrl = "pack://application:,,,/Assets/default-avatar.png" },
            new() { UserId = "3", Username = "Player123", IsOnline = false, AvatarUrl = "pack://application:,,,/Assets/default-avatar.png" }
        };
        ShareContentDialogControl.SetFriends(sampleFriends);

        ShareDialog.Visibility = Visibility.Visible;
    }

    private void ShareContentDialog_CloseRequested(object? sender, EventArgs e)
    {
        ShareDialog.Visibility = Visibility.Collapsed;
    }

    private void ShareContentDialog_SharedToFriend(object? sender, Controls.ShareContentDialog.ShareFriend friend)
    {
        var message = ShareContentDialogControl.GetMessage();
        ShowToast($"Shared to {friend.Username}!");
        ShareDialog.Visibility = Visibility.Collapsed;
    }

    private void ShareContentDialog_SharedToGroup(object? sender, EventArgs e)
    {
        ShowToast("Select a group chat to share!");
    }

    private void ShareContentDialog_LinkCopied(object? sender, EventArgs e)
    {
        ShowToast("Link copied to clipboard!");
    }

    private void ShareDialogOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == ShareDialog)
            ShareDialog.Visibility = Visibility.Collapsed;
    }

    // My Listings
    private async void MyListingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        await _viewModel.LoadMyProductsCommand.ExecuteAsync(null);
        MyListingsItemsControl.ItemsSource = _viewModel.MyProducts;
        MyListingsCountText.Text = $"{_viewModel.MyProducts.Count} active listing{(_viewModel.MyProducts.Count == 1 ? "" : "s")}";
        NoListingsPanel.Visibility = _viewModel.MyProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MyListingsPanel.Visibility = Visibility.Visible;
    }

    private void CloseMyListings_Click(object sender, RoutedEventArgs e)
    {
        MyListingsPanel.Visibility = Visibility.Collapsed;
    }

    private void MyListingsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == MyListingsPanel)
            MyListingsPanel.Visibility = Visibility.Collapsed;
    }

    private void EditListing_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.Tag is ProductDto product && _viewModel != null)
        {
            _viewModel.EditProductCommand.Execute(product);
            MyListingsPanel.Visibility = Visibility.Collapsed;

            // Pre-fill the create form for editing
            NewTitleBox.Text = product.Title;
            NewDescriptionBox.Text = product.Description;
            NewPriceBox.Text = product.Price.ToString("F2");
            NewTagsBox.Text = string.Join(", ", product.Tags ?? []);
            NewImageBox.Text = product.ImageUrls?.FirstOrDefault() ?? "";

            // Set category
            for (int i = 0; i < NewCategoryBox.Items.Count; i++)
            {
                if (NewCategoryBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == product.Category.ToString())
                {
                    NewCategoryBox.SelectedIndex = i;
                    break;
                }
            }

            CreateListingModal.Visibility = Visibility.Visible;
        }
    }

    private void ShareListing_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.Tag is ProductDto product)
        {
            ShowShareDialog(product);
        }
    }

    private async void DeleteListing_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.Tag is ProductDto product && _viewModel != null)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete '{product.Title}'?",
                "Delete Listing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ShowDeleteConfirmCommand.Execute(product);
                await _viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

                // Refresh listings
                await _viewModel.LoadMyProductsCommand.ExecuteAsync(null);
                MyListingsItemsControl.ItemsSource = _viewModel.MyProducts;
                MyListingsCountText.Text = $"{_viewModel.MyProducts.Count} active listing{(_viewModel.MyProducts.Count == 1 ? "" : "s")}";
                NoListingsPanel.Visibility = _viewModel.MyProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                ShowToast("Listing deleted successfully");
            }
        }
    }

    // Toast notification
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        // Properly dispose old timer before creating new one to prevent memory leak
        if (_toastTimer != null)
        {
            _toastTimer.Stop();
            _toastTimer.Tick -= ToastTimer_Tick;
        }

        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _toastTimer.Tick += ToastTimer_Tick;
        _toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        ToastNotification.Visibility = Visibility.Collapsed;
    }

    // Quick Actions Toolbar handlers
    private void QuickActions_ScrollTopRequested(object sender, RoutedEventArgs e)
    {
        // Find the parent ScrollViewer
        var scrollViewer = FindParent<ScrollViewer>(ProductsItemsControl);
        scrollViewer?.ScrollToTop();
    }

    private async void QuickActions_RefreshRequested(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        await _viewModel.LoadProductsCommand.ExecuteAsync(null);
        ShowToast("Products refreshed!");
    }

    private void QuickActions_FilterRequested(object sender, RoutedEventArgs e)
    {
        // Toggle filter visibility or show filter dialog
        ShowToast("Filter panel coming soon!");
    }

    private void QuickActions_CreateListingRequested(object sender, RoutedEventArgs e)
    {
        CreateListingButton_Click(sender, e);
    }

    // Rules Panel Toggle
    private void ToggleRulesPanel_Click(object sender, RoutedEventArgs e)
    {
        RulesPanel.Visibility = RulesPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        if (child == null) return null;

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
