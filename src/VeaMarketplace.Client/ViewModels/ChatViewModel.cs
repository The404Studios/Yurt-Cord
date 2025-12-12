using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ChatViewModel : BaseViewModel
{
    private readonly IChatService _chatService;
    private readonly IVoiceService _voiceService;
    private readonly IApiService _apiService;
    private readonly INavigationService _navigationService;
    private DateTime _lastTypingSent = DateTime.MinValue;
    private bool _eventHandlersRegistered = false;

    [ObservableProperty]
    private ObservableCollection<ChatMessageDto> _messages = [];

    [ObservableProperty]
    private ObservableCollection<OnlineUserDto> _onlineUsers = [];

    [ObservableProperty]
    private ObservableCollection<ChannelDto> _channels = [];

    [ObservableProperty]
    private ObservableCollection<VoiceUserState> _voiceUsers = [];

    [ObservableProperty]
    private string _currentChannel = "general";

    [ObservableProperty]
    private string _messageInput = string.Empty;

    [ObservableProperty]
    private string? _typingUser;

    [ObservableProperty]
    private bool _isInVoiceChannel;

    [ObservableProperty]
    private string? _currentVoiceChannel;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isDeafened;

    public ChatViewModel(IChatService chatService, IVoiceService voiceService, IApiService apiService, INavigationService navigationService)
    {
        _chatService = chatService;
        _voiceService = voiceService;
        _apiService = apiService;
        _navigationService = navigationService;

        RegisterEventHandlers();
    }

    private void RegisterEventHandlers()
    {
        // Prevent duplicate event handler registration
        if (_eventHandlersRegistered) return;
        _eventHandlersRegistered = true;

        _chatService.OnMessageReceived += message =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (message.Channel == CurrentChannel)
                {
                    // Check for duplicates to prevent double messages
                    if (!Messages.Any(m => m.Id == message.Id))
                    {
                        Messages.Add(message);
                    }
                }
            });
        };

        _chatService.OnUserJoined += user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (!OnlineUsers.Any(u => u.Id == user.Id))
                    OnlineUsers.Add(user);
            });
        };

        _chatService.OnUserLeft += user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var existingUser = OnlineUsers.FirstOrDefault(u => u.Id == user.Id);
                if (existingUser != null)
                    OnlineUsers.Remove(existingUser);
            });
        };

        _chatService.OnOnlineUsersReceived += users =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnlineUsers.Clear();
                foreach (var user in users)
                    OnlineUsers.Add(user);
            });
        };

        _chatService.OnChatHistoryReceived += (channel, messages) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (channel == CurrentChannel)
                {
                    Messages.Clear();
                    foreach (var message in messages)
                        Messages.Add(message);
                }
            });
        };

        _chatService.OnChannelListReceived += channels =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Channels.Clear();
                foreach (var channel in channels)
                    Channels.Add(channel);
            });
        };

        _chatService.OnUserTyping += (username, channel) =>
        {
            if (channel == CurrentChannel && username != _apiService.CurrentUser?.Username)
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    TypingUser = username;
                });

                // Clear typing indicator after 3 seconds
                Task.Delay(3000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (TypingUser == username)
                            TypingUser = null;
                    });
                });
            }
        };

        _chatService.OnMessageDeleted += messageId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                    Messages.Remove(message);
            });
        };

        _chatService.OnUserProfileUpdated += updatedUser =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                // Update online user in the list
                var existingUser = OnlineUsers.FirstOrDefault(u => u.Id == updatedUser.Id);
                if (existingUser != null)
                {
                    var index = OnlineUsers.IndexOf(existingUser);
                    OnlineUsers.RemoveAt(index);
                    OnlineUsers.Insert(index, updatedUser);
                }

                // Update sender info in existing messages
                foreach (var message in Messages.Where(m => m.SenderId == updatedUser.Id))
                {
                    message.SenderAvatarUrl = updatedUser.AvatarUrl;
                    message.SenderUsername = updatedUser.Username;
                }
            });
        };

        // Voice events
        _voiceService.OnUserJoinedVoice += user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (!VoiceUsers.Any(u => u.ConnectionId == user.ConnectionId))
                    VoiceUsers.Add(user);
            });
        };

        _voiceService.OnUserLeftVoice += user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var existingUser = VoiceUsers.FirstOrDefault(u => u.ConnectionId == user.ConnectionId);
                if (existingUser != null)
                    VoiceUsers.Remove(existingUser);
            });
        };

        _voiceService.OnVoiceChannelUsers += users =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                VoiceUsers.Clear();
                foreach (var user in users)
                    VoiceUsers.Add(user);
            });
        };

        _voiceService.OnUserSpeaking += (connectionId, username, isSpeaking, audioLevel) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var user = VoiceUsers.FirstOrDefault(u => u.ConnectionId == connectionId);
                if (user != null)
                {
                    user.IsSpeaking = isSpeaking;
                    user.AudioLevel = audioLevel;
                }
            });
        };
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput)) return;

        await _chatService.SendMessageAsync(MessageInput, CurrentChannel);
        MessageInput = string.Empty;
    }

    [RelayCommand]
    private async Task SwitchChannel(string channelName)
    {
        if (channelName == CurrentChannel) return;

        await _chatService.LeaveChannelAsync(CurrentChannel);
        CurrentChannel = channelName;
        Messages.Clear();
        await _chatService.JoinChannelAsync(channelName);
    }

    [RelayCommand]
    private async Task JoinVoiceChannel(string channelId)
    {
        if (IsInVoiceChannel)
        {
            await _voiceService.LeaveVoiceChannelAsync();
        }

        var user = _apiService.CurrentUser;
        if (user != null)
        {
            await _voiceService.ConnectAsync();
            await _voiceService.JoinVoiceChannelAsync(channelId, user.Id, user.Username, user.AvatarUrl);
            IsInVoiceChannel = true;
            CurrentVoiceChannel = channelId;

            // Navigate to voice call dashboard
            _navigationService.NavigateToVoiceCall();
        }
    }

    [RelayCommand]
    private async Task LeaveVoiceChannel()
    {
        await _voiceService.LeaveVoiceChannelAsync();
        IsInVoiceChannel = false;
        CurrentVoiceChannel = null;
        VoiceUsers.Clear();

        // Navigate back to chat
        _navigationService.NavigateToChat();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _voiceService.IsMuted = IsMuted;
    }

    [RelayCommand]
    private void ToggleDeafen()
    {
        IsDeafened = !IsDeafened;
        _voiceService.IsDeafened = IsDeafened;
        if (IsDeafened)
        {
            IsMuted = true;
            _voiceService.IsMuted = true;
        }
    }

    partial void OnMessageInputChanged(string value)
    {
        // Send typing indicator
        if (!string.IsNullOrEmpty(value) && (DateTime.Now - _lastTypingSent).TotalSeconds > 2)
        {
            _lastTypingSent = DateTime.Now;
            _ = _chatService.SendTypingAsync(CurrentChannel);
        }
    }
}
