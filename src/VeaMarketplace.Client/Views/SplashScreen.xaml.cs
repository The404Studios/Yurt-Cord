using System.Windows;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Views;

public partial class SplashScreen : Window
{
    private readonly LoadingStep[] _loadingSteps = new[]
    {
        new LoadingStep("Initializing", "Starting core services..."),
        new LoadingStep("Loading services", "Configuring dependency injection..."),
        new LoadingStep("Connecting", "Establishing secure connection..."),
        new LoadingStep("Authenticating", "Verifying credentials..."),
        new LoadingStep("Loading data", "Syncing marketplace data..."),
        new LoadingStep("Preparing UI", "Rendering interface components..."),
        new LoadingStep("Almost ready", "Final optimizations...")
    };

    private record LoadingStep(string Message, string Detail);

    public SplashScreen()
    {
        InitializeComponent();
        StartLoadingAnimation();
    }

    private async void StartLoadingAnimation()
    {
        try
        {
            var duration = 3000; // Total splash duration in ms
            var steps = _loadingSteps.Length;
            var stepDuration = duration / steps;
            var progressBarWidth = 420.0; // Match XAML width

            for (int i = 0; i < steps; i++)
            {
                var step = _loadingSteps[i];

                // Update loading text with fade effect
                await AnimateTextChange(step.Message + "...", step.Detail);

                // Animate progress bar with easing
                var targetWidth = ((i + 1) / (double)steps) * progressBarWidth;
                var currentWidth = LoadingProgress.Width;
                if (double.IsNaN(currentWidth)) currentWidth = 0;

                var animation = new DoubleAnimation(currentWidth, targetWidth, TimeSpan.FromMilliseconds(stepDuration - 100))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                LoadingProgress.BeginAnimation(WidthProperty, animation);

                await Task.Delay(stepDuration);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Splash screen animation error: {ex.Message}");
            // Don't crash - splash screen animation is non-critical
        }
    }

    private async Task AnimateTextChange(string loadingText, string statusText)
    {
        // Quick fade out
        var fadeOut = new DoubleAnimation(1, 0.5, TimeSpan.FromMilliseconds(80));
        LoadingText.BeginAnimation(OpacityProperty, fadeOut);
        StatusDetail.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(80);

        // Update text
        LoadingText.Text = loadingText;
        StatusDetail.Text = statusText;

        // Fade in
        var fadeIn = new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        LoadingText.BeginAnimation(OpacityProperty, fadeIn);
        StatusDetail.BeginAnimation(OpacityProperty, fadeIn);
    }

    public async Task CompleteAndClose()
    {
        // Show completion message
        await AnimateTextChange("Welcome!", "Loading complete");

        // Fill progress bar completely with bounce effect
        var bounceAnimation = new DoubleAnimation(LoadingProgress.Width, 420, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };
        LoadingProgress.BeginAnimation(WidthProperty, bounceAnimation);

        await Task.Delay(400);

        // Scale up slightly before fade
        var scaleUp = new DoubleAnimation(1, 1.02, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Elegant fade out
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (s, e) => Close();

        BeginAnimation(OpacityProperty, fadeOut);
    }

    public void UpdateStatus(string message, string detail)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingText.Text = message;
            StatusDetail.Text = detail;
        });
    }

    public void SetProgress(double percentage)
    {
        Dispatcher.Invoke(() =>
        {
            var targetWidth = (percentage / 100.0) * 420;
            var animation = new DoubleAnimation(LoadingProgress.Width, targetWidth, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            LoadingProgress.BeginAnimation(WidthProperty, animation);
        });
    }
}
