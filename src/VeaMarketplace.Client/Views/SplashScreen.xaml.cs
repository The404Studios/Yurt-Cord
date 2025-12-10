using System.Windows;
using System.Windows.Media.Animation;

namespace VeaMarketplace.Client.Views;

public partial class SplashScreen : Window
{
    private readonly string[] _loadingMessages = new[]
    {
        "Initializing...",
        "Loading services...",
        "Connecting to server...",
        "Preparing interface...",
        "Almost ready..."
    };

    public SplashScreen()
    {
        InitializeComponent();
        StartLoadingAnimation();
    }

    private async void StartLoadingAnimation()
    {
        var duration = 2500; // Total splash duration in ms
        var steps = _loadingMessages.Length;
        var stepDuration = duration / steps;

        for (int i = 0; i < steps; i++)
        {
            LoadingText.Text = _loadingMessages[i];

            // Animate progress bar
            var targetWidth = ((i + 1) / (double)steps) * 200;
            var animation = new DoubleAnimation(LoadingProgress.Width, targetWidth, TimeSpan.FromMilliseconds(stepDuration - 50));
            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            LoadingProgress.BeginAnimation(WidthProperty, animation);

            await Task.Delay(stepDuration);
        }
    }

    public async Task CompleteAndClose()
    {
        // Final progress animation
        var animation = new DoubleAnimation(LoadingProgress.Width, 200, TimeSpan.FromMilliseconds(200));
        LoadingProgress.BeginAnimation(WidthProperty, animation);

        LoadingText.Text = "Welcome!";
        await Task.Delay(300);

        // Fade out
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (s, e) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
