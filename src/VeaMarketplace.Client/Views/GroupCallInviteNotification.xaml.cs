using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class GroupCallInviteNotification : Window
{
    private readonly GroupCallInviteDto _invite;
    private readonly IVoiceService _voiceService;
    private readonly DispatcherTimer _autoDeclineTimer;
    private readonly DispatcherTimer _ringTimer;
    private int _ringCount;

    public event Action<string>? OnAccepted;
    public event Action<string>? OnDeclined;

    public GroupCallInviteNotification(GroupCallInviteDto invite, IVoiceService voiceService)
    {
        InitializeComponent();

        _invite = invite;
        _voiceService = voiceService;

        // Set content
        HostNameText.Text = $"{invite.HostUsername} invited you";
        CallNameText.Text = invite.CallName;
        ParticipantText.Text = $"{invite.ParticipantCount} participant{(invite.ParticipantCount != 1 ? "s" : "")} in call";

        // Set avatar
        if (!string.IsNullOrEmpty(invite.HostAvatarUrl))
        {
            try
            {
                HostAvatarBrush.ImageSource = new BitmapImage(new Uri(invite.HostAvatarUrl));
            }
            catch { }
        }

        // Position in bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        // Auto-decline after 30 seconds
        _autoDeclineTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoDeclineTimer.Tick += (s, e) =>
        {
            _autoDeclineTimer.Stop();
            _ringTimer.Stop();
            DeclineCall();
        };
        _autoDeclineTimer.Start();

        // Ring sound timer
        _ringTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _ringTimer.Tick += RingTimer_Tick;

        // Play initial ring
        PlayRingSound();
        _ringTimer.Start();
    }

    private void RingTimer_Tick(object? sender, EventArgs e)
    {
        _ringCount++;
        if (_ringCount < 10) // Ring up to 10 times
        {
            PlayRingSound();
        }
        else
        {
            _ringTimer.Stop();
        }
    }

    private void PlayRingSound()
    {
        try
        {
            SystemSounds.Hand.Play();
        }
        catch { }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        _ringTimer.Stop();
        AcceptCall();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        _ringTimer.Stop();
        DeclineCall();
    }

    private async void AcceptCall()
    {
        try
        {
            await _voiceService.JoinGroupCallAsync(_invite.CallId);
            OnAccepted?.Invoke(_invite.CallId);
        }
        catch { }
        Close();
    }

    private async void DeclineCall()
    {
        try
        {
            await _voiceService.DeclineGroupCallAsync(_invite.CallId);
            OnDeclined?.Invoke(_invite.CallId);
        }
        catch { }
        Close();
    }

    public static GroupCallInviteNotification? Show(GroupCallInviteDto invite, IVoiceService voiceService)
    {
        GroupCallInviteNotification? notification = null;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            notification = new GroupCallInviteNotification(invite, voiceService);
            notification.Show();
        });
        return notification;
    }
}
