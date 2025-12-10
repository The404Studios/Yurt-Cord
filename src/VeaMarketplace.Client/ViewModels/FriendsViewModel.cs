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
    private readonly DispatcherTimer _callTimer;
    private DateTime _callStartTime;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _friendRequestUsername = string.Empty;

    [ObservableProperty]
    private string _dmMessageInput = string.Empty;

    [ObservableProperty]
    private FriendDto? _selectedFriend;

    [ObservableProperty]
    private ConversationDto? _selectedConversation;

    [ObservableProperty]
    private bool _isInDMView;

    [ObservableProperty]
    private string _currentView = "Friends"; // Friends, Pending, Add

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

    public ObservableCollection<FriendDto> Friends => _friendService.Friends;
    public ObservableCollection<FriendRequestDto> PendingRequests => _friendService.PendingRequests;
    public ObservableCollection<FriendRequestDto> OutgoingRequests => _friendService.OutgoingRequests;
    public ObservableCollection<ConversationDto> Conversations => _friendService.Conversations;
    public ObservableCollection<DirectMessageDto> CurrentDMHistory => _friendService.CurrentDMHistory;

    public FriendsViewModel(IFriendService friendService, IApiService apiService, IVoiceService voiceService)
    {
        _friendService = friendService;
        _apiService = apiService;
        _voiceService = voiceService;

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
        if (string.IsNullOrWhiteSpace(FriendRequestUsername)) return;

        ClearError();
        try
        {
            await _friendService.SendFriendRequestAsync(FriendRequestUsername);
            FriendRequestUsername = string.Empty;
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
        // This would connect to voice hub and start call
        // For now just indicate call started
        IsInCall = true;
        _callStartTime = DateTime.UtcNow;
        _callTimer.Start();
    }

    [RelayCommand]
    private void AcceptCall()
    {
        HasIncomingCall = false;
        IsInCall = true;
        _callStartTime = DateTime.UtcNow;
        _callTimer.Start();
    }

    [RelayCommand]
    private void DeclineCall()
    {
        HasIncomingCall = false;
        CurrentCall = null;
    }

    [RelayCommand]
    private void EndCall()
    {
        IsInCall = false;
        _callTimer.Stop();
        CallDuration = "00:00";
        CurrentCall = null;
    }
}
