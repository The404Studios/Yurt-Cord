using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using VeaMarketplace.Client.Helpers;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel? _viewModel;
    private bool _isRegistering;
    private Storyboard? _spinnerStoryboard;
    private bool _isKeyValidated;
    private string? _validatedKey;

    public event Action? OnLoginSuccess;
    public event Action<string>? OnRegistrationSuccess; // Passes username for profile setup

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

        // Validation using centralized helper
        var usernameValidation = ValidationHelper.ValidateUsername(username);
        if (!usernameValidation)
        {
            ShowError(usernameValidation.ErrorMessage!);
            ShakeCard();
            return;
        }

        var passwordValidation = ValidationHelper.ValidatePassword(password);
        if (!passwordValidation)
        {
            ShowError(passwordValidation.ErrorMessage!);
            ShakeCard();
            return;
        }

        if (_isRegistering)
        {
            var emailValidation = ValidationHelper.ValidateEmail(email);
            if (!emailValidation)
            {
                ShowError(emailValidation.ErrorMessage!);
                ShakeCard();
                return;
            }

            var matchValidation = ValidationHelper.ValidatePasswordMatch(password, confirmPassword);
            if (!matchValidation)
            {
                ShowError(matchValidation.ErrorMessage!);
                ShakeCard();
                return;
            }

            // Validate activation key
            if (!_isKeyValidated || string.IsNullOrEmpty(_validatedKey))
            {
                ShowError("Please enter and validate your activation key");
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

                // Get hardware ID for device binding
                var hwidService = (HwidService)App.ServiceProvider.GetService(typeof(HwidService))!;
                var hwid = hwidService.GetHwid();

                var result = await ((IApiService)App.ServiceProvider.GetService(typeof(IApiService))!)
                    .RegisterAsync(username, email, password, _validatedKey!, hwid);

                if (result.Success)
                {
                    UpdateLoadingText("Success!", "Connecting to services...");
                    await Task.Delay(200);

                    var chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
                    if (result.Token != null)
                        await chatService.ConnectAsync(result.Token);

                    UpdateLoadingText("Welcome!", "Let's set up your profile...");
                    await Task.Delay(300);

                    // Trigger profile setup for new registrations
                    OnRegistrationSuccess?.Invoke(username);
                }
                else
                {
                    // Show specific error for HWID already bound
                    var errorMessage = result.HwidMismatch
                        ? "This device is already registered to another account"
                        : result.Message;
                    ShowError(errorMessage);
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
            ActivationKeyPanel.Visibility = Visibility.Visible;
            RememberMeCheck.Visibility = Visibility.Collapsed;

            // Reset key validation state when switching to register
            _isKeyValidated = false;
            _validatedKey = null;
            UpdateKeyValidationUI();
        }
        else
        {
            ActionButton.Content = "Login";
            SubtitleText.Text = "The marketplace with a community";
            TogglePromptText.Text = "Need an account?";
            ToggleLinkText.Text = "Register";
            EmailPanel.Visibility = Visibility.Collapsed;
            ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
            ActivationKeyPanel.Visibility = Visibility.Collapsed;
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
        ActivationKeyBox.IsEnabled = !show;
        ValidateKeyButton.IsEnabled = !show && ActivationKeyBox.Text.Length == 7 && !_isKeyValidated;
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

    /// <summary>
    /// Resets the login view to its initial state (called after logout)
    /// </summary>
    public void Reset()
    {
        // Clear form fields
        UsernameBox.Text = string.Empty;
        PasswordBox.Password = string.Empty;
        EmailBox.Text = string.Empty;
        ConfirmPasswordBox.Password = string.Empty;
        ActivationKeyBox.Text = string.Empty;
        RememberMeCheck.IsChecked = false;

        // Reset key validation state
        _isKeyValidated = false;
        _validatedKey = null;

        // Reset to login mode if in registration mode
        if (_isRegistering)
        {
            _isRegistering = false;
            UpdateUI();
        }

        // Clear any errors
        ClearError();

        // Reset loading state
        ShowLoading(false);

        // Focus username field
        Dispatcher.BeginInvoke(new Action(() => UsernameBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ActivationKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        var text = textBox.Text.ToUpper().Replace("-", "");
        var cursorPosition = textBox.SelectionStart;

        // Remove non-alphanumeric characters
        text = new string(text.Where(c => char.IsLetterOrDigit(c)).ToArray());

        // Limit to 6 characters (XXX-XXX format = 6 chars without dash)
        if (text.Length > 6)
            text = text.Substring(0, 6);

        // Insert dash after first 3 characters
        if (text.Length > 3)
            text = text.Insert(3, "-");

        // Update text if changed
        if (textBox.Text != text)
        {
            textBox.Text = text;
            // Adjust cursor position for dash insertion
            textBox.SelectionStart = Math.Min(cursorPosition + (text.Contains('-') && cursorPosition >= 3 ? 1 : 0), text.Length);
        }

        // Reset validation state when key changes
        if (_isKeyValidated)
        {
            _isKeyValidated = false;
            _validatedKey = null;
            UpdateKeyValidationUI();
        }

        // Enable validate button only when key is complete (XXX-XXX = 7 chars)
        ValidateKeyButton.IsEnabled = text.Length == 7;
    }

    private async void ValidateKey_Click(object sender, RoutedEventArgs e)
    {
        var key = ActivationKeyBox.Text.Trim();
        if (string.IsNullOrEmpty(key) || key.Length != 7)
        {
            ShowError("Please enter a valid activation key (XXX-XXX)");
            ShakeCard();
            return;
        }

        ClearError();
        ValidateKeyButton.IsEnabled = false;
        ValidateKeyButton.Content = "Validating...";

        try
        {
            var apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
            var result = await apiService.ValidateKeyAsync(key);

            if (result.Success)
            {
                _isKeyValidated = true;
                _validatedKey = key;
                UpdateKeyValidationUI();
            }
            else
            {
                ShowError(result.Message ?? "Invalid or already used activation key");
                ShakeCard();
            }
        }
        catch (Exception)
        {
            ShowError("Failed to validate key. Is the server running?");
            ShakeCard();
        }
        finally
        {
            ValidateKeyButton.Content = "Validate";
            ValidateKeyButton.IsEnabled = true;
        }
    }

    private void UpdateKeyValidationUI()
    {
        if (_isKeyValidated)
        {
            KeyStatusIcon.Visibility = Visibility.Visible;
            ActivationKeyBox.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x43, 0xB5, 0x81)); // Green
            ValidateKeyButton.Content = "Valid ✓";
            ValidateKeyButton.IsEnabled = false;
        }
        else
        {
            KeyStatusIcon.Visibility = Visibility.Collapsed;
            ActivationKeyBox.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4A, 0x4A, 0x5A)); // Default gray
            ValidateKeyButton.Content = "Validate";
            ValidateKeyButton.IsEnabled = ActivationKeyBox.Text.Length == 7;
        }
    }
}
