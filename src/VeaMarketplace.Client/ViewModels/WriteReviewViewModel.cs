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
    private int _selectedRating = 0;

    [ObservableProperty]
    private string _reviewTitle = string.Empty;

    [ObservableProperty]
    private string _reviewContent = string.Empty;

    [ObservableProperty]
    private string _productImage = string.Empty;

    [ObservableProperty]
    private List<string> _uploadedImageUrls = new();

    [ObservableProperty]
    private bool _canSubmit = false;

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
            // TODO: Open file picker and upload images
            // var dialog = new Microsoft.Win32.OpenFileDialog
            // {
            //     Multiselect = true,
            //     Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            // };

            // if (dialog.ShowDialog() == true)
            // {
            //     foreach (var file in dialog.FileNames)
            //     {
            //         var url = await _apiService.UploadImageAsync(file);
            //         UploadedImageUrls.Add(url);
            //     }
            // }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to upload images: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SubmitReview()
    {
        if (!CanSubmit || IsLoading) return;

        try
        {
            IsLoading = true;

            var request = new CreateReviewRequest
            {
                ProductId = _productId,
                Rating = SelectedRating,
                Title = ReviewTitle,
                Content = ReviewContent,
                ImageUrls = UploadedImageUrls
            };

            // TODO: Call API to submit review
            // await _apiService.CreateReviewAsync(request);

            // Close dialog on success
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to submit review: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
