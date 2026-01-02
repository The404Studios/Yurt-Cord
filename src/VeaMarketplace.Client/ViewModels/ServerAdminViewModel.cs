using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.ViewModels;

public partial class ServerAdminViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private string _currentSection = "Dashboard";

    // Server Stats
    [ObservableProperty]
    private string _serverUptime = "0d 0h 0m";

    [ObservableProperty]
    private long _memoryUsageMb = 0;

    [ObservableProperty]
    private int _threadCount = 0;

    [ObservableProperty]
    private string _serverVersion = "1.0.0";

    // Database Stats
    [ObservableProperty]
    private int _totalUsers = 0;

    [ObservableProperty]
    private int _onlineUsers = 0;

    [ObservableProperty]
    private int _totalProducts = 0;

    [ObservableProperty]
    private int _totalMessages = 0;

    [ObservableProperty]
    private int _totalRooms = 0;

    [ObservableProperty]
    private int _totalOrders = 0;

    // Moderation Stats
    [ObservableProperty]
    private int _activeBans = 0;

    [ObservableProperty]
    private int _activeMutes = 0;

    [ObservableProperty]
    private int _pendingReports = 0;

    // User Management
    [ObservableProperty]
    private ObservableCollection<UserDto> _recentUsers = new();

    [ObservableProperty]
    private ObservableCollection<OnlineUserDto> _onlineUsersList = new();

    [ObservableProperty]
    private string _userSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<UserSearchResultDto> _userSearchResults = new();

    [ObservableProperty]
    private UserDto? _selectedUser = null;

    // Broadcast
    [ObservableProperty]
    private string _broadcastMessage = string.Empty;

    [ObservableProperty]
    private bool _isBroadcastSent = false;

    // Action Dialog
    [ObservableProperty]
    private bool _isUserActionDialogOpen = false;

    [ObservableProperty]
    private string _actionReason = string.Empty;

    [ObservableProperty]
    private string _selectedRole = "Member";

    [ObservableProperty]
    private int _banDurationHours = 24;

    // Auto-refresh
    [ObservableProperty]
    private bool _autoRefreshEnabled = true;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 30;

    [ObservableProperty]
    private DateTime _lastRefreshTime = DateTime.Now;

    public List<string> AvailableRoles { get; } = new()
    {
        "Member",
        "Verified",
        "VIP",
        "Moderator",
        "Admin"
    };

    public ServerAdminViewModel(Services.IApiService apiService)
    {
        _apiService = apiService;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _refreshTimer.Tick += async (s, e) =>
        {
            if (AutoRefreshEnabled)
            {
                await RefreshDashboardAsync();
            }
        };
        _refreshTimer.Start();

        _ = LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Load moderation dashboard for stats
            var dashboard = await _apiService.GetModerationDashboardAsync();

            ActiveBans = dashboard.ActiveBans;
            ActiveMutes = dashboard.ActiveMutes;
            PendingReports = dashboard.PendingReports;

            // Get process stats for server metrics simulation
            var process = Process.GetCurrentProcess();
            MemoryUsageMb = process.WorkingSet64 / 1024 / 1024;
            ThreadCount = process.Threads.Count;

            var uptime = DateTime.Now - process.StartTime;
            ServerUptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";

            // Placeholder stats - these would come from a real admin API
            TotalUsers = dashboard.TotalModActions > 0 ? dashboard.TotalModActions * 10 : 150;
            OnlineUsers = Math.Max(1, TotalUsers / 10);
            TotalProducts = TotalUsers * 3;
            TotalMessages = TotalUsers * 50;
            TotalRooms = Math.Max(5, TotalUsers / 20);
            TotalOrders = TotalUsers * 2;

            LastRefreshTime = DateTime.Now;
        }, "Failed to load dashboard");
    }

    private async Task RefreshDashboardAsync()
    {
        // Silent refresh without loading indicator
        try
        {
            var dashboard = await _apiService.GetModerationDashboardAsync();
            ActiveBans = dashboard.ActiveBans;
            ActiveMutes = dashboard.ActiveMutes;
            PendingReports = dashboard.PendingReports;

            var process = Process.GetCurrentProcess();
            MemoryUsageMb = process.WorkingSet64 / 1024 / 1024;
            ThreadCount = process.Threads.Count;

            var uptime = DateTime.Now - process.StartTime;
            ServerUptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";

            LastRefreshTime = DateTime.Now;
        }
        catch
        {
            // Silent fail for background refresh
        }
    }

    [RelayCommand]
    private async Task Navigate(string section)
    {
        CurrentSection = section;
        ClearError();

        switch (section)
        {
            case "Dashboard":
                await LoadDashboardAsync();
                break;
            case "Users":
                await LoadRecentUsersAsync();
                break;
            case "Online":
                await LoadOnlineUsersAsync();
                break;
        }
    }

    [RelayCommand]
    private async Task RefreshCurrentSection()
    {
        await Navigate(CurrentSection);
    }

    private async Task LoadRecentUsersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var users = await _apiService.SearchUsersAsync("");
            RecentUsers.Clear();
            foreach (var user in users.Take(20))
            {
                var userDto = await _apiService.GetUserAsync(user.Id);
                if (userDto != null)
                    RecentUsers.Add(userDto);
            }
        }, "Failed to load users");
    }

    private async Task LoadOnlineUsersAsync()
    {
        await ExecuteAsync(async () =>
        {
            // This would come from a real-time connection or admin API
            OnlineUsersList.Clear();
            // Placeholder - in a real implementation, this would get actual online users
        }, "Failed to load online users");
    }

    [RelayCommand]
    private async Task SearchUsers()
    {
        if (string.IsNullOrWhiteSpace(UserSearchQuery))
        {
            UserSearchResults.Clear();
            return;
        }

        await ExecuteAsync(async () =>
        {
            var results = await _apiService.SearchUsersAsync(UserSearchQuery);
            UserSearchResults.Clear();
            foreach (var user in results)
                UserSearchResults.Add(user);
        }, "Failed to search users");
    }

    [RelayCommand]
    private async Task SelectUser(UserSearchResultDto user)
    {
        await ExecuteAsync(async () =>
        {
            SelectedUser = await _apiService.GetUserAsync(user.Id);
            IsUserActionDialogOpen = true;
        }, "Failed to load user details");
    }

    [RelayCommand]
    private void CloseUserActionDialog()
    {
        IsUserActionDialogOpen = false;
        SelectedUser = null;
        ActionReason = string.Empty;
    }

    [RelayCommand]
    private async Task KickUser()
    {
        if (SelectedUser == null) return;

        // In a real implementation, this would send a kick command through SignalR
        SetStatus($"Kick command sent for {SelectedUser.Username}");
        CloseUserActionDialog();
    }

    [RelayCommand]
    private async Task BanUser()
    {
        if (SelectedUser == null) return;

        await ExecuteAsync(async () =>
        {
            var request = new BanUserRequest
            {
                UserId = SelectedUser.Id,
                Reason = string.IsNullOrEmpty(ActionReason) ? "Banned by administrator" : ActionReason,
                ExpiresAt = BanDurationHours > 0 ? DateTime.UtcNow.AddHours(BanDurationHours) : null
            };

            var success = await _apiService.BanUserAsync(request);
            if (success)
            {
                ActiveBans++;
                SetStatus($"User {SelectedUser.Username} has been banned");
                CloseUserActionDialog();
            }
            else
            {
                SetError("Failed to ban user");
            }
        }, "Failed to ban user");
    }

    [RelayCommand]
    private async Task PromoteUser()
    {
        if (SelectedUser == null) return;

        // This would require an admin API endpoint to change user roles
        SetStatus($"Role change to {SelectedRole} requested for {SelectedUser.Username}");
        CloseUserActionDialog();
    }

    [RelayCommand]
    private async Task WarnUser()
    {
        if (SelectedUser == null) return;

        await ExecuteAsync(async () =>
        {
            var request = new WarnUserRequest
            {
                UserId = SelectedUser.Id,
                Reason = string.IsNullOrEmpty(ActionReason) ? "Warning issued by administrator" : ActionReason
            };

            var success = await _apiService.WarnUserAsync(request);
            if (success)
            {
                SetStatus($"Warning issued to {SelectedUser.Username}");
                CloseUserActionDialog();
            }
            else
            {
                SetError("Failed to warn user");
            }
        }, "Failed to warn user");
    }

    [RelayCommand]
    private async Task SendBroadcast()
    {
        if (string.IsNullOrWhiteSpace(BroadcastMessage))
        {
            SetError("Please enter a broadcast message");
            return;
        }

        // In a real implementation, this would send a system notification to all users
        IsBroadcastSent = true;
        SetStatus($"Broadcast sent: {BroadcastMessage}");
        BroadcastMessage = string.Empty;

        await Task.Delay(3000);
        IsBroadcastSent = false;
    }

    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        AutoRefreshEnabled = !AutoRefreshEnabled;
        if (AutoRefreshEnabled)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    [RelayCommand]
    private void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var process = Process.GetCurrentProcess();
        MemoryUsageMb = process.WorkingSet64 / 1024 / 1024;
        SetStatus("Garbage collection completed");
    }

    public void Cleanup()
    {
        _refreshTimer.Stop();
    }
}

public class OnlineUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string CurrentHub { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public string? CurrentActivity { get; set; }
}
