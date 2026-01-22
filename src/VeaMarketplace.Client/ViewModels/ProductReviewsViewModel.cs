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
    private double _averageRating;

    [ObservableProperty]
    private int _totalReviews;

    [ObservableProperty]
    private ObservableCollection<RatingBreakdownItem> _ratingBreakdown = new();

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private bool _canLoadMore = true;

    [ObservableProperty]
    private bool _isReportDialogOpen;

    [ObservableProperty]
    private ProductReviewDto? _reviewToReport;

    [ObservableProperty]
    private string _reportReason = string.Empty;

    public ProductReviewsViewModel(Services.IApiService apiService, Services.INavigationService navigationService)
    {
        _apiService = apiService;
        _navigationService = navigationService;
    }

    public async Task InitializeAsync(string productId)
    {
        ProductId = productId;
        CurrentPage = 1;
        Reviews.Clear();
        await LoadReviewsAsync();
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            IsLoading = true;
            var result = await _apiService.GetProductReviewsAsync(ProductId, CurrentPage);

            if (CurrentPage == 1)
            {
                // First page - also get summary info
                AverageRating = result.AverageRating;
                TotalReviews = result.TotalReviews;
                ProductTitle = result.ProductTitle ?? ProductTitle;
                UpdateRatingBreakdown(result);
            }

            foreach (var review in result.Reviews)
                Reviews.Add(review);

            CanLoadMore = result.HasMore;
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

    private void UpdateRatingBreakdown(ProductReviewListDto result)
    {
        RatingBreakdown.Clear();

        if (TotalReviews == 0)
        {
            for (int i = 5; i >= 1; i--)
                RatingBreakdown.Add(new RatingBreakdownItem { Stars = i, Count = 0, PercentWidth = 0 });
            return;
        }

        var maxCount = Math.Max(1, new[] { result.FiveStarCount, result.FourStarCount, result.ThreeStarCount, result.TwoStarCount, result.OneStarCount }.Max());

        RatingBreakdown.Add(new RatingBreakdownItem { Stars = 5, Count = result.FiveStarCount, PercentWidth = (result.FiveStarCount / (double)maxCount) * 200 });
        RatingBreakdown.Add(new RatingBreakdownItem { Stars = 4, Count = result.FourStarCount, PercentWidth = (result.FourStarCount / (double)maxCount) * 200 });
        RatingBreakdown.Add(new RatingBreakdownItem { Stars = 3, Count = result.ThreeStarCount, PercentWidth = (result.ThreeStarCount / (double)maxCount) * 200 });
        RatingBreakdown.Add(new RatingBreakdownItem { Stars = 2, Count = result.TwoStarCount, PercentWidth = (result.TwoStarCount / (double)maxCount) * 200 });
        RatingBreakdown.Add(new RatingBreakdownItem { Stars = 1, Count = result.OneStarCount, PercentWidth = (result.OneStarCount / (double)maxCount) * 200 });
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
        var dialog = new Views.WriteReviewDialog
        {
            DataContext = new WriteReviewViewModel(_apiService, ProductId, ProductTitle)
        };

        if (dialog.ShowDialog() == true)
        {
            // Reload reviews after successful submission
            CurrentPage = 1;
            Reviews.Clear();
            _ = LoadReviewsAsync();
        }
    }

    [RelayCommand]
    private async Task MarkHelpful(ProductReviewDto review)
    {
        try
        {
            if (await _apiService.MarkReviewHelpfulAsync(review.Id))
            {
                review.HelpfulCount++;
                // Force UI update
                var index = Reviews.IndexOf(review);
                if (index >= 0)
                {
                    Reviews[index] = review;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to mark review as helpful: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ReportReview(ProductReviewDto review)
    {
        ReviewToReport = review;
        ReportReason = string.Empty;
        IsReportDialogOpen = true;
    }

    [RelayCommand]
    private async Task SubmitReport()
    {
        if (ReviewToReport == null || string.IsNullOrWhiteSpace(ReportReason)) return;

        try
        {
            if (await _apiService.ReportReviewAsync(ReviewToReport.Id, ReportReason))
            {
                IsReportDialogOpen = false;
                ReviewToReport = null;
                ReportReason = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to report review: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelReport()
    {
        IsReportDialogOpen = false;
        ReviewToReport = null;
        ReportReason = string.Empty;
    }

    [RelayCommand]
    private async Task RefreshReviews()
    {
        CurrentPage = 1;
        Reviews.Clear();
        await LoadReviewsAsync();
    }
}

public class RatingBreakdownItem
{
    public int Stars { get; set; }
    public int Count { get; set; }
    public double PercentWidth { get; set; }
}
