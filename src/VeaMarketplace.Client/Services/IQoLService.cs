using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Quality of Life service - manages features that Discord doesn't offer
/// </summary>
public interface IQoLService
{
    // Message Templates
    ObservableCollection<MessageTemplate> Templates { get; }
    void AddTemplate(MessageTemplate template);
    void UpdateTemplate(MessageTemplate template);
    void DeleteTemplate(string templateId);
    MessageTemplate? GetTemplateByShortcut(string shortcut);
    void IncrementTemplateUsage(string templateId);

    // Scheduled Messages
    ObservableCollection<ScheduledMessage> ScheduledMessages { get; }
    void ScheduleMessage(ScheduledMessage message);
    void CancelScheduledMessage(string messageId);
    event Action<ScheduledMessage>? OnScheduledMessageReady;

    // Status Scheduler
    ObservableCollection<ScheduledStatus> ScheduledStatuses { get; }
    void AddScheduledStatus(ScheduledStatus status);
    void RemoveScheduledStatus(string statusId);
    ScheduledStatus? GetActiveScheduledStatus();
    event Action<ScheduledStatus>? OnStatusChangeRequired;

    // Friend Notes
    ObservableCollection<FriendNote> FriendNotes { get; }
    void SetFriendNote(FriendNote note);
    FriendNote? GetFriendNote(string userId);
    void DeleteFriendNote(string userId);
    List<FriendNote> GetFriendsByTag(string tag);
    List<FriendNote> GetUpcomingBirthdays(int days = 7);

    // Smart DND
    ObservableCollection<SmartDndSchedule> DndSchedules { get; }
    void AddDndSchedule(SmartDndSchedule schedule);
    void RemoveDndSchedule(string scheduleId);
    bool IsInDndPeriod();
    SmartDndSchedule? GetActiveDndSchedule();
    bool ShouldNotify(string senderId);

    // Activity Insights
    void TrackMessageSent();
    void TrackVoiceMinute();
    void TrackChannelActivity(string channelId);
    void TrackFriendInteraction(string friendId);
    ActivityInsight GetTodayInsight();
    List<ActivityInsight> GetWeeklyInsights();

    // Quick Actions
    ObservableCollection<QuickAction> QuickActions { get; }
    void AddQuickAction(QuickAction action);
    void RemoveQuickAction(string actionId);
    void ExecuteQuickAction(string actionId);

    // Auto-Away
    void ResetActivityTimer();
    bool IsAutoAway { get; }
    event Action? OnAutoAwayTriggered;
    event Action? OnAutoAwayReset;

    // Friend Anniversaries & Special Dates
    List<FriendAnniversary> GetUpcomingAnniversaries(int days = 7);
    void AddFriendshipAnniversary(string friendId, string friendUsername, DateTime friendshipDate);
    event Action<FriendAnniversary>? OnAnniversaryReminder;

    // Online Notifications (per-friend)
    ObservableCollection<OnlineNotificationPreference> OnlineNotifications { get; }
    void SetOnlineNotification(string friendId, bool enabled, bool soundEnabled = true);
    bool ShouldNotifyOnline(string friendId);
    void RemoveOnlineNotification(string friendId);
    event Action<string, string>? OnFriendOnlineNotification; // friendId, friendUsername
}

/// <summary>
/// Friend anniversary/special date info
/// </summary>
public class FriendAnniversary
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string? FriendAvatarUrl { get; set; }
    public DateTime Date { get; set; }
    public AnniversaryType Type { get; set; }
    public int Years { get; set; }
    public string Description => Type switch
    {
        AnniversaryType.Friendship => $"🎉 {Years} year{(Years != 1 ? "s" : "")} of friendship!",
        AnniversaryType.Birthday => "🎂 Birthday!",
        AnniversaryType.Custom => "📅 Special day",
        _ => "📌 Reminder"
    };
}

public enum AnniversaryType
{
    Friendship,
    Birthday,
    Custom
}

