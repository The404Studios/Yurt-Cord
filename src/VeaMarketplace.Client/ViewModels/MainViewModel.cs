using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IChatService _chatService;
    private readonly IVoiceService _voiceService;
    private readonly INavigationService _navigationService;

    // Store event handlers for proper unsubscription
    private readonly Action<string> _onNavigate;
    private readonly Action<UserDto> _onAuthenticated;

    [ObservableProperty]
    private UserDto? _currentUser;

    [ObservableProperty]
    private string _currentView = "Chat";

    [ObservableProperty]
    private ChatViewModel? _chatViewModel;

    [ObservableProperty]
    private MarketplaceViewModel? _marketplaceViewModel;

    [ObservableProperty]
    private ProfileViewModel? _profileViewModel;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    public MainViewModel(
        IApiService apiService,
        IChatService chatService,
        IVoiceService voiceService,
        INavigationService navigationService)
    {
        _apiService = apiService;
        _chatService = chatService;
        _voiceService = voiceService;
        _navigationService = navigationService;

        CurrentUser = _apiService.CurrentUser;

        // Create and store event handlers for proper cleanup
        _onNavigate = view =>
        {
            CurrentView = view;
        };

        _onAuthenticated = user =>
        {
            CurrentUser = user;
            IsConnected = true;
            ConnectionStatus = "Connected";
        };

        // Subscribe to events
        _navigationService.OnNavigate += _onNavigate;
        _chatService.OnAuthenticated += _onAuthenticated;

        // Initialize child view models (with null safety)
        ChatViewModel = App.ServiceProvider?.GetService(typeof(ChatViewModel)) as ChatViewModel;
        MarketplaceViewModel = App.ServiceProvider?.GetService(typeof(MarketplaceViewModel)) as MarketplaceViewModel;
        ProfileViewModel = App.ServiceProvider?.GetService(typeof(ProfileViewModel)) as ProfileViewModel;
    }

    /// <summary>
    /// Unsubscribes from all events to prevent memory leaks
    /// </summary>
    public void Cleanup()
    {
        _navigationService.OnNavigate -= _onNavigate;
        _chatService.OnAuthenticated -= _onAuthenticated;

        // Cleanup child view models if they support it
        (ChatViewModel as ChatViewModel)?.Cleanup();
        (MarketplaceViewModel as MarketplaceViewModel)?.Cleanup();
        (ProfileViewModel as ProfileViewModel)?.Cleanup();
    }

    [RelayCommand]
    private void NavigateToChat()
    {
        _navigationService.NavigateToChat();
    }

    [RelayCommand]
    private void NavigateToMarketplace()
    {
        _navigationService.NavigateToMarketplace();
    }

    [RelayCommand]
    private void NavigateToProfile()
    {
        _navigationService.NavigateToProfile();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigationService.NavigateToSettings();
    }

    [RelayCommand]
    private void NavigateToLeaderboard()
    {
        _navigationService.NavigateToLeaderboard();
    }

    [RelayCommand]
    private void NavigateToActivityFeed()
    {
        _navigationService.NavigateToActivityFeed();
    }

    [RelayCommand]
    private void NavigateToFriends()
    {
        _navigationService.NavigateToFriends();
    }

    [RelayCommand]
    private async Task Logout()
    {
        await _chatService.DisconnectAsync();
        await _voiceService.DisconnectAsync();
        _apiService.Logout();
    }
}
