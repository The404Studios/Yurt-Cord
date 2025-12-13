using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel? _viewModel;
    private bool _isRegistering;
    private Storyboard? _spinnerStoryboard;

    public event Action? OnLoginSuccess;

    public LoginView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (LoginViewModel)App.ServiceProvider.GetService(typeof(LoginViewModel))!;
        DataContext = _viewModel;

        _viewModel.OnLoginSuccess += () => OnLoginSuccess?.Invoke();
        _viewModel.OnLoginFailed += ShowError;

        // Enter key submits
        PasswordBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) ActionButton_Click(s, e);
        };

        UsernameBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) PasswordBox.Focus();
        };

        // Focus username on load with slight delay for animation
        Loaded += async (s, e) =>
        {
            await Task.Delay(400); // Wait for card entry animation
            UsernameBox.Focus();
        };

        // Get spinner storyboard reference
        _spinnerStoryboard = (Storyboard)FindResource("SpinnerAnimation");
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        ClearError();

        var username = UsernameBox.Text;
        var password = PasswordBox.Password;
        var email = EmailBox.Text;
        var confirmPassword = ConfirmPasswordBox.Password;

        // Validation
        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter a username");
            ShakeCard();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter a password");
            ShakeCard();
            return;
        }

        if (_isRegistering)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Please enter an email");
                ShakeCard();
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Passwords do not match");
                ShakeCard();
                return;
            }

            if (password.Length < 6)
            {
                ShowError("Password must be at least 6 characters");
                ShakeCard();
                return;
            }
        }

        ShowLoading(true, _isRegistering ? "Creating account..." : "Logging in...", "Connecting to server...");

        try
        {
            if (_isRegistering)
            {
                UpdateLoadingText("Creating account...", "Registering your profile...");
                await Task.Delay(300); // Brief delay for visual feedback

                var result = await ((IApiService)App.ServiceProvider.GetService(typeof(IApiService))!)
                    .RegisterAsync(username, email, password);

                if (result.Success)
                {
                    UpdateLoadingText("Success!", "Connecting to services...");
                    await Task.Delay(200);

                    var chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
                    if (result.Token != null)
                        await chatService.ConnectAsync(result.Token);

                    UpdateLoadingText("Welcome!", "Preparing your experience...");
                    await Task.Delay(300);

                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    ShowError(result.Message);
                    ShakeCard();
                }
            }
            else
            {
                UpdateLoadingText("Logging in...", "Verifying credentials...");
                await Task.Delay(200);

                if (_viewModel != null)
                {
                    _viewModel.Username = username;
                    _viewModel.Password = password;
                    _viewModel.RememberMe = RememberMeCheck.IsChecked ?? false;

                    UpdateLoadingText("Authenticating...", "Please wait...");
                    await _viewModel.LoginCommand.ExecuteAsync(null);
                }
            }
        }
        catch (Exception)
        {
            ShowError("Connection failed. Is the server running?");
            ShakeCard();
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void ToggleMode_Click(object sender, MouseButtonEventArgs e)
    {
        _isRegistering = !_isRegistering;

        // Animate the transition with scale + fade
        var scaleDown = new DoubleAnimation(1, 0.98, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (s, ev) =>
        {
            UpdateUI();

            var scaleUp = new DoubleAnimation(0.98, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            LoginCard.BeginAnimation(OpacityProperty, fadeIn);
        };

        LoginCard.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateUI()
    {
        if (_isRegistering)
        {
            ActionButton.Content = "Create Account";
            SubtitleText.Text = "Create your account";
            TogglePromptText.Text = "Already have an account?";
            ToggleLinkText.Text = "Login";
            EmailPanel.Visibility = Visibility.Visible;
            ConfirmPasswordPanel.Visibility = Visibility.Visible;
            RememberMeCheck.Visibility = Visibility.Collapsed;
        }
        else
        {
            ActionButton.Content = "Login";
            SubtitleText.Text = "The marketplace with a community";
            TogglePromptText.Text = "Need an account?";
            ToggleLinkText.Text = "Register";
            EmailPanel.Visibility = Visibility.Collapsed;
            ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
            RememberMeCheck.Visibility = Visibility.Visible;
        }

        ClearError();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;

        // Animate error appearance
        ErrorBorder.Opacity = 0;
        ErrorBorder.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ErrorBorder.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void ClearError()
    {
        if (ErrorBorder.Visibility == Visibility.Visible)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
                ErrorText.Text = string.Empty;
            };
            ErrorBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
    }

    private void ShakeCard()
    {
        var storyboard = (Storyboard)FindResource("ShakeAnimation");
        storyboard.Begin(LoginCard);
    }

    private void ShowLoading(bool show, string? text = null, string? subtext = null)
    {
        if (show)
        {
            LoadingText.Text = text ?? "Loading...";
            LoadingSubtext.Text = subtext ?? "Please wait...";

            LoadingOverlay.Opacity = 0;
            LoadingOverlay.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);

            // Start spinner animation
            _spinnerStoryboard?.Begin(this, true);
        }
        else
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _spinnerStoryboard?.Stop(this);
            };
            LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        ActionButton.IsEnabled = !show;
        UsernameBox.IsEnabled = !show;
        PasswordBox.IsEnabled = !show;
        EmailBox.IsEnabled = !show;
        ConfirmPasswordBox.IsEnabled = !show;
    }

    private void UpdateLoadingText(string text, string subtext)
    {
        Dispatcher.Invoke(() =>
        {
            // Quick fade transition for text change
            var fadeOut = new DoubleAnimation(1, 0.5, TimeSpan.FromMilliseconds(80));
            fadeOut.Completed += (s, e) =>
            {
                LoadingText.Text = text;
                LoadingSubtext.Text = subtext;

                var fadeIn = new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(120));
                LoadingText.BeginAnimation(OpacityProperty, fadeIn);
                LoadingSubtext.BeginAnimation(OpacityProperty, fadeIn);
            };
            LoadingText.BeginAnimation(OpacityProperty, fadeOut);
            LoadingSubtext.BeginAnimation(OpacityProperty, fadeOut);
        });
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this).WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