/// <summary>
/// Online notification preference for a specific friend
/// </summary>
public class OnlineNotificationPreference
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string? FriendAvatarUrl { get; set; }
    public bool Enabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QoLService : IQoLService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _schedulerTimer;
    private readonly DispatcherTimer _autoAwayTimer;
    private readonly HashSet<string> _todayInteractedFriends = [];
    private DateTime _lastActivityTime = DateTime.Now;
    private bool _isAutoAway;

    public ObservableCollection<MessageTemplate> Templates { get; } = [];
    public ObservableCollection<ScheduledMessage> ScheduledMessages { get; } = [];
    public ObservableCollection<ScheduledStatus> ScheduledStatuses { get; } = [];
    public ObservableCollection<FriendNote> FriendNotes { get; } = [];
    public ObservableCollection<SmartDndSchedule> DndSchedules { get; } = [];
    public ObservableCollection<QuickAction> QuickActions { get; } = [];
    public ObservableCollection<OnlineNotificationPreference> OnlineNotifications { get; } = [];

    private readonly Dictionary<string, DateTime> _friendshipDates = new();

    public bool IsAutoAway => _isAutoAway;

    public event Action<ScheduledMessage>? OnScheduledMessageReady;
    public event Action<ScheduledStatus>? OnStatusChangeRequired;
    public event Action? OnAutoAwayTriggered;
    public event Action? OnAutoAwayReset;
#pragma warning disable CS0067 // Event is never used - kept for future API compatibility
    public event Action<FriendAnniversary>? OnAnniversaryReminder;
