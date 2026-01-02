using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ProfileSetupView : UserControl
{
    private int _currentStep = 1;
    private const int TotalSteps = 3;

    // Profile data
    private string _selectedAvatarUrl = "";
    private string _selectedEmoji = "?";
    private Color _primaryColor = (Color)ColorConverter.ConvertFromString("#00B4D8")!; // Yurt Cord teal
    private Color _secondaryColor = (Color)ColorConverter.ConvertFromString("#48CAE4")!; // Yurt Cord teal light
    private LinearGradientBrush? _selectedAvatarGradient;

    public event Action? OnSetupComplete;
    public event Action? OnSetupSkipped;

    public string AvatarUrl => _selectedAvatarUrl;
    public string DisplayName => DisplayNameBox.Text;
    public string Bio => BioBox.Text;
    public Color PrimaryBannerColor => _primaryColor;
    public Color SecondaryBannerColor => _secondaryColor;

    public ProfileSetupView()
    {
        InitializeComponent();
    }

    public void SetUsername(string username)
    {
        // Set initial based on username
        var initial = string.IsNullOrEmpty(username) ? "?" : username[0].ToString().ToUpper();
        AvatarInitial.Text = initial;
        BannerAvatarInitial.Text = initial;
        PreviewInitial.Text = initial;
        DisplayNameBox.Text = username;
        UpdatePreview();
    }

    private void Avatar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            // Get the gradient and emoji from the button
            var border = FindVisualChild<Border>(button);
            if (border != null)
            {
                _selectedAvatarGradient = border.Background as LinearGradientBrush;
                var textBlock = FindVisualChild<TextBlock>(border);
                if (textBlock != null)
                {
                    _selectedEmoji = textBlock.Text;
                }
            }

            UpdateAvatarPreview();
            _selectedAvatarUrl = ""; // Clear custom URL when selecting preset
            CustomAvatarUrl.Text = "";
        }
    }

    private void UpdateAvatarPreview()
    {
        if (_selectedAvatarGradient != null)
        {
            // Clone the gradient for the preview
            var gradientClone = _selectedAvatarGradient.Clone();
            AvatarPreview.Background = gradientClone;
            AvatarInitial.Text = _selectedEmoji;

            // Update other previews
            UpdateBannerAvatarPreview();
            UpdateFinalPreview();
        }
    }

    private void UpdateBannerAvatarPreview()
    {
        if (_selectedAvatarGradient != null)
        {
            var border = BannerPreview.Child as Border;
            if (border != null)
            {
                border.Background = _selectedAvatarGradient.Clone();
                BannerAvatarInitial.Text = _selectedEmoji;
            }
        }
    }

    private void UpdateFinalPreview()
    {
        if (_selectedAvatarGradient != null)
        {
            PreviewAvatarGradient.GradientStops.Clear();
            foreach (var stop in _selectedAvatarGradient.GradientStops)
            {
                PreviewAvatarGradient.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
            }
            PreviewInitial.Text = _selectedEmoji;
        }

        // Update banner preview gradient
        PreviewBannerGradient.GradientStops.Clear();
        PreviewBannerGradient.GradientStops.Add(new GradientStop(_primaryColor, 0));
        PreviewBannerGradient.GradientStops.Add(new GradientStop(_secondaryColor, 1));
    }

    private void CustomAvatarUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        _selectedAvatarUrl = CustomAvatarUrl.Text;
        // Could add image preview logic here
    }

    private void PrimaryColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Background is SolidColorBrush brush)
        {
            _primaryColor = brush.Color;
            BannerColor1.Color = _primaryColor;
            UpdateFinalPreview();
        }
    }

    private void SecondaryColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Background is SolidColorBrush brush)
        {
            _secondaryColor = brush.Color;
            BannerColor2.Color = _secondaryColor;
            UpdateFinalPreview();
        }
    }

    private void DisplayName_TextChanged(object sender, TextChangedEventArgs e)
    {
        PreviewDisplayName.Text = string.IsNullOrWhiteSpace(DisplayNameBox.Text)
            ? "Display Name"
            : DisplayNameBox.Text;

        // Update initial
        if (!string.IsNullOrEmpty(DisplayNameBox.Text))
        {
            var initial = DisplayNameBox.Text[0].ToString().ToUpper();
            if (string.IsNullOrEmpty(_selectedAvatarUrl) && _selectedEmoji == "?")
            {
                AvatarInitial.Text = initial;
                BannerAvatarInitial.Text = initial;
                PreviewInitial.Text = initial;
            }
        }
    }

    private void Bio_TextChanged(object sender, TextChangedEventArgs e)
    {
        var length = BioBox.Text.Length;
        BioCounter.Text = $"{length}/190";
        PreviewBio.Text = string.IsNullOrWhiteSpace(BioBox.Text)
            ? "Your bio will appear here..."
            : BioBox.Text;
    }

    private void UpdatePreview()
    {
        PreviewDisplayName.Text = string.IsNullOrWhiteSpace(DisplayNameBox.Text)
            ? "Display Name"
            : DisplayNameBox.Text;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepUI();
        }
        else
        {
            // Final step - save profile and complete
            await SaveProfileAsync();
            OnSetupComplete?.Invoke();
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        OnSetupSkipped?.Invoke();
    }

    private async Task SaveProfileAsync()
    {
        try
        {
            var apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));

            if (apiService == null)
            {
                toastService?.ShowError("Error", "Could not connect to profile service");
                return;
            }

            // Build the accent color from primary banner color
            var accentColorHex = $"#{_primaryColor.R:X2}{_primaryColor.G:X2}{_primaryColor.B:X2}";

            // Build banner gradient as a special format string that can be parsed by the UI
            // Format: "gradient:#RRGGBB,#RRGGBB" for gradient banners
            var secondaryHex = $"#{_secondaryColor.R:X2}{_secondaryColor.G:X2}{_secondaryColor.B:X2}";
            var bannerValue = $"gradient:{accentColorHex},{secondaryHex}";

            // Build avatar value - either custom URL or gradient emoji format
            string? avatarValue = null;
            if (!string.IsNullOrWhiteSpace(_selectedAvatarUrl))
            {
                avatarValue = _selectedAvatarUrl;
            }
            else if (_selectedAvatarGradient != null && _selectedEmoji != "?")
            {
                // Store as emoji gradient format: "emoji:😊,#RRGGBB,#RRGGBB"
                var stop1 = _selectedAvatarGradient.GradientStops.FirstOrDefault();
                var stop2 = _selectedAvatarGradient.GradientStops.Skip(1).FirstOrDefault();
                if (stop1 != null && stop2 != null)
                {
                    var c1 = $"#{stop1.Color.R:X2}{stop1.Color.G:X2}{stop1.Color.B:X2}";
                    var c2 = $"#{stop2.Color.R:X2}{stop2.Color.G:X2}{stop2.Color.B:X2}";
                    avatarValue = $"emoji:{_selectedEmoji},{c1},{c2}";
                }
            }

            var request = new UpdateProfileRequest
            {
                DisplayName = string.IsNullOrWhiteSpace(DisplayNameBox.Text) ? null : DisplayNameBox.Text.Trim(),
                Bio = string.IsNullOrWhiteSpace(BioBox.Text) ? null : BioBox.Text.Trim(),
                AvatarUrl = avatarValue,
                BannerUrl = bannerValue,
                AccentColor = accentColorHex
            };

            var result = await apiService.UpdateProfileAsync(request);
            if (result != null)
            {
                toastService?.ShowSuccess("Profile Updated", "Your profile has been set up successfully!");
            }
            else
            {
                toastService?.ShowWarning("Partial Save", "Profile saved, but some settings may not have applied.");
            }
        }
        catch (Exception ex)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowError("Save Failed", $"Could not save profile: {ex.Message}");
        }
    }

    private void UpdateStepUI()
    {
        // Update progress dots - Yurt Cord teal theme
        Step1Dot.Fill = new SolidColorBrush(_currentStep >= 1 ? (Color)ColorConverter.ConvertFromString("#00B4D8")! : (Color)ColorConverter.ConvertFromString("#40444B")!);
        Step2Dot.Fill = new SolidColorBrush(_currentStep >= 2 ? (Color)ColorConverter.ConvertFromString("#00B4D8")! : (Color)ColorConverter.ConvertFromString("#40444B")!);
        Step3Dot.Fill = new SolidColorBrush(_currentStep >= 3 ? (Color)ColorConverter.ConvertFromString("#00B4D8")! : (Color)ColorConverter.ConvertFromString("#40444B")!);

        // Update button visibility
        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = _currentStep == TotalSteps ? "Finish" : "Next";

        // Update title
        StepTitle.Text = _currentStep switch
        {
            1 => "Choose Your Avatar",
            2 => "Customize Your Banner",
            3 => "Tell Us About Yourself",
            _ => "Profile Setup"
        };

        StepSubtitle.Text = _currentStep switch
        {
            1 => "Pick an avatar that represents you",
            2 => "Select colors for your profile banner",
            3 => "Add your display name and bio",
            _ => ""
        };

        // Animate panel transitions
        AnimateStepTransition();
    }

    private void AnimateStepTransition()
    {
        // Hide all panels
        Step1Panel.Visibility = Visibility.Collapsed;
        Step2Panel.Visibility = Visibility.Collapsed;
        Step3Panel.Visibility = Visibility.Collapsed;

        // Show current panel with animation
        var currentPanel = _currentStep switch
        {
            1 => Step1Panel,
            2 => Step2Panel,
            3 => Step3Panel,
            _ => Step1Panel
        };

        currentPanel.Opacity = 0;
        currentPanel.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        currentPanel.BeginAnimation(OpacityProperty, fadeIn);

        if (currentPanel.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        // Update final preview when reaching step 3
        if (_currentStep == 3)
        {
            UpdateFinalPreview();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
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
