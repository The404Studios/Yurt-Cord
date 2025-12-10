using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ChannelSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IApiService? _apiService;
    private readonly IVoiceService? _voiceService;
    private readonly INavigationService? _navigationService;
    private bool _isMuted;
    private bool _isDeafened;

    public ChannelSidebar()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _navigationService = (INavigationService)App.ServiceProvider.GetService(typeof(INavigationService))!;

        ChannelsItemsControl.ItemsSource = _viewModel.Channels;
        VoiceUsersItemsControl.ItemsSource = _viewModel.VoiceUsers;

        // Update user panel when logged in
        Loaded += (s, e) => UpdateUserPanel();

        // Show voice users when in a voice channel
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.IsInVoiceChannel))
            {
                Dispatcher.Invoke(() =>
                {
                    VoiceUsersPanel.Visibility = _viewModel.IsInVoiceChannel
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                });
            }
        };
    }

    private void UpdateUserPanel()
    {
        var user = _apiService?.CurrentUser;
        if (user != null)
        {
            UserNameText.Text = user.Username;
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                try
                {
                    UserAvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl));
                }
                catch { }
            }
        }
    }

    private async void ChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var button = (Button)sender;
        var channelName = button.Tag?.ToString();
        if (!string.IsNullOrEmpty(channelName))
        {
            await _viewModel.SwitchChannelCommand.ExecuteAsync(channelName);
        }
    }

    private async void VoiceChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var button = (Button)sender;
        var channelId = button.Tag?.ToString();
        if (!string.IsNullOrEmpty(channelId))
        {
            await _viewModel.JoinVoiceChannelCommand.ExecuteAsync(channelId);
        }
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;
        MicIcon.Text = _isMuted ? "🔇" : "🎤";
        MicIcon.Opacity = _isMuted ? 0.5 : 1;
    }

    private void DeafenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceService == null) return;
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;
        DeafenIcon.Text = _isDeafened ? "🔈" : "🔊";
        DeafenIcon.Opacity = _isDeafened ? 0.5 : 1;

        if (_isDeafened)
        {
            _isMuted = true;
            _voiceService.IsMuted = true;
            MicIcon.Text = "🔇";
            MicIcon.Opacity = 0.5;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationService?.NavigateToSettings();
    }
}
