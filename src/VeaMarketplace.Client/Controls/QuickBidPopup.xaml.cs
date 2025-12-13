using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Controls;

public partial class QuickBidPopup : UserControl
{
    private string _itemId = "";
    private string _itemTitle = "";
    private decimal _currentBid;
    private decimal _minIncrement;
    private decimal _bidAmount;
    private DateTime? _endsAt;

    public event EventHandler<BidPlacedEventArgs>? BidPlaced;
    public event EventHandler? CloseRequested;

    public QuickBidPopup()
    {
        InitializeComponent();
    }

    public void SetAuctionInfo(string itemId, string title, decimal currentBid,
        decimal minIncrement = 1, DateTime? endsAt = null)
    {
        _itemId = itemId;
        _itemTitle = title;
        _currentBid = currentBid;
        _minIncrement = minIncrement;
        _endsAt = endsAt;

        // Set minimum bid (current + increment)
        _bidAmount = currentBid + minIncrement;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        ItemTitle.Text = _itemTitle;
        CurrentBidText.Text = $"${_currentBid:F2}";
        BidInput.Text = _bidAmount.ToString("F2");

        if (_endsAt.HasValue)
        {
            var timeLeft = _endsAt.Value - DateTime.Now;
            if (timeLeft.TotalSeconds > 0)
            {
                if (timeLeft.TotalHours >= 1)
                    TimeLeftText.Text = $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
                else if (timeLeft.TotalMinutes >= 1)
                    TimeLeftText.Text = $"{(int)timeLeft.TotalMinutes}m {timeLeft.Seconds}s";
                else
                    TimeLeftText.Text = $"{timeLeft.Seconds}s";

                // Color based on urgency
                if (timeLeft.TotalMinutes < 5)
                    TimeLeftText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(237, 66, 69)); // Red
                else if (timeLeft.TotalMinutes < 30)
                    TimeLeftText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(250, 166, 26)); // Yellow
            }
            else
            {
                TimeLeftText.Text = "Ended";
                TimeLeftText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(114, 118, 125));
                PlaceBidButton.IsEnabled = false;
            }
        }
        else
        {
            TimeLeftText.Text = "No time limit";
        }

        ValidateBid();
    }

    private void ValidateBid()
    {
        var minBid = _currentBid + _minIncrement;

        if (_bidAmount < minBid)
        {
            ShowError($"Minimum bid is ${minBid:F2}");
            PlaceBidButton.IsEnabled = false;
        }
        else
        {
            HideError();
            PlaceBidButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorBorder.Visibility = Visibility.Collapsed;
    }

    private void BidInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(BidInput.Text, out var amount))
        {
            _bidAmount = amount;
            ValidateBid();
        }
        else
        {
            ShowError("Please enter a valid amount");
            PlaceBidButton.IsEnabled = false;
        }
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        var minBid = _currentBid + _minIncrement;
        _bidAmount = Math.Max(minBid, _bidAmount - _minIncrement);
        BidInput.Text = _bidAmount.ToString("F2");
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _bidAmount += _minIncrement;
        BidInput.Text = _bidAmount.ToString("F2");
    }

    private void QuickBidButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && decimal.TryParse(tagStr, out var increment))
        {
            _bidAmount += increment;
            BidInput.Text = _bidAmount.ToString("F2");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        AnimateClose();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        AnimateClose();
    }

    private async void PlaceBidButton_Click(object sender, RoutedEventArgs e)
    {
        if (_bidAmount < _currentBid + _minIncrement)
        {
            ShowError($"Minimum bid is ${_currentBid + _minIncrement:F2}");
            return;
        }

        // Disable button and show loading
        PlaceBidButton.IsEnabled = false;
        PlaceBidButton.Content = "Placing bid...";

        // Simulate API call delay
        await Task.Delay(500);

        BidPlaced?.Invoke(this, new BidPlacedEventArgs
        {
            ItemId = _itemId,
            BidAmount = _bidAmount
        });

        // Show success animation
        await ShowSuccessAnimation();

        AnimateClose();
    }

    private async Task ShowSuccessAnimation()
    {
        // Flash green
        var originalBg = MainBorder.Background;
        MainBorder.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(40, 67, 181, 129));

        await Task.Delay(300);

        MainBorder.Background = originalBg;
    }

    private void AnimateClose()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        var scaleDown = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(150));

        fadeOut.Completed += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);

        MainBorder.BeginAnimation(OpacityProperty, fadeOut);
        MainScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleDown);
        MainScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleDown);
    }
}

public class BidPlacedEventArgs : EventArgs
{
    public string ItemId { get; set; } = "";
    public decimal BidAmount { get; set; }
}
