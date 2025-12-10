using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class LoginView : UserControl
{
    private readonly LoginViewModel _viewModel;
    private bool _isRegistering;

    public event Action? OnLoginSuccess;

    public LoginView()
    {
        InitializeComponent();

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

        // Focus username on load
        Loaded += (s, e) => UsernameBox.Focus();
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

        ShowLoading(true);

        try
        {
            if (_isRegistering)
            {
                var result = await ((IApiService)App.ServiceProvider.GetService(typeof(IApiService))!)
                    .RegisterAsync(username, email, password);

                if (result.Success)
                {
                    var chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
                    if (result.Token != null)
                        await chatService.ConnectAsync(result.Token);

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
                _viewModel.Username = username;
                _viewModel.Password = password;
                _viewModel.RememberMe = RememberMeCheck.IsChecked ?? false;
                await _viewModel.LoginCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
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

        // Animate the transition
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, ev) =>
        {
            UpdateUI();
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            LoginCard.BeginAnimation(OpacityProperty, fadeIn);
        };
        LoginCard.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateUI()
    {
        if (_isRegistering)
        {
            ActionButton.Content = "Register";
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
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorBorder.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
    }

    private void ShakeCard()
    {
        var storyboard = (Storyboard)FindResource("ShakeAnimation");
        storyboard.Begin(LoginCard);
    }

    private void ShowLoading(bool show)
    {
        LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ActionButton.IsEnabled = !show;
        UsernameBox.IsEnabled = !show;
        PasswordBox.IsEnabled = !show;
        EmailBox.IsEnabled = !show;
        ConfirmPasswordBox.IsEnabled = !show;
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
