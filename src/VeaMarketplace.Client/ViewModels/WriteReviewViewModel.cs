using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class WriteReviewViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly string _productId;
    private readonly string _productTitle;

    [ObservableProperty]
    private int _selectedRating;

    [ObservableProperty]
    private string _reviewTitle = string.Empty;

    [ObservableProperty]
    private string _reviewContent = string.Empty;

    [ObservableProperty]
    private string _productImage = string.Empty;

    [ObservableProperty]
    private List<string> _uploadedImageUrls = new();

    [ObservableProperty]
    private bool _canSubmit;

    public string ProductTitle => _productTitle;

    public WriteReviewViewModel(Services.IApiService apiService, string productId, string productTitle)
    {
        _apiService = apiService;
        _productId = productId;
        _productTitle = productTitle;
    }

    partial void OnSelectedRatingChanged(int value)
    {
        UpdateCanSubmit();
    }

    partial void OnReviewTitleChanged(string value)
    {
        UpdateCanSubmit();
    }

    partial void OnReviewContentChanged(string value)
    {
        UpdateCanSubmit();
    }

    private void UpdateCanSubmit()
    {
        CanSubmit = SelectedRating > 0 &&
                    !string.IsNullOrWhiteSpace(ReviewTitle) &&
                    !string.IsNullOrWhiteSpace(ReviewContent) &&
                    ReviewTitle.Length >= 5 &&
                    ReviewContent.Length >= 20;
    }

    [RelayCommand]
    private async Task UploadImages()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp",
                Title = "Select images for your review"
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                var newUrls = new List<string>(UploadedImageUrls);

                foreach (var file in dialog.FileNames)
                {
                    var url = await _apiService.UploadImageAsync(file);
                    if (!string.IsNullOrEmpty(url))
                        newUrls.Add(url);
                }

                UploadedImageUrls = newUrls;
                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            IsLoading = false;
            ErrorMessage = $"Failed to upload images: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveImage(string imageUrl)
    {
        var newUrls = new List<string>(UploadedImageUrls);
        newUrls.Remove(imageUrl);
        UploadedImageUrls = newUrls;
    }

    [RelayCommand]
    private async Task SubmitReview()
    {
        if (!CanSubmit || IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var request = new CreateReviewRequest
            {
                ProductId = _productId,
                Rating = SelectedRating,
                Title = ReviewTitle,
                Content = ReviewContent,
                ImageUrls = UploadedImageUrls
            };

            var review = await _apiService.CreateReviewAsync(request);

            if (review != null && !string.IsNullOrEmpty(review.Id))
            {
                // Close dialog on success
                await System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow?.OwnedWindows.Count > 0)
                    {
                        var dialog = System.Windows.Application.Current.MainWindow.OwnedWindows[0] as Views.WriteReviewDialog;
                        if (dialog != null)
                        {
                            dialog.DialogResult = true;
                            dialog.Close();
                        }
                    }
                });
            }
            else
            {
                ErrorMessage = "Failed to submit review. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to submit review: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (System.Windows.Application.Current.MainWindow?.OwnedWindows.Count > 0)
            {
                var dialog = System.Windows.Application.Current.MainWindow.OwnedWindows[0] as Views.WriteReviewDialog;
                if (dialog != null)
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                }
            }
        });
    }
}
