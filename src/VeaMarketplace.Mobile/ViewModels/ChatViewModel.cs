using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Mobile.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class ChatViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IChatService _chatService;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private ObservableCollection<ChannelDto> _channels = new();

    [ObservableProperty]
    private ObservableCollection<MessageDto> _messages = new();

    [ObservableProperty]
    private ChannelDto? _selectedChannel;

    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private string? _typingUser;

    [ObservableProperty]
    private bool _isConnected;

    public ChatViewModel(
        IApiService apiService,
        IChatService chatService,
        INotificationService notificationService)
    {
        _apiService = apiService;
        _chatService = chatService;
        _notificationService = notificationService;

        _chatService.OnMessageReceived += OnMessageReceived;
        _chatService.OnUserTyping += OnUserTyping;
        _chatService.OnConnectionChanged += OnConnectionChanged;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await _chatService.ConnectAsync();
            await LoadChannelsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadChannelsAsync()
    {
        var channels = await _apiService.GetChannelsAsync();
        Channels.Clear();
        foreach (var channel in channels)
        {
            Channels.Add(channel);
        }

        if (Channels.Count > 0 && SelectedChannel == null)
        {
            await SelectChannelAsync(Channels[0]);
        }
    }

    [RelayCommand]
    private async Task SelectChannelAsync(ChannelDto channel)
    {
        if (channel == null) return;

        SelectedChannel = channel;
        Messages.Clear();

        await _chatService.JoinChannelAsync(channel.Id);

        var messages = await _apiService.GetMessagesAsync(channel.Id);
        foreach (var msg in messages.OrderBy(m => m.Timestamp))
        {
            Messages.Add(msg);
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || SelectedChannel == null)
            return;

        var content = MessageText;
        MessageText = string.Empty;

        var message = await _apiService.SendMessageAsync(SelectedChannel.Id, content);
        if (message != null)
        {
            Messages.Add(message);
        }
        else
        {
            await _notificationService.ShowToastAsync("Failed to send message");
            MessageText = content; // Restore message
        }
    }

    [RelayCommand]
    private async Task RefreshMessagesAsync()
    {
        if (SelectedChannel == null) return;

        IsRefreshing = true;
        try
        {
            var messages = await _apiService.GetMessagesAsync(SelectedChannel.Id);
            Messages.Clear();
            foreach (var msg in messages.OrderBy(m => m.Timestamp))
            {
                Messages.Add(msg);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void OnMessageReceived(MessageDto message)
    {
        if (message.ChannelId == SelectedChannel?.Id)
        {
            Messages.Add(message);
        }
    }

    private void OnUserTyping(string username)
    {
        TypingUser = username;
        // Clear typing indicator after 3 seconds
        Task.Delay(3000).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() => TypingUser = null);
        });
    }

    private void OnConnectionChanged()
    {
        IsConnected = _chatService.IsConnected;
    }
}
