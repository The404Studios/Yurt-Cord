using System.Media;
using System.Windows;
using System.Windows.Media;
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

        // Set avatar with fallback to initial
        if (!string.IsNullOrEmpty(invite.HostAvatarUrl))
        {
            try
            {
                HostAvatarBrush.ImageSource = new BitmapImage(new Uri(invite.HostAvatarUrl));
            }
            catch
            {
                // Avatar load failed - use default gradient background
            }
        }

        // Position in bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        // Ring sound timer (initialize first since auto-decline timer references it)
        _ringTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _ringTimer.Tick += RingTimer_Tick;

        // Auto-decline after 30 seconds
        _autoDeclineTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoDeclineTimer.Tick += AutoDeclineTimer_Tick;
        _autoDeclineTimer.Start();

        // Play initial ring
        PlayRingSound();
        _ringTimer.Start();

        // Clean up on close
        Closed += OnWindowClosed;
    }

    private void AutoDeclineTimer_Tick(object? sender, EventArgs e)
    {
        _autoDeclineTimer.Stop();
        _ringTimer.Stop();
        _ = DeclineCallAsync();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _autoDeclineTimer.Stop();
        _autoDeclineTimer.Tick -= AutoDeclineTimer_Tick;
        _ringTimer.Stop();
        _ringTimer.Tick -= RingTimer_Tick;
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to play ring sound: {ex.Message}");
        }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        _ringTimer.Stop();
        _ = AcceptCallAsync();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        _autoDeclineTimer.Stop();
        _ringTimer.Stop();
        _ = DeclineCallAsync();
    }

    private async Task AcceptCallAsync()
    {
        try
        {
            await _voiceService.JoinGroupCallAsync(_invite.CallId);
            OnAccepted?.Invoke(_invite.CallId);
        }
        catch (Exception ex)
        {
            var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
            toastService?.ShowError("Join Failed", $"Could not join call: {ex.Message}");
        }
        Close();
    }

    private async Task DeclineCallAsync()
    {
        try
        {
            await _voiceService.DeclineGroupCallAsync(_invite.CallId);
            OnDeclined?.Invoke(_invite.CallId);
        }
        catch
        {
            // Decline failed silently - just close the notification
        }
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
