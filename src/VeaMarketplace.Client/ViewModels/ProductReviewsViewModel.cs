using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ProductReviewsViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly Services.INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<ProductReviewDto> _reviews = new();

    [ObservableProperty]
    private string _productId = string.Empty;

    [ObservableProperty]
    private string _productTitle = string.Empty;

    [ObservableProperty]
    private double _averageRating = 0;

    [ObservableProperty]
    private int _totalReviews = 0;

    [ObservableProperty]
    private ObservableCollection<RatingBreakdownItem> _ratingBreakdown = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private bool _canLoadMore = true;

    public ProductReviewsViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    public async Task InitializeAsync(string productId)
    {
        ProductId = productId;
        await LoadReviewsAsync();
        await LoadRatingSummaryAsync();
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load reviews
            // var reviews = await _apiService.GetProductReviewsAsync(ProductId, CurrentPage, 10);
            // foreach (var review in reviews)
            //     Reviews.Add(review);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load reviews: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRatingSummaryAsync()
    {
        try
        {
            // TODO: Call API to load rating summary
            // var summary = await _apiService.GetProductRatingSummaryAsync(ProductId);
            // AverageRating = summary.AverageRating;
            // TotalReviews = summary.TotalReviews;

            // Calculate breakdown
            RatingBreakdown.Clear();
            RatingBreakdown.Add(new RatingBreakdownItem { Stars = 5, Count = 0, PercentWidth = 200 });
            RatingBreakdown.Add(new RatingBreakdownItem { Stars = 4, Count = 0, PercentWidth = 150 });
            RatingBreakdown.Add(new RatingBreakdownItem { Stars = 3, Count = 0, PercentWidth = 80 });
            RatingBreakdown.Add(new RatingBreakdownItem { Stars = 2, Count = 0, PercentWidth = 40 });
            RatingBreakdown.Add(new RatingBreakdownItem { Stars = 1, Count = 0, PercentWidth = 20 });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load rating summary: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!CanLoadMore || IsLoading) return;
        CurrentPage++;
        await LoadReviewsAsync();
    }

    [RelayCommand]
    private void OpenWriteReview()
    {
        // Open write review dialog
        var dialog = new Views.WriteReviewDialog
        {
            DataContext = new WriteReviewViewModel(_apiService, ProductId, ProductTitle)
        };

        if (dialog.ShowDialog() == true)
        {
            // Reload reviews after successful submission
            _ = LoadReviewsAsync();
        }
    }

    [RelayCommand]
    private async Task MarkHelpful(ProductReviewDto review)
    {
        try
        {
            // TODO: Call API to mark review as helpful
            // await _apiService.MarkReviewHelpfulAsync(review.Id);
            review.HelpfulCount++;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to mark review as helpful: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ReportReview(ProductReviewDto review)
    {
        // Open report dialog
        // TODO: Implement report functionality
    }
}

public class RatingBreakdownItem
{
    public int Stars { get; set; }
    public int Count { get; set; }
    public double PercentWidth { get; set; }
}
