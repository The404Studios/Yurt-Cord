using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Controls;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class FriendsViewModel : BaseViewModel
{
    private readonly IFriendService _friendService;
    private readonly IApiService _apiService;
    private readonly IVoiceService _voiceService;
    private readonly INavigationService _navigationService;
    private readonly ISocialService? _socialService;
    private readonly DispatcherTimer _callTimer;
    private DateTime _callStartTime;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _friendFilter = "All"; // All, Online, Offline, GroupName

    [ObservableProperty]
    private string _friendSort = "Name"; // Name, Status, RecentActivity

    [ObservableProperty]
    private bool _sortAscending = true;

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
    public bool HasPendingRequests => PendingRequests.Count > 0;
    public bool HasNoFriends => Friends.Count == 0;

    // Current user info for the user panel
    public string? CurrentUsername => _apiService.CurrentUser?.Username;
    public string? CurrentUserAvatar => _apiService.CurrentUser?.AvatarUrl;

    // Filtered and sorted friends list
    public IEnumerable<FriendDto> FilteredFriends
    {
        get
        {
            var filtered = Friends.AsEnumerable();

            // Apply search query filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLowerInvariant();
                filtered = filtered.Where(f =>
                    (f.Username?.ToLowerInvariant().Contains(query) == true) ||
                    (f.DisplayName?.ToLowerInvariant().Contains(query) == true) ||
                    GetFriendNickname(f.UserId)?.ToLowerInvariant().Contains(query) == true ||
                    GetFriendTags(f.UserId).Any(t => t.ToLowerInvariant().Contains(query)));
            }

            // Apply status filter
            filtered = FriendFilter switch
            {
                "Online" => filtered.Where(f => f.IsOnline),
                "Offline" => filtered.Where(f => !f.IsOnline),
                "All" => filtered,
                _ => FilterByGroup(filtered, FriendFilter) // Group name
            };

            // Apply sort
            filtered = (FriendSort, SortAscending) switch
            {
                ("Name", true) => filtered.OrderBy(f => f.DisplayName ?? f.Username),
                ("Name", false) => filtered.OrderByDescending(f => f.DisplayName ?? f.Username),
                ("Status", true) => filtered.OrderByDescending(f => f.IsOnline).ThenBy(f => f.Username),
                ("Status", false) => filtered.OrderBy(f => f.IsOnline).ThenBy(f => f.Username),
                ("RecentActivity", true) => filtered.OrderByDescending(f => GetFriendLastActivity(f.UserId)),
                ("RecentActivity", false) => filtered.OrderBy(f => GetFriendLastActivity(f.UserId)),
                _ => filtered.OrderBy(f => f.Username)
            };

            return filtered;
        }
    }

    // Filter options available
    public IEnumerable<string> FilterOptions
    {
        get
        {
            var options = new List<string> { "All", "Online", "Offline" };
            if (FriendGroups != null)
            {
                options.AddRange(FriendGroups.Select(g => g.Name));
            }
            return options;
        }
    }

    public IEnumerable<string> SortOptions => new[] { "Name", "Status", "RecentActivity" };

    private IEnumerable<FriendDto> FilterByGroup(IEnumerable<FriendDto> friends, string groupName)
    {
        if (_socialService == null) return friends;

        var group = FriendGroups?.FirstOrDefault(g => g.Name == groupName);
        if (group == null) return friends;

        return friends.Where(f => group.MemberIds.Contains(f.UserId));
    }

    private string? GetFriendNickname(string userId)
    {
        var qolService = App.ServiceProvider?.GetService(typeof(IQoLService)) as IQoLService;
        return qolService?.GetFriendNote(userId)?.Nickname;
    }

    private List<string> GetFriendTags(string userId)
    {
        var qolService = App.ServiceProvider?.GetService(typeof(IQoLService)) as IQoLService;
        return qolService?.GetFriendNote(userId)?.Tags ?? new List<string>();
    }

    private DateTime GetFriendLastActivity(string userId)
    {
        var history = _socialService?.RecentInteractions?
            .Where(i => i.FriendId == userId)
            .OrderByDescending(i => i.Timestamp)
            .FirstOrDefault();
        return history?.Timestamp ?? DateTime.MinValue;
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredFriends));
    }

    partial void OnFriendFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredFriends));
    }

    partial void OnFriendSortChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredFriends));
    }

    partial void OnSortAscendingChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredFriends));
    }

    // Friend Groups (from SocialService)
    public ObservableCollection<FriendGroup>? FriendGroups => _socialService?.FriendGroups;
    public bool HasFriendGroups => FriendGroups?.Count > 0;

    /// <summary>
    /// Populates the Members property of each FriendGroup from the Friends collection
    /// </summary>
    private void PopulateFriendGroupMembers()
    {
        if (FriendGroups == null) return;

        foreach (var group in FriendGroups)
        {
            group.Members = Friends
                .Where(f => group.MemberIds.Contains(f.UserId))
                .ToList();
        }
    }

    private void OnFriendsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        PopulateFriendGroupMembers();
    }

    private void OnFriendGroupsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        PopulateFriendGroupMembers();
    }

    // Activity tracking
    public ObservableCollection<InteractionEvent>? RecentInteractions => _socialService?.RecentInteractions;
    public ObservableCollection<FriendRecommendation>? FriendRecommendations => _socialService?.Recommendations;
    public bool HasRecommendations => FriendRecommendations?.Count > 0;

    // Friendship stats
    [ObservableProperty]
    private FriendshipStats? _friendshipStats;

    // Top friends by interaction
    [ObservableProperty]
    private List<FriendInteractionSummary>? _topFriendsByInteraction;

    // Pinned messages
    public ObservableCollection<PinnedMessage>? PinnedMessages => _socialService?.PinnedMessages;
    public bool HasPinnedMessages => PinnedMessages?.Count > 0;
    public int PinnedMessagesCount => PinnedMessages?.Count ?? 0;

    public FriendsViewModel(IFriendService friendService, IApiService apiService, IVoiceService voiceService, INavigationService navigationService)
    {
        _friendService = friendService;
        _apiService = apiService;
        _voiceService = voiceService;
        _navigationService = navigationService;

        // Try to get optional social service (handles groups, activity, reactions)
        _socialService = App.ServiceProvider?.GetService(typeof(ISocialService)) as ISocialService;

        // Set up call timer
        _callTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _callTimer.Tick += CallTimer_Tick;

        // Subscribe to events using named handlers for proper cleanup
        _friendService.OnNewFriendRequest += OnNewFriendRequest;
        _friendService.OnDirectMessageReceived += OnDirectMessageReceived;
        _friendService.OnError += OnFriendServiceError;
        _friendService.OnSuccess += OnFriendServiceSuccess;
        _friendService.OnUserSearchResult += OnUserSearchResult;
        _friendService.OnUserTypingDM += OnUserTypingDM;
        _friendService.OnUserStoppedTypingDM += OnUserStoppedTypingDM;
        _friendService.OnFriendRemoved += OnFriendRemoved;
        _friendService.OnUserBlocked += OnUserBlocked;
        _friendService.OnFriendOnline += OnFriendOnline;
        _friendService.OnFriendOffline += OnFriendOffline;
        _friendService.OnConversationsUpdated += OnConversationsUpdated;
        _friendService.OnFriendProfileUpdated += OnFriendProfileUpdated;

        // Populate friend group members when friends or groups change
        Friends.CollectionChanged += OnFriendsCollectionChanged;
        if (_socialService?.FriendGroups != null)
        {
            _socialService.FriendGroups.CollectionChanged += OnFriendGroupsCollectionChanged;
        }

        // Initial population
        PopulateFriendGroupMembers();

        // Subscribe to voice service call events
        _voiceService.OnIncomingCall += OnIncomingCall;
        _voiceService.OnCallAnswered += OnCallAnswered;
        _voiceService.OnCallDeclined += OnCallDeclined;
        _voiceService.OnCallEnded += OnCallEnded;
        _voiceService.OnCallFailed += OnCallFailed;
        _voiceService.OnCallUserSpeaking += OnCallUserSpeaking;
        _voiceService.OnLocalAudioLevel += OnLocalAudioLevel;
    }

    #region Event Handlers

    private void OnNewFriendRequest(FriendRequestDto request)
    {
        var toastService = (IToastNotificationService?)App.ServiceProvider?.GetService(typeof(IToastNotificationService));
        toastService?.ShowFriendRequest(request.RequesterUsername);
    }

    private void OnDirectMessageReceived(DirectMessageDto message)
    {
        // Only notify if not currently viewing this conversation
        if (SelectedFriend?.UserId != message.SenderId)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider?.GetService(typeof(IToastNotificationService));
            var content = message.Content ?? string.Empty;
            var preview = content.Length > 50 ? content[..47] + "..." : content;
            toastService?.ShowMessage(message.SenderUsername, preview);
        }
    }

    private void OnFriendServiceError(string error)
    {
        SetError(error);
    }

    private void OnFriendServiceSuccess(string message)
    {
        ClearError();
    }

    private void OnUserSearchResult(UserSearchResultDto? result)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsSearchingUser = false;
            UserSearchCompleted = true;
            SearchedUser = result;

            if (result != null)
            {
                UserSearchMessage = result.IsFriend ? "You're already friends!" : string.Empty;
            }
            else
            {
                UserSearchMessage = "No user found with that username";
            }

            OnPropertyChanged(nameof(CanSendFriendRequest));
        });
    }

    private void OnUserTypingDM(string userId, string username)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (SelectedFriend?.UserId == userId)
            {
                IsPartnerTyping = true;
                TypingUsername = username;
            }
        });
    }

    private void OnUserStoppedTypingDM(string userId)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (SelectedFriend?.UserId == userId)
            {
                IsPartnerTyping = false;
                TypingUsername = null;
            }
        });
    }

    private void OnFriendRemoved(FriendDto friend)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(TotalFriendsCount));
            OnPropertyChanged(nameof(OnlineFriendsCount));
        });
    }

    private void OnUserBlocked(BlockedUserDto user)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(TotalFriendsCount));
            OnPropertyChanged(nameof(OnlineFriendsCount));
        });
    }

    private void OnFriendOnline(FriendDto friend)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(OnlineFriendsCount));
        });
    }

    private void OnFriendOffline(string userId)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(OnlineFriendsCount));
        });
    }

    private void OnConversationsUpdated()
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(UnreadConversationsCount));
        });
    }

    private void OnFriendProfileUpdated(FriendDto friend)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            // The Friends collection is already updated by the service
            // We just need to refresh the UI if the selected friend was updated
            if (SelectedFriend?.UserId == friend.UserId)
            {
                // Force UI refresh for the selected friend's profile
                OnPropertyChanged(nameof(SelectedFriend));
            }

            // Refresh filtered friends list to show updated avatar/banner/status
            OnPropertyChanged(nameof(FilteredFriends));
        });
    }

    private void OnIncomingCall(VoiceCallDto call)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CurrentCall = call;
            HasIncomingCall = true;
            SelectedFriend = Friends.FirstOrDefault(f => f.UserId == call.CallerId);
        });
    }

    private void OnCallAnswered(VoiceCallDto call)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CurrentCall = call;
            HasIncomingCall = false;
            IsInCall = true;
            _callStartTime = call.AnsweredAt ?? DateTime.UtcNow;
            _callTimer.Start();
        });
    }

    private void OnCallDeclined(VoiceCallDto call)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            HasIncomingCall = false;
            IsInCall = false;
            CurrentCall = null;
        });
    }

    private void OnCallEnded(string callId, string reason)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsInCall = false;
            HasIncomingCall = false;
            CurrentCall = null;
            _callTimer.Stop();
            CallDuration = "00:00";
        });
    }

    private void OnCallFailed(string error)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsInCall = false;
            HasIncomingCall = false;
            CurrentCall = null;
            SetError(error);
        });
    }

    private void OnCallUserSpeaking(string connectionId, bool isSpeaking, double audioLevel)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsCallUserSpeaking = isSpeaking;
            CallUserAudioLevel = audioLevel;
        });
    }

    private void OnLocalAudioLevel(double level)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            AudioLevelWidth = level * 80;
        });
    }

    #endregion

    public void Cleanup()
    {
        // Cleanup timers
        _callTimer.Stop();
        _callTimer.Tick -= CallTimer_Tick;

        if (_usernameSearchDebounce != null)
        {
            _usernameSearchDebounce.Stop();
            _usernameSearchDebounce.Tick -= OnUsernameSearchDebounceTick;
            _usernameSearchDebounce = null;
        }

        // Unsubscribe from friend service events
        _friendService.OnNewFriendRequest -= OnNewFriendRequest;
        _friendService.OnDirectMessageReceived -= OnDirectMessageReceived;
        _friendService.OnError -= OnFriendServiceError;
        _friendService.OnSuccess -= OnFriendServiceSuccess;
        _friendService.OnUserSearchResult -= OnUserSearchResult;
        _friendService.OnUserTypingDM -= OnUserTypingDM;
        _friendService.OnUserStoppedTypingDM -= OnUserStoppedTypingDM;
        _friendService.OnFriendRemoved -= OnFriendRemoved;
        _friendService.OnUserBlocked -= OnUserBlocked;
        _friendService.OnFriendOnline -= OnFriendOnline;
        _friendService.OnFriendOffline -= OnFriendOffline;
        _friendService.OnConversationsUpdated -= OnConversationsUpdated;
        _friendService.OnFriendProfileUpdated -= OnFriendProfileUpdated;

        // Unsubscribe from collection changed events
        Friends.CollectionChanged -= OnFriendsCollectionChanged;
        if (_socialService?.FriendGroups != null)
        {
            _socialService.FriendGroups.CollectionChanged -= OnFriendGroupsCollectionChanged;
        }

        // Unsubscribe from voice service events
        _voiceService.OnIncomingCall -= OnIncomingCall;
        _voiceService.OnCallAnswered -= OnCallAnswered;
        _voiceService.OnCallDeclined -= OnCallDeclined;
        _voiceService.OnCallEnded -= OnCallEnded;
        _voiceService.OnCallFailed -= OnCallFailed;
        _voiceService.OnCallUserSpeaking -= OnCallUserSpeaking;
        _voiceService.OnLocalAudioLevel -= OnLocalAudioLevel;
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
        try
        {
            await _friendService.RespondToFriendRequestAsync(request.Id, true);
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowSuccess("Friend Added", $"You are now friends with {request.RequesterUsername}!");
        }
        catch (Exception ex)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowError("Request Failed", $"Could not accept request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeclineFriendRequestAsync(FriendRequestDto request)
    {
        try
        {
            await _friendService.RespondToFriendRequestAsync(request.Id, false);
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowInfo("Request Declined", $"Friend request from {request.RequesterUsername} declined");
        }
        catch (Exception ex)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowError("Request Failed", $"Could not decline request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(FriendDto friend)
    {
        try
        {
            await _friendService.RemoveFriendAsync(friend.UserId);
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowInfo("Friend Removed", $"Removed {friend.GetDisplayName()} from friends");
        }
        catch (Exception ex)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowError("Remove Failed", $"Could not remove friend: {ex.Message}");
        }
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

    // Video call properties
    [ObservableProperty]
    private bool _isVideoEnabled;

    [ObservableProperty]
    private bool _isScreenSharing;

    [ObservableProperty]
    private bool _isSelfPreviewEnabled = true;

    [ObservableProperty]
    private double _audioLevelWidth;

    public string? SelfAvatarUrl => _apiService.CurrentUser?.AvatarUrl;

    [RelayCommand]
    private async Task ToggleVideoAsync()
    {
        IsVideoEnabled = !IsVideoEnabled;

        if (IsVideoEnabled)
        {
            await _voiceService.StartVideoAsync();
        }
        else
        {
            await _voiceService.StopVideoAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleScreenShareAsync()
    {
        // Screen sharing is only supported in voice channels, not DM calls
        if (!_voiceService.IsInVoiceChannel)
        {
            return; // Screen share not available in DM calls
        }

        if (IsScreenSharing)
        {
            await _voiceService.StopScreenShareAsync();
            IsScreenSharing = false;
        }
        else
        {
            await _voiceService.StartScreenShareAsync();
            IsScreenSharing = true;
        }
    }

    [RelayCommand]
    private void ToggleSelfPreview()
    {
        IsSelfPreviewEnabled = !IsSelfPreviewEnabled;
    }

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

    // Create new DM - opens friend selector
    [RelayCommand]
    private void CreateDM()
    {
        // Switch to friends view to select someone to message
        CurrentView = "Friends";
        IsInDMView = false;
    }

    // Start video call with friend
    [RelayCommand]
    private async Task StartVideoCallAsync(FriendDto? friend)
    {
        if (friend == null) return;

        try
        {
            // Ensure voice service is connected
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
            // Enable video after call starts
            IsVideoEnabled = true;
            await _voiceService.StartVideoAsync();
        }
        catch (Exception ex)
        {
            SetError($"Failed to start video call: {ex.Message}");
        }
    }

    // Add friends to existing DM (convert to group)
    [RelayCommand]
    private void AddToDM()
    {
        if (SelectedFriend == null) return;

        // Show friend selector to add to group DM
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Coming Soon", "Group DMs will be available in a future update");
    }

    // Start group call
    [RelayCommand]
    private async Task StartGroupCallAsync()
    {
        try
        {
            if (!_voiceService.IsConnected)
            {
                await _voiceService.ConnectAsync();
                if (_apiService.AuthToken != null)
                {
                    await _voiceService.AuthenticateForCallsAsync(_apiService.AuthToken);
                }
            }

            // Create a new group call with no initial invites
            var callName = $"{CurrentUsername}'s Call";
            await _voiceService.StartGroupCallAsync(callName, new List<string>());

            // Navigate to group call view
            _navigationService.NavigateToGroupCall();
        }
        catch (Exception ex)
        {
            SetError($"Failed to start group call: {ex.Message}");
        }
    }

    // Create group DM
    [RelayCommand]
    private void CreateGroupDM()
    {
        // Show friend selector for group DM
        var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
        toastService?.ShowInfo("Coming Soon", "Group DMs will be available in a future update");
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

    partial void OnFriendRequestUsernameChanged(string value)
    {
        // Reset search state on new input
        SearchedUser = null;
        UserSearchCompleted = false;
        UserSearchMessage = string.Empty;

        // Debounce the search - cleanup old timer if exists
        if (_usernameSearchDebounce != null)
        {
            _usernameSearchDebounce.Stop();
            _usernameSearchDebounce.Tick -= OnUsernameSearchDebounceTick;
            _usernameSearchDebounce = null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            IsSearchingUser = false;
            return;
        }

        IsSearchingUser = true;
        _usernameSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _usernameSearchDebounce.Tick += OnUsernameSearchDebounceTick;
        _usernameSearchDebounce.Start();
    }

    private async void OnUsernameSearchDebounceTick(object? sender, EventArgs e)
    {
        _usernameSearchDebounce?.Stop();
        await _friendService.SearchUserAsync(FriendRequestUsername);
    }

    public bool CanSendFriendRequest => SearchedUser != null && !SearchedUser.IsFriend && UserSearchCompleted;

    #region Friend Notes Commands (QoL Feature)

    [RelayCommand]
    private void EditFriendNote(FriendDto friend)
    {
        if (friend == null) return;

        // Get QoL service
        var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
        if (qolService == null) return;

        var existingNote = qolService.GetFriendNote(friend.UserId);

        // Show dialog with current note
        var dialog = new System.Windows.Window
        {
            Title = $"Note for {friend.Username}",
            Width = 400,
            Height = 250,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Private note (only visible to you):",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 0, 0, 8)
        };

        var noteBox = new System.Windows.Controls.TextBox
        {
            Text = existingNote?.Note ?? "",
            AcceptsReturn = true,
            Height = 100,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12)
        };

        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "Save Note",
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 12, 0, 0),
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 101, 242)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        saveBtn.Click += (s, e) =>
        {
            qolService.SetFriendNote(new FriendNote
            {
                UserId = friend.UserId,
                Note = noteBox.Text,
                Nickname = existingNote?.Nickname,
                Tags = existingNote?.Tags ?? [],
                Birthday = existingNote?.Birthday,
                Timezone = existingNote?.Timezone
            });
            dialog.Close();
        };

        panel.Children.Add(label);
        panel.Children.Add(noteBox);
        panel.Children.Add(saveBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void SetFriendBirthday(FriendDto friend)
    {
        if (friend == null) return;

        var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
        if (qolService == null) return;

        var existingNote = qolService.GetFriendNote(friend.UserId);

        var dialog = new System.Windows.Window
        {
            Title = $"Set Birthday for {friend.Username}",
            Width = 350,
            Height = 180,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Birthday:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 0, 0, 8)
        };

        var datePicker = new System.Windows.Controls.DatePicker
        {
            SelectedDate = existingNote?.Birthday ?? DateTime.Today.AddYears(-20),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37))
        };

        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "Save Birthday",
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 12, 0, 0),
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 101, 242)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        saveBtn.Click += (s, e) =>
        {
            qolService.SetFriendNote(new FriendNote
            {
                UserId = friend.UserId,
                Note = existingNote?.Note ?? "",
                Nickname = existingNote?.Nickname,
                Tags = existingNote?.Tags ?? [],
                Birthday = datePicker.SelectedDate,
                Timezone = existingNote?.Timezone
            });
            dialog.Close();
        };

        panel.Children.Add(label);
        panel.Children.Add(datePicker);
        panel.Children.Add(saveBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void AddFriendTag(FriendDto friend)
    {
        if (friend == null) return;

        var qolService = App.ServiceProvider.GetService(typeof(IQoLService)) as IQoLService;
        if (qolService == null) return;

        var existingNote = qolService.GetFriendNote(friend.UserId);
        var currentTags = existingNote?.Tags ?? [];

        var dialog = new System.Windows.Window
        {
            Title = $"Tags for {friend.Username}",
            Width = 350,
            Height = 220,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Current tags: " + (currentTags.Count > 0 ? string.Join(", ", currentTags) : "None"),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 0, 0, 12),
            TextWrapping = System.Windows.TextWrapping.Wrap
        };

        var newTagLabel = new System.Windows.Controls.TextBlock
        {
            Text = "Add new tag:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 0, 0, 4)
        };

        var tagBox = new System.Windows.Controls.TextBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8)
        };

        var addBtn = new System.Windows.Controls.Button
        {
            Content = "Add Tag",
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 12, 0, 0),
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 101, 242)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        addBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(tagBox.Text))
            {
                var newTags = currentTags.ToList();
                if (!newTags.Contains(tagBox.Text.Trim()))
                {
                    newTags.Add(tagBox.Text.Trim());
                }

                qolService.SetFriendNote(new FriendNote
                {
                    UserId = friend.UserId,
                    Note = existingNote?.Note ?? "",
                    Nickname = existingNote?.Nickname,
                    Tags = newTags,
                    Birthday = existingNote?.Birthday,
                    Timezone = existingNote?.Timezone
                });
                dialog.Close();
            }
        };

        panel.Children.Add(label);
        panel.Children.Add(newTagLabel);
        panel.Children.Add(tagBox);
        panel.Children.Add(addBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    #endregion

    #region Friend Groups Commands

    [RelayCommand]
    private async Task CreateFriendGroupAsync()
    {
        if (_socialService == null) return;

        var dialog = new System.Windows.Window
        {
            Title = "Create Friend Group",
            Width = 400,
            Height = 300,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };

        var nameLabel = new System.Windows.Controls.TextBlock
        {
            Text = "Group Name:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 0, 0, 4)
        };

        var nameBox = new System.Windows.Controls.TextBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8)
        };

        var emojiLabel = new System.Windows.Controls.TextBlock
        {
            Text = "Emoji (optional):",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 12, 0, 4)
        };

        var emojiBox = new System.Windows.Controls.TextBox
        {
            Text = "⭐",
            MaxLength = 4,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8)
        };

        var colorLabel = new System.Windows.Controls.TextBlock
        {
            Text = "Color:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            Margin = new System.Windows.Thickness(0, 12, 0, 4)
        };

        var colorBox = new System.Windows.Controls.TextBox
        {
            Text = "#5865F2",
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8)
        };

        var createBtn = new System.Windows.Controls.Button
        {
            Content = "Create Group",
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 16, 0, 0),
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 101, 242)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        createBtn.Click += async (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(nameBox.Text))
            {
                await _socialService.CreateFriendGroupAsync(nameBox.Text, emojiBox.Text, colorBox.Text);
                OnPropertyChanged(nameof(FriendGroups));
                OnPropertyChanged(nameof(HasFriendGroups));
                dialog.Close();
            }
        };

        panel.Children.Add(nameLabel);
        panel.Children.Add(nameBox);
        panel.Children.Add(emojiLabel);
        panel.Children.Add(emojiBox);
        panel.Children.Add(colorLabel);
        panel.Children.Add(colorBox);
        panel.Children.Add(createBtn);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ToggleGroupExpanded(FriendGroup group)
    {
        if (group == null) return;
        group.IsCollapsed = !group.IsCollapsed;
        OnPropertyChanged(nameof(FriendGroups));
    }

    [RelayCommand]
    private void EditFriendGroup(FriendGroup group)
    {
        if (group == null || _socialService == null) return;

        var dialog = new System.Windows.Window
        {
            Title = $"Edit {group.Name}",
            Width = 400,
            Height = 350,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };

        var nameBox = new System.Windows.Controls.TextBox
        {
            Text = group.Name,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8)
        };

        var emojiBox = new System.Windows.Controls.TextBox
        {
            Text = group.Emoji ?? "",
            MaxLength = 4,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8),
            Margin = new System.Windows.Thickness(0, 12, 0, 0)
        };

        var colorBox = new System.Windows.Controls.TextBox
        {
            Text = group.Color,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 37)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(12, 8, 12, 8),
            Margin = new System.Windows.Thickness(0, 12, 0, 0)
        };

        var buttonsPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 16, 0, 0)
        };

        var deleteBtn = new System.Windows.Controls.Button
        {
            Content = "Delete",
            Margin = new System.Windows.Thickness(0, 0, 8, 0),
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 66, 69)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        deleteBtn.Click += async (s, e) =>
        {
            await _socialService.DeleteFriendGroupAsync(group.Id);
            OnPropertyChanged(nameof(FriendGroups));
            OnPropertyChanged(nameof(HasFriendGroups));
            dialog.Close();
        };

        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "Save",
            Padding = new System.Windows.Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 101, 242)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new System.Windows.Thickness(0)
        };

        saveBtn.Click += async (s, e) =>
        {
            group.Name = nameBox.Text;
            group.Emoji = emojiBox.Text;
            group.Color = colorBox.Text;
            await _socialService.UpdateFriendGroupAsync(group);
            OnPropertyChanged(nameof(FriendGroups));
            dialog.Close();
        };

        buttonsPanel.Children.Add(deleteBtn);
        buttonsPanel.Children.Add(saveBtn);

        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Group Name:", Foreground = System.Windows.Media.Brushes.White, Margin = new System.Windows.Thickness(0, 0, 0, 4) });
        panel.Children.Add(nameBox);
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Emoji:", Foreground = System.Windows.Media.Brushes.White, Margin = new System.Windows.Thickness(0, 8, 0, 4) });
        panel.Children.Add(emojiBox);
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Color:", Foreground = System.Windows.Media.Brushes.White, Margin = new System.Windows.Thickness(0, 8, 0, 4) });
        panel.Children.Add(colorBox);
        panel.Children.Add(buttonsPanel);

        dialog.Content = panel;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task AddFriendToGroupAsync((FriendDto Friend, FriendGroup Group) parameter)
    {
        var (friend, group) = parameter;
        if (friend == null || group == null || _socialService == null) return;
        await _socialService.AddFriendToGroupAsync(group.Id, friend.UserId);
        OnPropertyChanged(nameof(FriendGroups));
    }

    #endregion

    #region Activity Commands

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
        else if (value == "Activity")
        {
            _ = LoadActivityDataAsync();
        }
    }

    private async Task LoadActivityDataAsync()
    {
        if (_socialService == null) return;

        FriendshipStats = await _socialService.GetFriendshipStatsAsync();
        TopFriendsByInteraction = await _socialService.GetTopFriendsByInteractionAsync(5);

        OnPropertyChanged(nameof(FriendshipStats));
        OnPropertyChanged(nameof(TopFriendsByInteraction));
        OnPropertyChanged(nameof(RecentInteractions));
        OnPropertyChanged(nameof(FriendRecommendations));
        OnPropertyChanged(nameof(HasRecommendations));
    }

    [RelayCommand]
    private async Task AddRecommendedFriendAsync(FriendRecommendation recommendation)
    {
        if (recommendation == null) return;

        try
        {
            await _friendService.SendFriendRequestByIdAsync(recommendation.UserId);
            if (_socialService != null)
            {
                await _socialService.DismissRecommendationAsync(recommendation.UserId);
                OnPropertyChanged(nameof(FriendRecommendations));
                OnPropertyChanged(nameof(HasRecommendations));
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to send friend request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DismissRecommendationAsync(FriendRecommendation recommendation)
    {
        if (recommendation == null || _socialService == null) return;

        await _socialService.DismissRecommendationAsync(recommendation.UserId);
        OnPropertyChanged(nameof(FriendRecommendations));
        OnPropertyChanged(nameof(HasRecommendations));
    }

    #endregion

    #region Friend Search and Filter Commands

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        FriendFilter = filter;
    }

    [RelayCommand]
    private void SetSort(string sort)
    {
        if (FriendSort == sort)
        {
            // Toggle direction if same sort
            SortAscending = !SortAscending;
        }
        else
        {
            FriendSort = sort;
            SortAscending = true;
        }
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortAscending = !SortAscending;
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        FriendFilter = "All";
        FriendSort = "Name";
        SortAscending = true;
    }

    #endregion

    #region Message Reactions and Pinned Messages Commands

    [RelayCommand]
    private async Task AddReactionAsync(DirectMessageDto message)
    {
        if (message == null || _socialService == null) return;

        // Show emoji picker or use default reaction
        var emojis = new[] { "👍", "❤️", "😂", "😮", "😢", "😡" };
        var random = new Random();
        var emoji = emojis[random.Next(emojis.Length)];

        await _socialService.AddReactionAsync(message.Id, emoji);
    }

    [RelayCommand]
    private async Task ToggleReactionAsync(ReactionGroup reaction)
    {
        if (reaction == null || _socialService == null) return;

        // For now, just toggle the reaction off if user reacted
        if (reaction.CurrentUserReacted)
        {
            await _socialService.RemoveReactionAsync(reaction.Emoji, reaction.Emoji);
        }
    }

    [RelayCommand]
    private void ReplyToMessage(DirectMessageDto message)
    {
        if (message == null) return;

        // Set the message input to reference the reply
        DmMessageInput = $"[Reply to {message.SenderUsername}] ";
    }

    [RelayCommand]
    private async Task TogglePinMessageAsync(DirectMessageDto message)
    {
        if (message == null || _socialService == null || SelectedFriend == null) return;

        // Check if message is already pinned
        var isPinned = PinnedMessages?.Any(p => p.MessageId == message.Id) ?? false;

        if (isPinned)
        {
            var pinned = PinnedMessages?.FirstOrDefault(p => p.MessageId == message.Id);
            if (pinned != null)
            {
                await _socialService.UnpinMessageAsync(pinned.Id);
            }
        }
        else
        {
            await _socialService.PinMessageAsync(SelectedFriend.UserId, message.Id);
        }

        OnPropertyChanged(nameof(PinnedMessages));
        OnPropertyChanged(nameof(HasPinnedMessages));
        OnPropertyChanged(nameof(PinnedMessagesCount));
    }

    [RelayCommand]
    private void ShowPinnedMessages()
    {
        if (SelectedFriend == null || PinnedMessages == null) return;

        var dialog = new System.Windows.Window
        {
            Title = $"Pinned Messages with {SelectedFriend.Username}",
            Width = 450,
            Height = 500,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 49, 54))
        };

        var scrollViewer = new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(16) };

        if (PinnedMessages.Count == 0)
        {
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "No pinned messages yet",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 155, 164)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 32, 0, 0)
            });
        }
        else
        {
            foreach (var pin in PinnedMessages.OrderByDescending(p => p.PinnedAt))
            {
                var card = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(54, 57, 63)),
                    CornerRadius = new System.Windows.CornerRadius(8),
                    Padding = new System.Windows.Thickness(12),
                    Margin = new System.Windows.Thickness(0, 0, 0, 8)
                };

                var cardPanel = new System.Windows.Controls.StackPanel();

                cardPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = $"📌 Pinned by {pin.PinnedByUsername}",
                    FontSize = 11,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 155, 164)),
                    Margin = new System.Windows.Thickness(0, 0, 0, 8)
                });

                cardPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = pin.SenderUsername,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.White
                });

                cardPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = pin.Content,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 221, 222)),
                    Margin = new System.Windows.Thickness(0, 4, 0, 0)
                });

                card.Child = cardPanel;
                panel.Children.Add(card);
            }
        }

        scrollViewer.Content = panel;
        dialog.Content = scrollViewer;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ShowMessageOptions(DirectMessageDto message)
    {
        if (message == null) return;

        // Copy message content to clipboard
        try
        {
            System.Windows.Clipboard.SetText(message.Content);
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowInfo("Copied", "Message copied to clipboard");
        }
        catch
        {
            // Clipboard operation failed silently
        }
    }

    #endregion
}
