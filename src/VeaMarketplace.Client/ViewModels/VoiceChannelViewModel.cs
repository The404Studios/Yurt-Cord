using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.Views;

namespace VeaMarketplace.Client.ViewModels;

public partial class VoiceChannelViewModel : BaseViewModel
{
    private readonly IVoiceService _voiceService;

    [ObservableProperty]
    private ObservableCollection<VoiceUserState> _users = [];

    [ObservableProperty]
    private string _channelName = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isScreenSharing;

    [ObservableProperty]
    private ScreenShareStats? _screenShareStats;

    [ObservableProperty]
    private ObservableCollection<RemoteScreenShare> _activeScreenShares = [];

    public VoiceChannelViewModel(IVoiceService voiceService)
    {
        _voiceService = voiceService;

        _voiceService.OnVoiceChannelUsers += OnVoiceChannelUsers;
        _voiceService.OnUserJoinedVoice += OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice += OnUserLeftVoice;
        _voiceService.OnUserScreenShareChanged += OnUserScreenShareChanged;
        _voiceService.OnScreenShareStatsUpdated += OnScreenShareStatsUpdated;
    }

    #region Event Handlers

    private void OnVoiceChannelUsers(List<VoiceUserState> users)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Users.Clear();
            foreach (var user in users)
                Users.Add(user);
        });
    }

    private void OnUserJoinedVoice(VoiceUserState user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!Users.Any(u => u.ConnectionId == user.ConnectionId))
                Users.Add(user);
        });
    }

    private void OnUserLeftVoice(VoiceUserState user)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = Users.FirstOrDefault(u => u.ConnectionId == user.ConnectionId);
            if (existing != null)
                Users.Remove(existing);
        });
    }

    private void OnUserScreenShareChanged(string connectionId, bool isSharing)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var user = Users.FirstOrDefault(u => u.ConnectionId == connectionId);
            if (user != null)
            {
                user.IsScreenSharing = isSharing;
            }

            if (!isSharing)
            {
                var share = ActiveScreenShares.FirstOrDefault(s => s.ConnectionId == connectionId);
                if (share != null)
                    ActiveScreenShares.Remove(share);
            }
        });
    }

    private void OnScreenShareStatsUpdated(ScreenShareStats stats)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ScreenShareStats = stats;
        });
    }

    #endregion

    public void Cleanup()
    {
        _voiceService.OnVoiceChannelUsers -= OnVoiceChannelUsers;
        _voiceService.OnUserJoinedVoice -= OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice -= OnUserLeftVoice;
        _voiceService.OnUserScreenShareChanged -= OnUserScreenShareChanged;
        _voiceService.OnScreenShareStatsUpdated -= OnScreenShareStatsUpdated;
    }

    [RelayCommand]
    private async Task StartScreenShareAsync()
    {
        try
        {
            if (!_voiceService.IsInVoiceChannel)
            {
                SetError("You must be in a voice channel to share your screen.");
                return;
            }

            var picker = new ScreenSharePicker(_voiceService);
            picker.Owner = System.Windows.Application.Current.MainWindow;

            if (picker.ShowDialog() == true && picker.SelectedDisplay != null)
            {
                var settings = picker.GetSettings();
                await _voiceService.StartScreenShareAsync(picker.SelectedDisplay, settings);
                IsScreenSharing = true;
            }
        }
        catch (Exception ex)
        {
            SetError($"Failed to start screen share: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopScreenShareAsync()
    {
        try
        {
            await _voiceService.StopScreenShareAsync();
            IsScreenSharing = false;
            ScreenShareStats = null;
        }
        catch (Exception ex)
        {
            SetError($"Failed to stop screen share: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ViewScreenShare(VoiceUserState user)
    {
        if (user == null || !user.IsScreenSharing) return;

        try
        {
            var viewer = new ScreenShareViewer(_voiceService, user.ConnectionId, user.Username);
            viewer.Show();
        }
        catch (Exception ex)
        {
            SetError($"Failed to open screen share viewer: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _voiceService.IsMuted = !_voiceService.IsMuted;
        OnPropertyChanged(nameof(IsMuted));
    }

    [RelayCommand]
    private void ToggleDeafen()
    {
        _voiceService.IsDeafened = !_voiceService.IsDeafened;
        OnPropertyChanged(nameof(IsDeafened));
    }

    [RelayCommand]
    private async Task LeaveVoiceChannelAsync()
    {
        try
        {
            if (IsScreenSharing)
            {
                await StopScreenShareAsync();
            }
            await _voiceService.LeaveVoiceChannelAsync();
            IsConnected = false;
        }
        catch (Exception ex)
        {
            SetError($"Failed to leave voice channel: {ex.Message}");
        }
    }

    public bool IsMuted => _voiceService.IsMuted;
    public bool IsDeafened => _voiceService.IsDeafened;
}
