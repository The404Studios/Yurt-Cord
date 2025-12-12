using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class FriendsViewModel : BaseViewModel
{
    private readonly IFriendService _friendService;
    private readonly IApiService _apiService;
    private readonly IVoiceService _voiceService;
    private readonly INavigationService _navigationService;
    private readonly DispatcherTimer _callTimer;
    private DateTime _callStartTime;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _friendRequestUsername = string.Empty;

    [ObservableProperty]
    private string _dmMessageInput = string.Empty;

    // Username search state
    [ObservableProperty]
    private UserSearchResultDto? _searchedUser;

    [ObservableProperty]
    private bool _isSearchingUser;

    [ObservableProperty]
    private bool _userSearchCompleted;

    [ObservableProperty]
    private string _userSearchMessage = string.Empty;

    private DispatcherTimer? _usernameSearchDebounce;

    [ObservableProperty]
    private FriendDto? _selectedFriend;

    [ObservableProperty]
    private ConversationDto? _selectedConversation;

    [ObservableProperty]
    private bool _isInDMView;

    [ObservableProperty]
    private string _currentView = "Friends"; // Friends, Pending, Add, Blocked, Conversations

    // Call state
    [ObservableProperty]
    private bool _hasIncomingCall;

    [ObservableProperty]
    private bool _isInCall;

    [ObservableProperty]
    private VoiceCallDto? _currentCall;

    [ObservableProperty]
    private string _callDuration = "00:00";

    [ObservableProperty]
    private bool _isCallUserSpeaking;

    [ObservableProperty]
    private double _callUserAudioLevel;

    // Typing indicator
    [ObservableProperty]
    private bool _isPartnerTyping;

    [ObservableProperty]
    private string? _typingUsername;

    // Block dialog
    [ObservableProperty]
    private bool _isBlockDialogOpen;

    [ObservableProperty]
    private FriendDto? _userToBlock;

    [ObservableProperty]
    private string _blockReason = string.Empty;

    // Context menu
    [ObservableProperty]
    private FriendDto? _contextMenuFriend;

    public ObservableCollection<FriendDto> Friends => _friendService.Friends;
    public ObservableCollection<FriendRequestDto> PendingRequests => _friendService.PendingRequests;
    public ObservableCollection<FriendRequestDto> OutgoingRequests => _friendService.OutgoingRequests;
    public ObservableCollection<ConversationDto> Conversations => _friendService.Conversations;
    public ObservableCollection<DirectMessageDto> CurrentDMHistory => _friendService.CurrentDMHistory;
    public ObservableCollection<BlockedUserDto> BlockedUsers => _friendService.BlockedUsers;

    public int OnlineFriendsCount => Friends.Count(f => f.IsOnline);
    public int TotalFriendsCount => Friends.Count;
    public int PendingRequestsCount => PendingRequests.Count;
    public int UnreadConversationsCount => Conversations.Count(c => c.UnreadCount > 0);

    public FriendsViewModel(IFriendService friendService, IApiService apiService, IVoiceService voiceService, INavigationService navigationService)
    {
        _friendService = friendService;
        _apiService = apiService;
        _voiceService = voiceService;
        _navigationService = navigationService;

        // Set up call timer
        _callTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _callTimer.Tick += CallTimer_Tick;

        // Subscribe to events
        _friendService.OnNewFriendRequest += request =>
        {
            // Could show notification
        };

        _friendService.OnDirectMessageReceived += message =>
        {
            // Notification for new DM
        };

        _friendService.OnError += error =>
        {
            SetError(error);
        };

        _friendService.OnSuccess += message =>
        {
            // Could show toast notification
            ClearError();
        };

        _friendService.OnUserSearchResult += result =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsSearchingUser = false;
                UserSearchCompleted = true;
                SearchedUser = result;

                if (result != null)
                {
                    if (result.IsFriend)
                    {
                        UserSearchMessage = "You're already friends!";
                    }
                    else
                    {
                        UserSearchMessage = string.Empty;
                    }
                }
                else
                {
                    UserSearchMessage = "No user found with that username";
                }

                OnPropertyChanged(nameof(CanSendFriendRequest));
            });
        };

        _friendService.OnUserTypingDM += (userId, username) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (SelectedFriend?.UserId == userId)
                {
                    IsPartnerTyping = true;
                    TypingUsername = username;
                }
            });
        };

        _friendService.OnUserStoppedTypingDM += userId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (SelectedFriend?.UserId == userId)
                {
                    IsPartnerTyping = false;
                    TypingUsername = null;
                }
            });
        };

        _friendService.OnFriendRemoved += friend =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(TotalFriendsCount));
                OnPropertyChanged(nameof(OnlineFriendsCount));
            });
        };

        _friendService.OnUserBlocked += user =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(TotalFriendsCount));
                OnPropertyChanged(nameof(OnlineFriendsCount));
            });
        };

        _friendService.OnFriendOnline += friend =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(OnlineFriendsCount));
            });
        };

        _friendService.OnFriendOffline += userId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(OnlineFriendsCount));
            });
        };

        _friendService.OnConversationsUpdated += () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(UnreadConversationsCount));
            });
        };

        // Subscribe to voice service call events
        _voiceService.OnIncomingCall += call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentCall = call;
                HasIncomingCall = true;
                // Find the friend for the caller
                SelectedFriend = Friends.FirstOrDefault(f => f.UserId == call.CallerId);
            });
        };

        _voiceService.OnCallAnswered += call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentCall = call;
                HasIncomingCall = false;
                IsInCall = true;
                _callStartTime = call.AnsweredAt ?? DateTime.UtcNow;
                _callTimer.Start();
            });
        };

        _voiceService.OnCallDeclined += call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                HasIncomingCall = false;
                IsInCall = false;
                CurrentCall = null;
            });
        };

        _voiceService.OnCallEnded += (callId, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsInCall = false;
                HasIncomingCall = false;
                CurrentCall = null;
                _callTimer.Stop();
                CallDuration = "00:00";
            });
        };

        _voiceService.OnCallFailed += error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsInCall = false;
                HasIncomingCall = false;
                CurrentCall = null;
                SetError(error);
            });
        };

        _voiceService.OnCallUserSpeaking += (connectionId, isSpeaking, audioLevel) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsCallUserSpeaking = isSpeaking;
                CallUserAudioLevel = audioLevel;
            });
        };
    }

    private void CallTimer_Tick(object? sender, EventArgs e)
    {
        var duration = DateTime.UtcNow - _callStartTime;
        CallDuration = duration.ToString(@"mm\:ss");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (_apiService.AuthToken == null) return;

        try
        {
            await _friendService.ConnectAsync(_apiService.AuthToken);
        }
        catch (Exception ex)
        {
            SetError("Failed to connect: " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task SendFriendRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(FriendRequestUsername) || SearchedUser == null) return;

        ClearError();
        try
        {
            // Send by user ID for reliability
            await _friendService.SendFriendRequestByIdAsync(SearchedUser.UserId);
            FriendRequestUsername = string.Empty;
            SearchedUser = null;
            UserSearchCompleted = false;
            UserSearchMessage = string.Empty;
            OnPropertyChanged(nameof(CanSendFriendRequest));
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AcceptFriendRequestAsync(FriendRequestDto request)
    {
        await _friendService.RespondToFriendRequestAsync(request.Id, true);
    }

    [RelayCommand]
    private async Task DeclineFriendRequestAsync(FriendRequestDto request)
    {
        await _friendService.RespondToFriendRequestAsync(request.Id, false);
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(FriendDto friend)
    {
        await _friendService.RemoveFriendAsync(friend.UserId);
    }

    [RelayCommand]
    private async Task OpenDMAsync(FriendDto friend)
    {
        SelectedFriend = friend;
        IsInDMView = true;
        await _friendService.GetDMHistoryAsync(friend.UserId);
    }

    [RelayCommand]
    private async Task OpenConversationAsync(ConversationDto conversation)
    {
        SelectedConversation = conversation;
        SelectedFriend = Friends.FirstOrDefault(f => f.UserId == conversation.UserId);
        IsInDMView = true;
        await _friendService.GetDMHistoryAsync(conversation.UserId);
    }

    [RelayCommand]
    private void CloseDM()
    {
        IsInDMView = false;
        SelectedFriend = null;
        SelectedConversation = null;
    }

    [RelayCommand]
    private async Task SendDMAsync()
    {
        if (string.IsNullOrWhiteSpace(DmMessageInput) || SelectedFriend == null) return;

        await _friendService.SendDirectMessageAsync(SelectedFriend.UserId, DmMessageInput);
        DmMessageInput = string.Empty;
    }

    [RelayCommand]
    private void SwitchView(string view)
    {
        CurrentView = view;
    }

    // Call commands
    [RelayCommand]
    private async Task StartCallAsync(FriendDto friend)
    {
        if (friend == null) return;

        try
        {
            // Ensure voice service is connected and authenticated
            if (!_voiceService.IsConnected)
            {
                await _voiceService.ConnectAsync();
                if (_apiService.AuthToken != null)
                {
                    await _voiceService.AuthenticateForCallsAsync(_apiService.AuthToken);
                }
            }

            SelectedFriend = friend;
            await _voiceService.StartCallAsync(friend.UserId);
        }
        catch (Exception ex)
        {
            SetError($"Failed to start call: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AcceptCallAsync()
    {
        if (CurrentCall == null) return;

        try
        {
            await _voiceService.AnswerCallAsync(CurrentCall.Id, true);
        }
        catch (Exception ex)
        {
            SetError($"Failed to accept call: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeclineCallAsync()
    {
        if (CurrentCall == null) return;

        try
        {
            await _voiceService.AnswerCallAsync(CurrentCall.Id, false);
            HasIncomingCall = false;
            CurrentCall = null;
        }
        catch (Exception ex)
        {
            SetError($"Failed to decline call: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EndCallAsync()
    {
        if (CurrentCall == null && _voiceService.CurrentCallId == null) return;

        try
        {
            var callId = CurrentCall?.Id ?? _voiceService.CurrentCallId!;
            await _voiceService.EndCallAsync(callId);
            IsInCall = false;
            _callTimer.Stop();
            CallDuration = "00:00";
            CurrentCall = null;
        }
        catch (Exception ex)
        {
            SetError($"Failed to end call: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _voiceService.IsMuted = !_voiceService.IsMuted;
        OnPropertyChanged(nameof(IsMuted));
    }

    public bool IsMuted => _voiceService.IsMuted;

    // Cancel friend request
    [RelayCommand]
    private async Task CancelFriendRequestAsync(FriendRequestDto request)
    {
        await _friendService.CancelFriendRequestAsync(request.Id);
    }

    // Block functionality
    [RelayCommand]
    private void OpenBlockDialog(FriendDto friend)
    {
        UserToBlock = friend;
        BlockReason = string.Empty;
        IsBlockDialogOpen = true;
    }

    [RelayCommand]
    private void CloseBlockDialog()
    {
        IsBlockDialogOpen = false;
        UserToBlock = null;
        BlockReason = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmBlockAsync()
    {
        if (UserToBlock == null) return;

        try
        {
            await _friendService.BlockUserAsync(UserToBlock.UserId, string.IsNullOrWhiteSpace(BlockReason) ? null : BlockReason);
            IsBlockDialogOpen = false;
            UserToBlock = null;
            BlockReason = string.Empty;

            // Close DM if blocking current chat partner
            if (SelectedFriend?.UserId == UserToBlock?.UserId)
            {
                CloseDM();
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to block user: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UnblockUserAsync(BlockedUserDto user)
    {
        try
        {
            await _friendService.UnblockUserAsync(user.UserId);
        }
        catch (Exception ex)
        {
            SetError($"Failed to unblock user: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadBlockedUsersAsync()
    {
        await _friendService.GetBlockedUsersAsync();
    }

    // Refresh conversations
    [RelayCommand]
    private async Task RefreshConversationsAsync()
    {
        await _friendService.RefreshConversationsAsync();
    }

    // View friend profile
    [RelayCommand]
    private void ViewFriendProfile(FriendDto friend)
    {
        if (friend == null) return;
        _navigationService.NavigateToProfile(friend.UserId);
    }

    // View searched user profile
    [RelayCommand]
    private void ViewSearchedUserProfile()
    {
        if (SearchedUser == null) return;
        _navigationService.NavigateToProfile(SearchedUser.UserId);
    }

    // Send typing indicator
    [RelayCommand]
    private async Task SendTypingAsync()
    {
        if (SelectedFriend != null)
        {
            await _friendService.SendTypingDMAsync(SelectedFriend.UserId);
        }
    }

    // Send a nudge to a friend
    [RelayCommand]
    private async Task SendNudgeAsync(FriendDto? friend)
    {
        if (friend == null) return;

        try
        {
            await _voiceService.SendNudgeAsync(friend.UserId);
        }
        catch (Exception ex)
        {
            SetError($"Failed to send nudge: {ex.Message}");
        }
    }

    // Mark messages as read when opening a conversation
    partial void OnSelectedFriendChanged(FriendDto? value)
    {
        if (value != null)
        {
            _ = _friendService.MarkMessagesReadAsync(value.UserId);
            IsPartnerTyping = false;
            TypingUsername = null;
        }
    }

    partial void OnCurrentViewChanged(string value)
    {
        if (value == "Blocked")
        {
            _ = LoadBlockedUsersAsync();
        }
        else if (value == "Conversations")
        {
            _ = RefreshConversationsAsync();
        }
        else if (value == "Add")
        {
            // Reset search state when switching to Add view
            SearchedUser = null;
            UserSearchCompleted = false;
            UserSearchMessage = string.Empty;
            FriendRequestUsername = string.Empty;
        }
    }

    partial void OnFriendRequestUsernameChanged(string value)
    {
        // Reset search state on new input
        SearchedUser = null;
        UserSearchCompleted = false;
        UserSearchMessage = string.Empty;

        // Debounce the search
        _usernameSearchDebounce?.Stop();

        if (string.IsNullOrWhiteSpace(value))
        {
            IsSearchingUser = false;
            return;
        }

        IsSearchingUser = true;
        _usernameSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _usernameSearchDebounce.Tick += async (s, e) =>
        {
            _usernameSearchDebounce.Stop();
            await _friendService.SearchUserAsync(value);
        };
        _usernameSearchDebounce.Start();
    }

    public bool CanSendFriendRequest => SearchedUser != null && !SearchedUser.IsFriend && UserSearchCompleted;
}
