using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.ViewModels;

public partial class VoiceChannelViewModel : BaseViewModel
{
    private readonly IVoiceService _voiceService;

    [ObservableProperty]
    private ObservableCollection<VoiceUserState> _users = new();

    [ObservableProperty]
    private string _channelName = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    public VoiceChannelViewModel(IVoiceService voiceService)
    {
        _voiceService = voiceService;

        _voiceService.OnVoiceChannelUsers += users =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Users.Clear();
                foreach (var user in users)
                    Users.Add(user);
            });
        };

        _voiceService.OnUserJoinedVoice += user =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!Users.Any(u => u.ConnectionId == user.ConnectionId))
                    Users.Add(user);
            });
        };

        _voiceService.OnUserLeftVoice += user =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = Users.FirstOrDefault(u => u.ConnectionId == user.ConnectionId);
                if (existing != null)
                    Users.Remove(existing);
            });
        };
    }
}