#pragma warning restore CS0067
    public event Action<string, string>? OnFriendOnlineNotification;

    public QoLService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // Load from settings
        LoadFromSettings();

        // Set up scheduler timer (checks every minute)
        _schedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _schedulerTimer.Tick += SchedulerTimer_Tick;
        _schedulerTimer.Start();

        // Set up auto-away timer (checks every minute)
        _autoAwayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _autoAwayTimer.Tick += AutoAwayTimer_Tick;
        if (_settingsService.Settings.EnableAutoAway)
        {
            _autoAwayTimer.Start();
        }
    }

    private void LoadFromSettings()
    {
        Templates.Clear();
        foreach (var template in _settingsService.Settings.MessageTemplates)
            Templates.Add(template);

        ScheduledMessages.Clear();
        foreach (var msg in _settingsService.Settings.ScheduledMessages.Where(m => !m.IsSent))
            ScheduledMessages.Add(msg);

        ScheduledStatuses.Clear();
        foreach (var status in _settingsService.Settings.ScheduledStatuses)
            ScheduledStatuses.Add(status);

        FriendNotes.Clear();
        foreach (var note in _settingsService.Settings.FriendNotes)
            FriendNotes.Add(note);

        DndSchedules.Clear();
        foreach (var schedule in _settingsService.Settings.DndSchedules)
            DndSchedules.Add(schedule);

        QuickActions.Clear();
        foreach (var action in _settingsService.Settings.QuickActions)
            QuickActions.Add(action);

        // Add default templates if empty
        if (Templates.Count == 0)
        {
            AddDefaultTemplates();
        }

        // Add default quick actions if empty
        if (QuickActions.Count == 0)
        {
            AddDefaultQuickActions();
        }

        // Load friendship dates
        LoadFriendshipDates();

        // Load online notifications
        LoadOnlineNotifications();
    }

    private void AddDefaultTemplates()
    {
        var defaults = new[]
        {
            new MessageTemplate { Name = "AFK", Content = "I'm AFK, will be back soon!", Shortcut = "/afk", Category = "Status" },
            new MessageTemplate { Name = "BRB", Content = "BRB in 5 minutes!", Shortcut = "/brb", Category = "Status" },
            new MessageTemplate { Name = "Good Morning", Content = "Good morning everyone! Hope you're having a great day!", Shortcut = "/gm", Category = "Greetings" },
            new MessageTemplate { Name = "Good Night", Content = "Good night! Catch you all tomorrow!", Shortcut = "/gn", Category = "Greetings" },
            new MessageTemplate { Name = "Thanks", Content = "Thanks for the help! Really appreciate it.", Shortcut = "/ty", Category = "General" },
            new MessageTemplate { Name = "Welcome", Content = "Welcome to the server! Feel free to ask if you have any questions.", Shortcut = "/welcome", Category = "Moderation" },
            new MessageTemplate { Name = "Busy", Content = "Currently busy with work, will respond when I can!", Shortcut = "/busy", Category = "Status" },
            new MessageTemplate { Name = "In Meeting", Content = "In a meeting right now, will be available in about an hour.", Shortcut = "/meeting", Category = "Status" }
        };

        foreach (var template in defaults)
        {
            Templates.Add(template);
        }
        SaveTemplates();
    }

    private void AddDefaultQuickActions()
    {
        var defaults = new[]
        {
            new QuickAction { Name = "Go Online", ActionType = "status", ActionValue = "Online", Hotkey = "Ctrl+1", Order = 1 },
            new QuickAction { Name = "Go Away", ActionType = "status", ActionValue = "Away", Hotkey = "Ctrl+2", Order = 2 },
            new QuickAction { Name = "Do Not Disturb", ActionType = "status", ActionValue = "DND", Hotkey = "Ctrl+3", Order = 3 },
            new QuickAction { Name = "Go Invisible", ActionType = "status", ActionValue = "Invisible", Hotkey = "Ctrl+4", Order = 4 },
            new QuickAction { Name = "Open Marketplace", ActionType = "navigation", ActionValue = "Marketplace", Hotkey = "Ctrl+M", Order = 5 },
            new QuickAction { Name = "Open Friends", ActionType = "navigation", ActionValue = "Friends", Hotkey = "Ctrl+F", Order = 6 }
        };

        foreach (var action in defaults)
        {
            QuickActions.Add(action);
        }
        SaveQuickActions();
    }

    private void SchedulerTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;

        // Check scheduled messages
        foreach (var msg in ScheduledMessages.Where(m => !m.IsSent && m.ScheduledTime <= now).ToList())
        {
            OnScheduledMessageReady?.Invoke(msg);
            msg.IsSent = true;
            msg.SentAt = now;

            if (!msg.IsRecurring)
            {
                ScheduledMessages.Remove(msg);
            }
            else
            {
                // Reschedule recurring messages
                msg.IsSent = false;
                msg.ScheduledTime = GetNextRecurrence(msg.ScheduledTime, msg.RecurrenceType);
            }
        }
        SaveScheduledMessages();

        // Check scheduled statuses
        foreach (var status in ScheduledStatuses.Where(s => s.IsActive))
        {
            if (ShouldActivateStatus(status, now))
            {
                OnStatusChangeRequired?.Invoke(status);
            }
        }
    }

    private bool ShouldActivateStatus(ScheduledStatus status, DateTime now)
    {
        if (status.ActiveDays.Count > 0 && !status.ActiveDays.Contains(now.DayOfWeek))
            return false;

        if (status.StartTime.Date == now.Date &&
            status.StartTime.Hour == now.Hour &&
            status.StartTime.Minute == now.Minute)
        {
            return true;
        }

        return false;
    }

    private DateTime GetNextRecurrence(DateTime current, RecurrenceType type)
    {
        return type switch
        {
            RecurrenceType.Daily => current.AddDays(1),
            RecurrenceType.Weekly => current.AddDays(7),
            RecurrenceType.Weekdays => GetNextWeekday(current),
            RecurrenceType.Monthly => current.AddMonths(1),
            _ => current.AddDays(1)
        };
    }

    private DateTime GetNextWeekday(DateTime current)
    {
        var next = current.AddDays(1);
        while (next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        return next;
    }

    private void AutoAwayTimer_Tick(object? sender, EventArgs e)
    {
        if (!_settingsService.Settings.EnableAutoAway) return;

        var idleMinutes = (DateTime.Now - _lastActivityTime).TotalMinutes;
        var threshold = _settingsService.Settings.AutoAwayMinutes;

        if (!_isAutoAway && idleMinutes >= threshold)
        {
            _isAutoAway = true;
            OnAutoAwayTriggered?.Invoke();
        }
    }

    #region Message Templates

    public void AddTemplate(MessageTemplate template)
    {
        Templates.Add(template);
        SaveTemplates();
    }

    public void UpdateTemplate(MessageTemplate template)
    {
        var existing = Templates.FirstOrDefault(t => t.Id == template.Id);
        if (existing != null)
        {
            var index = Templates.IndexOf(existing);
            Templates[index] = template;
            SaveTemplates();
        }
    }

    public void DeleteTemplate(string templateId)
    {
        var template = Templates.FirstOrDefault(t => t.Id == templateId);
        if (template != null)
        {
            Templates.Remove(template);
            SaveTemplates();
        }
    }

    public MessageTemplate? GetTemplateByShortcut(string shortcut)
    {
        return Templates.FirstOrDefault(t =>
            t.Shortcut?.Equals(shortcut, StringComparison.OrdinalIgnoreCase) == true);
    }

    public void IncrementTemplateUsage(string templateId)
    {
        var template = Templates.FirstOrDefault(t => t.Id == templateId);
        if (template != null)
        {
            template.UseCount++;
            SaveTemplates();
        }
    }

    private void SaveTemplates()
    {
        _settingsService.Settings.MessageTemplates = Templates.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Scheduled Messages

    public void ScheduleMessage(ScheduledMessage message)
    {
        ScheduledMessages.Add(message);
        SaveScheduledMessages();
    }

    public void CancelScheduledMessage(string messageId)
    {
        var message = ScheduledMessages.FirstOrDefault(m => m.Id == messageId);
        if (message != null)
        {
            ScheduledMessages.Remove(message);
            SaveScheduledMessages();
        }
    }

    private void SaveScheduledMessages()
    {
        _settingsService.Settings.ScheduledMessages = ScheduledMessages.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Status Scheduler

    public void AddScheduledStatus(ScheduledStatus status)
    {
        ScheduledStatuses.Add(status);
        SaveScheduledStatuses();
    }

    public void RemoveScheduledStatus(string statusId)
    {
        var status = ScheduledStatuses.FirstOrDefault(s => s.Id == statusId);
        if (status != null)
        {
            ScheduledStatuses.Remove(status);
            SaveScheduledStatuses();
        }
    }

    public ScheduledStatus? GetActiveScheduledStatus()
    {
        var now = DateTime.Now;
        return ScheduledStatuses.FirstOrDefault(s =>
            s.IsActive &&
            (s.ActiveDays.Count == 0 || s.ActiveDays.Contains(now.DayOfWeek)) &&
            s.StartTime <= now &&
            (s.EndTime == null || s.EndTime > now));
    }

    private void SaveScheduledStatuses()
    {
        _settingsService.Settings.ScheduledStatuses = ScheduledStatuses.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Friend Notes

    public void SetFriendNote(FriendNote note)
    {
        var existing = FriendNotes.FirstOrDefault(n => n.UserId == note.UserId);
        if (existing != null)
        {
            var index = FriendNotes.IndexOf(existing);
            note.LastUpdated = DateTime.UtcNow;
            FriendNotes[index] = note;
        }
        else
        {
            FriendNotes.Add(note);
        }
        SaveFriendNotes();
    }

    public FriendNote? GetFriendNote(string userId)
    {
        return FriendNotes.FirstOrDefault(n => n.UserId == userId);
    }

    public void DeleteFriendNote(string userId)
    {
        var note = FriendNotes.FirstOrDefault(n => n.UserId == userId);
        if (note != null)
        {
            FriendNotes.Remove(note);
            SaveFriendNotes();
        }
    }

    public List<FriendNote> GetFriendsByTag(string tag)
    {
        return FriendNotes.Where(n =>
            n.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public List<FriendNote> GetUpcomingBirthdays(int days = 7)
    {
        var today = DateTime.Today;
        return FriendNotes
            .Where(n => n.Birthday.HasValue)
            .Where(n =>
            {
                var birthday = n.Birthday!.Value;
                var thisYearBirthday = new DateTime(today.Year, birthday.Month, birthday.Day);
                if (thisYearBirthday < today)
                    thisYearBirthday = thisYearBirthday.AddYears(1);
                return (thisYearBirthday - today).TotalDays <= days;
            })
            .OrderBy(n =>
            {
                var birthday = n.Birthday!.Value;
                var thisYearBirthday = new DateTime(today.Year, birthday.Month, birthday.Day);
                if (thisYearBirthday < today)
                    thisYearBirthday = thisYearBirthday.AddYears(1);
                return thisYearBirthday;
            })
            .ToList();
    }

    private void SaveFriendNotes()
    {
        _settingsService.Settings.FriendNotes = FriendNotes.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Smart DND

    public void AddDndSchedule(SmartDndSchedule schedule)
    {
        DndSchedules.Add(schedule);
        SaveDndSchedules();
    }

    public void RemoveDndSchedule(string scheduleId)
    {
        var schedule = DndSchedules.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule != null)
        {
            DndSchedules.Remove(schedule);
            SaveDndSchedules();
        }
    }

    public bool IsInDndPeriod()
    {
        return GetActiveDndSchedule() != null;
    }

    public SmartDndSchedule? GetActiveDndSchedule()
    {
        var now = DateTime.Now;
        var currentTime = now.TimeOfDay;

        return DndSchedules.FirstOrDefault(s =>
            (s.ActiveDays.Count == 0 || s.ActiveDays.Contains(now.DayOfWeek)) &&
            currentTime >= s.StartTime &&
            currentTime <= s.EndTime);
    }

    public bool ShouldNotify(string senderId)
    {
        var activeSchedule = GetActiveDndSchedule();
        if (activeSchedule == null)
            return true;

        // Check whitelist
        if (activeSchedule.WhitelistedUserIds.Contains(senderId))
            return true;

        return false;
    }

    private void SaveDndSchedules()
    {
        _settingsService.Settings.DndSchedules = DndSchedules.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Activity Insights

    public void TrackMessageSent()
    {
        var insight = GetOrCreateTodayInsight();
        insight.MessagesSent++;
        SaveActivityInsights();
    }

    public void TrackVoiceMinute()
    {
        var insight = GetOrCreateTodayInsight();
        insight.VoiceMinutes++;
        SaveActivityInsights();
    }

    public void TrackChannelActivity(string channelId)
    {
        var insight = GetOrCreateTodayInsight();
        if (insight.ChannelActivity.ContainsKey(channelId))
            insight.ChannelActivity[channelId]++;
        else
            insight.ChannelActivity[channelId] = 1;
        SaveActivityInsights();
    }

    public void TrackFriendInteraction(string friendId)
    {
        if (_todayInteractedFriends.Add(friendId))
        {
            var insight = GetOrCreateTodayInsight();
            insight.FriendsInteractedWith = _todayInteractedFriends.Count;
            SaveActivityInsights();
        }
    }

    public ActivityInsight GetTodayInsight()
    {
        return GetOrCreateTodayInsight();
    }

    public List<ActivityInsight> GetWeeklyInsights()
    {
        var weekAgo = DateTime.Today.AddDays(-7);
        return _settingsService.Settings.ActivityInsights
            .Where(i => i.Date >= weekAgo)
            .OrderByDescending(i => i.Date)
            .ToList();
    }

    private ActivityInsight GetOrCreateTodayInsight()
    {
        var today = DateTime.Today;
        var existing = _settingsService.Settings.ActivityInsights
            .FirstOrDefault(i => i.Date.Date == today);

        if (existing != null)
            return existing;

        var newInsight = new ActivityInsight { Date = today };
        _settingsService.Settings.ActivityInsights.Add(newInsight);

        // Clean up old insights (keep last 90 days)
        var cutoff = today.AddDays(-90);
        _settingsService.Settings.ActivityInsights.RemoveAll(i => i.Date < cutoff);

        return newInsight;
    }

    private void SaveActivityInsights()
    {
        _settingsService.SaveSettings();
    }

    #endregion

    #region Quick Actions

    public void AddQuickAction(QuickAction action)
    {
        QuickActions.Add(action);
        SaveQuickActions();
    }

    public void RemoveQuickAction(string actionId)
    {
        var action = QuickActions.FirstOrDefault(a => a.Id == actionId);
        if (action != null)
        {
            QuickActions.Remove(action);
            SaveQuickActions();
        }
    }

    public void ExecuteQuickAction(string actionId)
    {
        var action = QuickActions.FirstOrDefault(a => a.Id == actionId);
        if (action == null) return;

        // Action execution would be handled by the caller
        // This is just for tracking
        System.Diagnostics.Debug.WriteLine($"Executing quick action: {action.Name} ({action.ActionType}: {action.ActionValue})");
    }

    private void SaveQuickActions()
    {
        _settingsService.Settings.QuickActions = QuickActions.ToList();
        _settingsService.SaveSettings();
    }

    #endregion

    #region Auto-Away

    public void ResetActivityTimer()
    {
        _lastActivityTime = DateTime.Now;
        if (_isAutoAway)
        {
            _isAutoAway = false;
            OnAutoAwayReset?.Invoke();
        }
    }

    #endregion

    #region Friend Anniversaries

    public List<FriendAnniversary> GetUpcomingAnniversaries(int days = 7)
    {
        var today = DateTime.Today;
        var anniversaries = new List<FriendAnniversary>();

        // Check friendship anniversaries
        foreach (var kvp in _friendshipDates)
        {
            var friendshipDate = kvp.Value;
            var years = today.Year - friendshipDate.Year;
            var thisYearAnniversary = new DateTime(today.Year, friendshipDate.Month, friendshipDate.Day);

            if (thisYearAnniversary < today)
            {
                thisYearAnniversary = thisYearAnniversary.AddYears(1);
                years++;
            }

            if ((thisYearAnniversary - today).TotalDays <= days)
            {
                // Try to get friend info from notes
                var note = FriendNotes.FirstOrDefault(n => n.UserId == kvp.Key);
                anniversaries.Add(new FriendAnniversary
                {
                    FriendId = kvp.Key,
                    FriendUsername = note?.Username ?? "Friend",
                    FriendAvatarUrl = note?.AvatarUrl,
                    Date = thisYearAnniversary,
                    Type = AnniversaryType.Friendship,
                    Years = years
                });
            }
        }

        // Check birthdays from friend notes
        foreach (var note in FriendNotes.Where(n => n.Birthday.HasValue))
        {
            var birthday = note.Birthday!.Value;
            var thisYearBirthday = new DateTime(today.Year, birthday.Month, birthday.Day);

            if (thisYearBirthday < today)
                thisYearBirthday = thisYearBirthday.AddYears(1);

            if ((thisYearBirthday - today).TotalDays <= days)
            {
                // Don't duplicate if already added as friendship anniversary
                if (!anniversaries.Any(a => a.FriendId == note.UserId && a.Type == AnniversaryType.Birthday))
                {
                    anniversaries.Add(new FriendAnniversary
                    {
                        FriendId = note.UserId,
                        FriendUsername = note.Username ?? "Friend",
                        FriendAvatarUrl = note.AvatarUrl,
                        Date = thisYearBirthday,
                        Type = AnniversaryType.Birthday,
                        Years = 0
                    });
                }
            }
        }

        return anniversaries.OrderBy(a => a.Date).ToList();
    }

    public void AddFriendshipAnniversary(string friendId, string friendUsername, DateTime friendshipDate)
    {
        _friendshipDates[friendId] = friendshipDate;

        // Also update friend note if exists
        var note = FriendNotes.FirstOrDefault(n => n.UserId == friendId);
        if (note != null)
        {
            note.FriendshipDate = friendshipDate;
            SaveFriendNotes();
        }

        SaveFriendshipDates();
    }

    private void SaveFriendshipDates()
    {
        _settingsService.Settings.FriendshipDates = _friendshipDates.ToDictionary(k => k.Key, v => v.Value);
        _settingsService.SaveSettings();
    }

    private void LoadFriendshipDates()
    {
        _friendshipDates.Clear();
        if (_settingsService.Settings.FriendshipDates != null)
        {
            foreach (var kvp in _settingsService.Settings.FriendshipDates)
            {
                _friendshipDates[kvp.Key] = kvp.Value;
            }
        }
    }

    #endregion

    #region Online Notifications

    public void SetOnlineNotification(string friendId, bool enabled, bool soundEnabled = true)
    {
        var existing = OnlineNotifications.FirstOrDefault(n => n.FriendId == friendId);
        if (existing != null)
        {
            existing.Enabled = enabled;
            existing.SoundEnabled = soundEnabled;
        }
        else
        {
            // Try to get friend info from notes
            var note = FriendNotes.FirstOrDefault(n => n.UserId == friendId);
            OnlineNotifications.Add(new OnlineNotificationPreference
            {
                FriendId = friendId,
                FriendUsername = note?.Username ?? "Friend",
                FriendAvatarUrl = note?.AvatarUrl,
                Enabled = enabled,
                SoundEnabled = soundEnabled
            });
        }
        SaveOnlineNotifications();
    }

    public bool ShouldNotifyOnline(string friendId)
    {
        var pref = OnlineNotifications.FirstOrDefault(n => n.FriendId == friendId);
        return pref?.Enabled ?? false;
    }

    public void RemoveOnlineNotification(string friendId)
    {
        var pref = OnlineNotifications.FirstOrDefault(n => n.FriendId == friendId);
        if (pref != null)
        {
            OnlineNotifications.Remove(pref);
            SaveOnlineNotifications();
        }
    }

    /// <summary>
    /// Called when a friend comes online to trigger notification if enabled
    /// </summary>
    public void NotifyFriendOnline(string friendId, string friendUsername)
    {
        if (ShouldNotifyOnline(friendId))
        {
            OnFriendOnlineNotification?.Invoke(friendId, friendUsername);
        }
    }

    private void SaveOnlineNotifications()
    {
        _settingsService.Settings.OnlineNotifications = OnlineNotifications.ToList();
        _settingsService.SaveSettings();
    }

    private void LoadOnlineNotifications()
    {
        OnlineNotifications.Clear();
        if (_settingsService.Settings.OnlineNotifications != null)
        {
            foreach (var pref in _settingsService.Settings.OnlineNotifications)
            {
                OnlineNotifications.Add(pref);
            }
        }
    }

    #endregion

    public void Dispose()
    {
        _schedulerTimer.Stop();
        _schedulerTimer.Tick -= SchedulerTimer_Tick;

        _autoAwayTimer.Stop();
        _autoAwayTimer.Tick -= AutoAwayTimer_Tick;

        GC.SuppressFinalize(this);
    }
}
