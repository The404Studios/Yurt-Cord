using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.ViewModels;

public partial class ModerationPanelViewModel : BaseViewModel
{
    private readonly Services.IApiService _apiService;

    [ObservableProperty]
    private string _currentSection = "Dashboard";

    [ObservableProperty]
    private object? _currentView;

    // Dashboard Stats
    [ObservableProperty]
    private int _activeBans = 0;

    [ObservableProperty]
    private int _pendingReports = 0;

    [ObservableProperty]
    private int _autoModActions24h = 0;

    [ObservableProperty]
    private int _totalModActions = 0;

    [ObservableProperty]
    private ObservableCollection<ModerationLogDto> _recentModActions = new();

    // Bans
    [ObservableProperty]
    private ObservableCollection<UserBanDto> _bannedUsers = new();

    // Reports
    [ObservableProperty]
    private ObservableCollection<MessageReportDto> _pendingReportsList = new();

    public ModerationPanelViewModel(Services.IApiService apiService)
    {
        _apiService = apiService;
        LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load moderation dashboard
            // var dashboard = await _apiService.GetModerationDashboardAsync();
            // ActiveBans = dashboard.ActiveBans;
            // PendingReports = dashboard.PendingReports;
            // AutoModActions24h = dashboard.AutoModActions24h;
            // TotalModActions = dashboard.TotalModActions;

            // RecentModActions.Clear();
            // foreach (var action in dashboard.RecentActions)
            //     RecentModActions.Add(action);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Navigate(string section)
    {
        CurrentSection = section;

        switch (section)
        {
            case "Dashboard":
                await LoadDashboardAsync();
                break;
            case "Bans":
                await LoadBansAsync();
                break;
            case "Reports":
                await LoadReportsAsync();
                break;
            // Add more cases as needed
        }
    }

    private async Task LoadBansAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load bans
            // var bans = await _apiService.GetBannedUsersAsync();
            // BannedUsers.Clear();
            // foreach (var ban in bans)
            //     BannedUsers.Add(ban);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load bans: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadReportsAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Call API to load reports
            // var reports = await _apiService.GetPendingReportsAsync();
            // PendingReportsList.Clear();
            // foreach (var report in reports)
            //     PendingReportsList.Add(report);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load reports: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenBanDialog()
    {
        // TODO: Open ban user dialog
    }

    [RelayCommand]
    private async Task UnbanUser(UserBanDto ban)
    {
        try
        {
            // TODO: Call API to unban user
            // await _apiService.UnbanUserAsync(ban.UserId);
            BannedUsers.Remove(ban);
            ActiveBans--;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to unban user: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TakeAction(MessageReportDto report)
    {
        // TODO: Open action dialog with options:
        // - Delete message
        // - Warn user
        // - Mute user
        // - Ban user
    }

    [RelayCommand]
    private async Task DismissReport(MessageReportDto report)
    {
        try
        {
            // TODO: Call API to dismiss report
            // await _apiService.DismissReportAsync(report.Id);
            PendingReportsList.Remove(report);
            PendingReports--;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to dismiss report: {ex.Message}";
        }
    }
}
