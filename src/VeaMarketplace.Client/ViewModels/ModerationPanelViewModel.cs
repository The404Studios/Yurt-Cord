using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

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
    private int _activeBans;

    [ObservableProperty]
    private int _activeMutes;

    [ObservableProperty]
    private int _pendingReports;

    [ObservableProperty]
    private int _autoModActions24h;

    [ObservableProperty]
    private int _totalModActions;

    [ObservableProperty]
    private ObservableCollection<ModerationLogDto> _recentModActions = new();

    // Bans
    [ObservableProperty]
    private ObservableCollection<UserBanDto> _bannedUsers = new();

    [ObservableProperty]
    private string _banSearchQuery = string.Empty;

    // Reports
    [ObservableProperty]
    private ObservableCollection<MessageReportDto> _pendingReportsList = new();

    // Ban Dialog
    [ObservableProperty]
    private bool _isBanDialogOpen;

    [ObservableProperty]
    private string _banUserId = string.Empty;

    [ObservableProperty]
    private string _banReason = string.Empty;

    [ObservableProperty]
    private bool _isPermanentBan = true;

    [ObservableProperty]
    private DateTime? _banExpiryDate;

    // Action Dialog
    [ObservableProperty]
    private bool _isActionDialogOpen;

    [ObservableProperty]
    private MessageReportDto? _selectedReport;

    [ObservableProperty]
    private string _actionReason = string.Empty;

    public ModerationPanelViewModel(Services.IApiService apiService)
    {
        _apiService = apiService;
        _ = LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var dashboard = await _apiService.GetModerationDashboardAsync();

            ActiveBans = dashboard.ActiveBans;
            ActiveMutes = dashboard.ActiveMutes;
            PendingReports = dashboard.PendingReports;
            AutoModActions24h = dashboard.AutoModActions24h;
            TotalModActions = dashboard.TotalModActions;

            RecentModActions.Clear();
            foreach (var action in dashboard.RecentActions)
                RecentModActions.Add(action);
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
        ErrorMessage = null;

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
        }
    }

    [RelayCommand]
    private async Task RefreshCurrentSection()
    {
        await Navigate(CurrentSection);
    }

    private async Task LoadBansAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var bans = await _apiService.GetBannedUsersAsync();

            BannedUsers.Clear();
            foreach (var ban in bans)
                BannedUsers.Add(ban);
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
            ErrorMessage = null;

            var reports = await _apiService.GetPendingReportsAsync();

            PendingReportsList.Clear();
            foreach (var report in reports)
                PendingReportsList.Add(report);
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
        BanUserId = string.Empty;
        BanReason = string.Empty;
        IsPermanentBan = true;
        BanExpiryDate = DateTime.Now.AddDays(7);
        IsBanDialogOpen = true;
    }

    [RelayCommand]
    private void CloseBanDialog()
    {
        IsBanDialogOpen = false;
    }

    [RelayCommand]
    private async Task SubmitBan()
    {
        if (string.IsNullOrWhiteSpace(BanUserId) || string.IsNullOrWhiteSpace(BanReason))
        {
            ErrorMessage = "User ID and reason are required.";
            return;
        }

        try
        {
            IsLoading = true;

            var request = new BanUserRequest
            {
                UserId = BanUserId,
                Reason = BanReason,
                ExpiresAt = IsPermanentBan ? null : BanExpiryDate
            };

            var success = await _apiService.BanUserAsync(request);

            if (success)
            {
                IsBanDialogOpen = false;
                await LoadBansAsync();
                ActiveBans++;
            }
            else
            {
                ErrorMessage = "Failed to ban user. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to ban user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UnbanUser(UserBanDto ban)
    {
        try
        {
            IsLoading = true;

            var success = await _apiService.UnbanUserAsync(ban.Id, "Unbanned by moderator");

            if (success)
            {
                BannedUsers.Remove(ban);
                ActiveBans--;
            }
            else
            {
                ErrorMessage = "Failed to unban user.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to unban user: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenActionDialog(MessageReportDto report)
    {
        SelectedReport = report;
        ActionReason = string.Empty;
        IsActionDialogOpen = true;
    }

    [RelayCommand]
    private void CloseActionDialog()
    {
        IsActionDialogOpen = false;
        SelectedReport = null;
    }

    [RelayCommand]
    private async Task TakeAction(string actionType)
    {
        if (SelectedReport == null) return;

        try
        {
            IsLoading = true;
            bool success = false;

            switch (actionType)
            {
                case "delete":
                    success = await _apiService.DeleteMessageAsync(SelectedReport.MessageId, ActionReason);
                    break;

                case "warn":
                    var warnRequest = new WarnUserRequest
                    {
                        UserId = SelectedReport.ReportedUserId,
                        Reason = string.IsNullOrEmpty(ActionReason) ? $"Warning for reported message: {SelectedReport.Reason}" : ActionReason
                    };
                    success = await _apiService.WarnUserAsync(warnRequest);
                    break;

                case "mute":
                    var muteRequest = new MuteUserRequest
                    {
                        UserId = SelectedReport.ReportedUserId,
                        Reason = ActionReason,
                        ExpiresAt = DateTime.UtcNow.AddHours(1)
                    };
                    success = await _apiService.MuteUserAsync(muteRequest);
                    if (success) ActiveMutes++;
                    break;

                case "ban":
                    var banRequest = new BanUserRequest
                    {
                        UserId = SelectedReport.ReportedUserId,
                        Reason = string.IsNullOrEmpty(ActionReason) ? $"Banned for reported message: {SelectedReport.Reason}" : ActionReason,
                        ExpiresAt = null
                    };
                    success = await _apiService.BanUserAsync(banRequest);
                    if (success) ActiveBans++;
                    break;
            }

            if (success)
            {
                // Resolve the report after taking action
                await _apiService.ResolveReportAsync(SelectedReport.Id, $"Action taken: {actionType}");
                PendingReportsList.Remove(SelectedReport);
                PendingReports--;
                IsActionDialogOpen = false;
                SelectedReport = null;
            }
            else
            {
                ErrorMessage = $"Failed to execute action: {actionType}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to take action: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DismissReport(MessageReportDto report)
    {
        try
        {
            IsLoading = true;

            var success = await _apiService.DismissReportAsync(report.Id);

            if (success)
            {
                PendingReportsList.Remove(report);
                PendingReports--;
            }
            else
            {
                ErrorMessage = "Failed to dismiss report.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to dismiss report: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ViewReportedMessage(MessageReportDto report)
    {
        // Could navigate to the message location or show it in a preview dialog
        SelectedReport = report;
    }

    [RelayCommand]
    private async Task SearchBans()
    {
        if (string.IsNullOrWhiteSpace(BanSearchQuery))
        {
            await LoadBansAsync();
            return;
        }

        var query = BanSearchQuery.ToLowerInvariant();
        var filtered = BannedUsers.Where(b =>
            b.Username.ToLowerInvariant().Contains(query) ||
            b.UserId.ToLowerInvariant().Contains(query) ||
            b.Reason.ToLowerInvariant().Contains(query)
        ).ToList();

        BannedUsers.Clear();
        foreach (var ban in filtered)
            BannedUsers.Add(ban);
    }
}
