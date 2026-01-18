using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Client.Views;

public partial class ProductReportDialog : Window
{
    private readonly ProductDto _product;
    private readonly IApiService _apiService;
    private readonly IToastNotificationService _toastService;

    public bool ReportSubmitted { get; private set; }

    public ProductReportDialog(ProductDto product)
    {
        InitializeComponent();
        _product = product;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;

        SetupUI();
        SetupEventHandlers();
    }

    private void SetupUI()
    {
        // Set product info
        ProductTitle.Text = _product.Title;
        ProductSeller.Text = $"by {_product.SellerUsername}";

        // Load product image
        if (!string.IsNullOrEmpty(_product.ImageUrl))
        {
            try
            {
                ProductImage.Source = new BitmapImage(new Uri(_product.ImageUrl));
            }
            catch
            {
                // Leave image empty on error
            }
        }
    }

    private void SetupEventHandlers()
    {
        // Track character count
        DetailsTextBox.TextChanged += (s, e) =>
        {
            CharCount.Text = $"{DetailsTextBox.Text.Length}/500";
        };

        // Enable submit when a reason is selected
        ReasonScam.Checked += OnReasonSelected;
        ReasonCounterfeit.Checked += OnReasonSelected;
        ReasonProhibited.Checked += OnReasonSelected;
        ReasonMisleading.Checked += OnReasonSelected;
        ReasonInappropriate.Checked += OnReasonSelected;
        ReasonIntellectual.Checked += OnReasonSelected;
        ReasonOther.Checked += OnReasonSelected;

        // Allow dragging the window
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void OnReasonSelected(object sender, RoutedEventArgs e)
    {
        SubmitButton.IsEnabled = true;
    }

    private ProductReportReason GetSelectedReason()
    {
        if (ReasonScam.IsChecked == true) return ProductReportReason.Scam;
        if (ReasonCounterfeit.IsChecked == true) return ProductReportReason.Counterfeit;
        if (ReasonProhibited.IsChecked == true) return ProductReportReason.ProhibitedItem;
        if (ReasonMisleading.IsChecked == true) return ProductReportReason.MisleadingDescription;
        if (ReasonInappropriate.IsChecked == true) return ProductReportReason.InappropriateContent;
        if (ReasonIntellectual.IsChecked == true) return ProductReportReason.IntellectualProperty;
        return ProductReportReason.Other;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SubmitButton.IsEnabled = false;
            SubmitButton.Content = "Submitting...";

            var reason = GetSelectedReason();
            var details = DetailsTextBox.Text.Trim();

            var result = await _apiService.ReportProductAsync(_product.Id, reason, details);

            if (result != null)
            {
                ReportSubmitted = true;
                _toastService.ShowSuccess("Report Submitted",
                    "Thank you for your report. Our team will review it shortly.");
                DialogResult = true;
                Close();
            }
            else
            {
                _toastService.ShowError("Report Failed",
                    "Could not submit report. Please try again later.");
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "Submit Report";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error submitting report: {ex.Message}");
            _toastService.ShowError("Report Failed",
                "An error occurred while submitting your report.");
            SubmitButton.IsEnabled = true;
            SubmitButton.Content = "Submit Report";
        }
    }
}
