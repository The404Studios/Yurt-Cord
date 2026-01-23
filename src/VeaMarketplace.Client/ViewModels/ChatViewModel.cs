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
    private bool _eventHandlersRegistered;

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

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

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

        // Chat service events
        _chatService.OnMessageReceived += OnMessageReceived;
        _chatService.OnUserJoined += OnUserJoined;
        _chatService.OnUserLeft += OnUserLeft;
        _chatService.OnOnlineUsersReceived += OnOnlineUsersReceived;
        _chatService.OnChatHistoryReceived += OnChatHistoryReceived;
        _chatService.OnChannelListReceived += OnChannelListReceived;
        _chatService.OnUserTyping += OnUserTyping;
        _chatService.OnUserStoppedTyping += OnUserStoppedTyping;
        _chatService.OnMessageDeleted += OnMessageDeleted;
        _chatService.OnMessageEdited += OnMessageEdited;
        _chatService.OnReactionAdded += OnReactionAdded;
        _chatService.OnReactionRemoved += OnReactionRemoved;
        _chatService.OnUserProfileUpdated += OnUserProfileUpdated;

        // Voice events
        _voiceService.OnUserJoinedVoice += OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice += OnUserLeftVoice;
        _voiceService.OnVoiceChannelUsers += OnVoiceChannelUsers;
        _voiceService.OnUserSpeaking += OnUserSpeaking;
    }

    #region Event Handlers

    private void OnMessageReceived(ChatMessageDto message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (message.Channel == CurrentChannel)
            {
                if (!Messages.Any(m => m.Id == message.Id))
                {
                    Messages.Add(message);
                }
            }
        });
    }

    private void OnUserJoined(OnlineUserDto user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!OnlineUsers.Any(u => u.Id == user.Id))
                OnlineUsers.Add(user);
        });
    }

    private void OnUserLeft(OnlineUserDto user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existingUser = OnlineUsers.FirstOrDefault(u => u.Id == user.Id);
            if (existingUser != null)
                OnlineUsers.Remove(existingUser);
        });
    }

    private void OnOnlineUsersReceived(List<OnlineUserDto> users)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            OnlineUsers.Clear();
            foreach (var user in users)
                OnlineUsers.Add(user);
        });
    }

    private void OnChatHistoryReceived(string channel, List<ChatMessageDto> messages)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (channel == CurrentChannel)
            {
                Messages.Clear();
                foreach (var message in messages)
                    Messages.Add(message);
            }
        });
    }

    private void OnChannelListReceived(List<ChannelDto> channels)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Channels.Clear();
            foreach (var channel in channels)
                Channels.Add(channel);

            // Ensure "general" channel exists as fallback
            if (!Channels.Any(c => c.Name == "general"))
            {
                Channels.Insert(0, new ChannelDto
                {
                    Name = "general",
                    Icon = "#",
                    Description = "General communications channel"
                });
            }
        });
    }

    private void OnUserTyping(string username, string channel)
    {
        if (channel == CurrentChannel && username != _apiService.CurrentUser?.Username)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                TypingUser = username;
            });

            // Clear typing indicator after 3 seconds
            var capturedUsername = username;
            Task.Delay(3000).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (TypingUser == capturedUsername)
                        TypingUser = null;
                });
            });
        }
    }

    private void OnMessageDeleted(string messageId)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
                Messages.Remove(message);
        });
    }

    private void OnMessageEdited(string messageId, string newContent)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                message.Content = newContent;
                message.IsEdited = true;
            }
        });
    }

    private void OnReactionAdded(string messageId, string userId, string emoji, int count)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                // Check if this user already has this reaction
                var existingReaction = message.Reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);
                if (existingReaction == null)
                {
                    message.Reactions.Add(new MessageReactionDto
                    {
                        MessageId = messageId,
                        UserId = userId,
                        Emoji = emoji,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        });
    }

    private void OnReactionRemoved(string messageId, string userId, string emoji)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                var reaction = message.Reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);
                if (reaction != null)
                {
                    message.Reactions.Remove(reaction);
                }
            }
        });
    }

    private void OnUserStoppedTyping(string username, string channel)
    {
        if (channel == CurrentChannel && TypingUser == username)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                TypingUser = null;
            });
        }
    }

    private void OnUserProfileUpdated(OnlineUserDto updatedUser)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existingUser = OnlineUsers.FirstOrDefault(u => u.Id == updatedUser.Id);
            if (existingUser != null)
            {
                var index = OnlineUsers.IndexOf(existingUser);
                OnlineUsers.RemoveAt(index);
                OnlineUsers.Insert(index, updatedUser);
            }

            foreach (var message in Messages.Where(m => m.SenderId == updatedUser.Id))
            {
                message.SenderAvatarUrl = updatedUser.AvatarUrl;
                message.SenderUsername = updatedUser.Username;
            }
        });
    }

    private void OnUserJoinedVoice(VoiceUserState user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!VoiceUsers.Any(u => u.ConnectionId == user.ConnectionId))
                VoiceUsers.Add(user);
        });
    }

    private void OnUserLeftVoice(VoiceUserState user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existingUser = VoiceUsers.FirstOrDefault(u => u.ConnectionId == user.ConnectionId);
            if (existingUser != null)
                VoiceUsers.Remove(existingUser);
        });
    }

    private void OnVoiceChannelUsers(List<VoiceUserState> users)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            VoiceUsers.Clear();
            foreach (var user in users)
                VoiceUsers.Add(user);
        });
    }

    private void OnUserSpeaking(string connectionId, string username, bool isSpeaking, double audioLevel)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var user = VoiceUsers.FirstOrDefault(u => u.ConnectionId == connectionId);
            if (user != null)
            {
                user.IsSpeaking = isSpeaking;
                user.AudioLevel = audioLevel;
            }
        });
    }

    #endregion

    public void Cleanup()
    {
        if (!_eventHandlersRegistered) return;

        // Unsubscribe from chat service events
        _chatService.OnMessageReceived -= OnMessageReceived;
        _chatService.OnUserJoined -= OnUserJoined;
        _chatService.OnUserLeft -= OnUserLeft;
        _chatService.OnOnlineUsersReceived -= OnOnlineUsersReceived;
        _chatService.OnChatHistoryReceived -= OnChatHistoryReceived;
        _chatService.OnChannelListReceived -= OnChannelListReceived;
        _chatService.OnUserTyping -= OnUserTyping;
        _chatService.OnUserStoppedTyping -= OnUserStoppedTyping;
        _chatService.OnMessageDeleted -= OnMessageDeleted;
        _chatService.OnMessageEdited -= OnMessageEdited;
        _chatService.OnReactionAdded -= OnReactionAdded;
        _chatService.OnReactionRemoved -= OnReactionRemoved;
        _chatService.OnUserProfileUpdated -= OnUserProfileUpdated;

        // Unsubscribe from voice service events
        _voiceService.OnUserJoinedVoice -= OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice -= OnUserLeftVoice;
        _voiceService.OnVoiceChannelUsers -= OnVoiceChannelUsers;
        _voiceService.OnUserSpeaking -= OnUserSpeaking;

        _eventHandlersRegistered = false;
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput)) return;

        try
        {
            var message = MessageInput;
            MessageInput = string.Empty; // Clear immediately for better UX
            await _chatService.SendMessageAsync(message, CurrentChannel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send message: {ex.Message}");
            ErrorMessage = "Failed to send message. Please try again.";
            ClearErrorAfterDelay();
        }
    }

    private void ClearErrorAfterDelay()
    {
        Task.Delay(5000).ContinueWith(_ =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => ErrorMessage = null);
        });
    }

    [RelayCommand]
    private async Task SwitchChannel(string channelName)
    {
        if (channelName == CurrentChannel) return;

        var previousChannel = CurrentChannel;
        try
        {
            IsLoading = true;
            await _chatService.LeaveChannelAsync(CurrentChannel);
            CurrentChannel = channelName;
            Messages.Clear();
            await _chatService.JoinChannelAsync(channelName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to switch channel: {ex.Message}");
            CurrentChannel = previousChannel; // Rollback on failure
            ErrorMessage = "Failed to switch channel. Please try again.";
            ClearErrorAfterDelay();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task JoinVoiceChannel(string channelId)
    {
        try
        {
            IsLoading = true;

            if (IsInVoiceChannel)
            {
                await _voiceService.LeaveVoiceChannelAsync();
            }

            var user = _apiService.CurrentUser;
            if (user != null)
            {
                await _voiceService.ConnectAsync();
                // Use default avatar if AvatarUrl is empty or a special format
                var avatarUrl = GetDisplayableAvatarUrl(user.AvatarUrl);
                await _voiceService.JoinVoiceChannelAsync(channelId, user.Id, user.Username, avatarUrl);
                IsInVoiceChannel = true;
                CurrentVoiceChannel = channelId;

                // Navigate to voice call dashboard
                _navigationService.NavigateToVoiceCall();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to join voice channel: {ex.Message}");
            IsInVoiceChannel = false;
            CurrentVoiceChannel = null;
            ErrorMessage = "Failed to join voice channel. Please try again.";
            ClearErrorAfterDelay();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LeaveVoiceChannel()
    {
        try
        {
            await _voiceService.LeaveVoiceChannelAsync();
            IsInVoiceChannel = false;
            CurrentVoiceChannel = null;
            VoiceUsers.Clear();

            // Navigate back to chat
            _navigationService.NavigateToChat();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to leave voice channel: {ex.Message}");
            // Force cleanup even on error
            IsInVoiceChannel = false;
            CurrentVoiceChannel = null;
            VoiceUsers.Clear();
            _navigationService.NavigateToChat();
        }
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
        if (!string.IsNullOrEmpty(value) && (DateTime.UtcNow - _lastTypingSent).TotalSeconds > 2)
        {
            _lastTypingSent = DateTime.UtcNow;
            _ = _chatService.SendTypingAsync(CurrentChannel);
        }
    }

    /// <summary>
    /// Returns a displayable avatar URL, converting special formats to default avatar.
    /// </summary>
    private static string GetDisplayableAvatarUrl(string? avatarUrl)
    {
        // If empty or special format, use default avatar
        if (string.IsNullOrWhiteSpace(avatarUrl) ||
            avatarUrl.StartsWith("emoji:") ||
            avatarUrl.StartsWith("gradient:"))
        {
            return AppConstants.DefaultAvatarPath;
        }
        return avatarUrl;
    }
}
