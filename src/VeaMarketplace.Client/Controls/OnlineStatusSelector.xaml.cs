using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Controls;

public partial class OnlineStatusSelector : UserControl
{
    private readonly IApiService? _apiService;
    private UserStatus _currentStatus = UserStatus.Online;

    public event EventHandler<UserStatus>? StatusChanged;
    public event EventHandler? CustomStatusRequested;

    public UserStatus CurrentStatus
    {
        get => _currentStatus;
        set
        {
            _currentStatus = value;
            UpdateCheckmarks();
        }
    }

    public OnlineStatusSelector()
    {
        InitializeComponent();

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            return;

        _apiService = App.ServiceProvider.GetService(typeof(IApiService)) as IApiService;
        UpdateCheckmarks();
    }

    private void UpdateCheckmarks()
    {
        OnlineCheck.Visibility = _currentStatus == UserStatus.Online ? Visibility.Visible : Visibility.Collapsed;
        IdleCheck.Visibility = _currentStatus == UserStatus.Idle ? Visibility.Visible : Visibility.Collapsed;
        DndCheck.Visibility = _currentStatus == UserStatus.DoNotDisturb ? Visibility.Visible : Visibility.Collapsed;
        InvisibleCheck.Visibility = _currentStatus == UserStatus.Invisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void StatusOption_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string statusString)
            return;

        var newStatus = statusString switch
        {
            "Online" => UserStatus.Online,
            "Idle" => UserStatus.Idle,
            "DoNotDisturb" => UserStatus.DoNotDisturb,
            "Invisible" => UserStatus.Invisible,
            _ => UserStatus.Online
        };

        if (newStatus == _currentStatus)
            return;

        _currentStatus = newStatus;
        UpdateCheckmarks();

        // Update on server - map client UserStatus to shared UserPresenceStatus
        if (_apiService != null)
        {
            try
            {
                var presenceStatus = newStatus switch
                {
                    UserStatus.Online => UserPresenceStatus.Online,
                    UserStatus.Idle => UserPresenceStatus.Idle,
                    UserStatus.DoNotDisturb => UserPresenceStatus.DoNotDisturb,
                    UserStatus.Invisible => UserPresenceStatus.Invisible,
                    _ => UserPresenceStatus.Online
                };

                await _apiService.UpdatePresenceAsync(new UpdatePresenceRequest
                {
                    Status = presenceStatus
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update status: {ex.Message}");
            }
        }

        StatusChanged?.Invoke(this, newStatus);
    }

    private void SetCustomStatus_Click(object sender, MouseButtonEventArgs e)
    {
        CustomStatusRequested?.Invoke(this, EventArgs.Empty);
    }
}
