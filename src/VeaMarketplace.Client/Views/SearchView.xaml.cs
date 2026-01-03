using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class SearchView : UserControl
{
    private readonly SearchViewModel? _viewModel;
    private readonly INavigationService? _navigationService;

    public SearchView()
    {
        InitializeComponent();
    }

    public SearchView(SearchViewModel viewModel, INavigationService navigationService) : this()
    {
        _viewModel = viewModel;
        _navigationService = navigationService;
        DataContext = viewModel;

        Loaded += SearchView_Loaded;
        Unloaded += SearchView_Unloaded;
    }

    private void SearchView_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SearchView_Loaded;
        Unloaded -= SearchView_Unloaded;
    }

    private async void SearchView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }

            // Focus the search box
            SearchTextBox.Focus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize search view: {ex.Message}");
        }
    }

    private void ProductCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ProductDto product)
        {
            _navigationService?.NavigateToProduct(product.Id);
        }
    }

    private void SellerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SellerProfileDto seller)
        {
            _navigationService?.NavigateToProfile(seller.Username);
        }
    }

    private void RecentSearch_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is string query)
        {
            _viewModel?.UseRecentSearchCommand.Execute(query);
        }
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string categoryName)
        {
            if (Enum.TryParse<ProductCategory>(categoryName, out var category))
            {
                _viewModel?.SelectCategoryCommand.Execute(category);
            }
        }
    }
}
