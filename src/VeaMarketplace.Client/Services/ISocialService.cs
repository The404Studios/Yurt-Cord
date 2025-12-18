using System.Collections.ObjectModel;
using System.Text.Json;
using VeaMarketplace.Client.Models;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Interface for extended social features beyond basic friend management
/// </summary>
public interface ISocialService
{
    // Friend Groups
    ObservableCollection<FriendGroup> FriendGroups { get; }
    Task<FriendGroup> CreateFriendGroupAsync(string name, string? emoji = null, string? color = null);
    Task UpdateFriendGroupAsync(FriendGroup group);
    Task DeleteFriendGroupAsync(string groupId);
    Task AddFriendToGroupAsync(string groupId, string friendId);
    Task RemoveFriendFromGroupAsync(string groupId, string friendId);
    Task MoveFriendBetweenGroupsAsync(string friendId, string? fromGroupId, string toGroupId);
    FriendGroup? GetFriendGroup(string friendId);
    List<FriendDto> GetFriendsInGroup(string groupId, IEnumerable<FriendDto> allFriends);

    // Activity History & Interaction Tracking
    ObservableCollection<InteractionEvent> RecentInteractions { get; }
    Task<FriendInteractionHistory> GetInteractionHistoryAsync(string friendId);
    Task RecordInteractionAsync(string friendId, InteractionType type, string? details = null);
    Task<List<FriendInteractionSummary>> GetTopFriendsByInteractionAsync(int count = 5);
    Task<FriendshipStats> GetFriendshipStatsAsync();

    // Rich Presence
    RichPresence? CurrentPresence { get; }
    CustomActivity? CurrentActivity { get; }
    ObservableCollection<RichPresence> FriendPresences { get; }
    Task SetPresenceAsync(RichPresence presence);
    Task ClearPresenceAsync();
    Task SetCustomActivityAsync(string emoji, string text, TimeSpan? duration = null);
    Task ClearCustomActivityAsync();
    RichPresence? GetFriendPresence(string friendId);

    // Message Reactions
    Task AddReactionAsync(string messageId, string emoji, bool isCustom = false, string? customUrl = null);
    Task RemoveReactionAsync(string messageId, string emoji);
    Task<List<MessageReaction>> GetMessageReactionsAsync(string messageId);

    // Pinned Messages
    ObservableCollection<PinnedMessage> PinnedMessages { get; }
    Task PinMessageAsync(string conversationId, string messageId, string? note = null);
    Task UnpinMessageAsync(string pinnedMessageId);
    Task<List<PinnedMessage>> GetPinnedMessagesAsync(string conversationId);

    // Friend Recommendations
    ObservableCollection<FriendRecommendation> Recommendations { get; }
    Task RefreshRecommendationsAsync();
    Task DismissRecommendationAsync(string userId);
    Task<List<MutualConnection>> GetMutualFriendsAsync(string userId);

    // Events
    event Action<FriendGroup>? OnFriendGroupCreated;
    event Action<FriendGroup>? OnFriendGroupUpdated;
    event Action<string>? OnFriendGroupDeleted;
    event Action<InteractionEvent>? OnNewInteraction;
    event Action<RichPresence>? OnPresenceUpdated;
    event Action<string, RichPresence>? OnFriendPresenceUpdated;
    event Action<string, MessageReaction>? OnReactionAdded;
    event Action<string, string>? OnReactionRemoved;
    event Action<PinnedMessage>? OnMessagePinned;
    event Action<string>? OnMessageUnpinned;
    event Action<FriendRecommendation>? OnNewRecommendation;

    // Persistence
    Task LoadAsync();
    Task SaveAsync();
}

/// <summary>
/// Implementation of extended social features
/// </summary>
public class SocialService : ISocialService
{
    private readonly ISettingsService _settingsService;
    private readonly IFriendService _friendService;
    private readonly string _dataPath;
    private SocialData _data = new();

    // Collections
    public ObservableCollection<FriendGroup> FriendGroups { get; } = new();
    public ObservableCollection<InteractionEvent> RecentInteractions { get; } = new();
    public ObservableCollection<RichPresence> FriendPresences { get; } = new();
    public ObservableCollection<PinnedMessage> PinnedMessages { get; } = new();
    public ObservableCollection<FriendRecommendation> Recommendations { get; } = new();

    // Current state
    public RichPresence? CurrentPresence { get; private set; }
    public CustomActivity? CurrentActivity { get; private set; }

    // Events
    public event Action<FriendGroup>? OnFriendGroupCreated;
    public event Action<FriendGroup>? OnFriendGroupUpdated;
    public event Action<string>? OnFriendGroupDeleted;
    public event Action<InteractionEvent>? OnNewInteraction;
    public event Action<RichPresence>? OnPresenceUpdated;
    public event Action<string, RichPresence>? OnFriendPresenceUpdated;
    public event Action<string, MessageReaction>? OnReactionAdded;
    public event Action<string, string>? OnReactionRemoved;
    public event Action<PinnedMessage>? OnMessagePinned;
    public event Action<string>? OnMessageUnpinned;
    public event Action<FriendRecommendation>? OnNewRecommendation;

    public SocialService(ISettingsService settingsService, IFriendService friendService)
    {
        _settingsService = settingsService;
        _friendService = friendService;
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VeaMarketplace", "social_data.json");

        // Subscribe to friend events for automatic interaction tracking
        _friendService.OnDirectMessageReceived += OnDirectMessageReceived;
        _friendService.OnFriendOnline += OnFriendOnline;

        // Initialize default groups
        InitializeDefaultGroups();
    }

    private void InitializeDefaultGroups()
    {
        // Add default groups if none exist
        if (FriendGroups.Count == 0)
        {
            FriendGroups.Add(new FriendGroup { Name = "Favorites", Emoji = "⭐", Color = "#FFD700", SortOrder = 0 });
            FriendGroups.Add(new FriendGroup { Name = "Gaming", Emoji = "🎮", Color = "#5865F2", SortOrder = 1 });
            FriendGroups.Add(new FriendGroup { Name = "Work", Emoji = "💼", Color = "#43B581", SortOrder = 2 });
            FriendGroups.Add(new FriendGroup { Name = "Family", Emoji = "👨‍👩‍👧‍👦", Color = "#ED4245", SortOrder = 3 });
        }
    }

    private void OnDirectMessageReceived(DirectMessageDto message)
    {
        // Auto-track message interactions
        _ = RecordInteractionAsync(message.SenderId, InteractionType.MessageReceived);
    }

    private void OnFriendOnline(FriendDto friend)
    {
        // Could trigger presence sync here
    }

    #region Friend Groups

    public Task<FriendGroup> CreateFriendGroupAsync(string name, string? emoji = null, string? color = null)
    {
        var group = new FriendGroup
        {
            Name = name,
            Emoji = emoji,
            Color = color ?? "#5865F2",
            SortOrder = FriendGroups.Count
        };

        FriendGroups.Add(group);
        _data.FriendGroups.Add(group);
        OnFriendGroupCreated?.Invoke(group);
        _ = SaveAsync();

        return Task.FromResult(group);
    }

    public Task UpdateFriendGroupAsync(FriendGroup group)
    {
        var existing = _data.FriendGroups.FirstOrDefault(g => g.Id == group.Id);
        if (existing != null)
        {
            var index = _data.FriendGroups.IndexOf(existing);
            _data.FriendGroups[index] = group;

            var obsIndex = FriendGroups.IndexOf(FriendGroups.FirstOrDefault(g => g.Id == group.Id)!);
            if (obsIndex >= 0)
                FriendGroups[obsIndex] = group;

            OnFriendGroupUpdated?.Invoke(group);
            _ = SaveAsync();
        }
        return Task.CompletedTask;
    }

    public Task DeleteFriendGroupAsync(string groupId)
    {
        var group = _data.FriendGroups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            _data.FriendGroups.Remove(group);
            var obsGroup = FriendGroups.FirstOrDefault(g => g.Id == groupId);
            if (obsGroup != null)
                FriendGroups.Remove(obsGroup);

            OnFriendGroupDeleted?.Invoke(groupId);
            _ = SaveAsync();
        }
        return Task.CompletedTask;
    }

    public Task AddFriendToGroupAsync(string groupId, string friendId)
    {
        var group = _data.FriendGroups.FirstOrDefault(g => g.Id == groupId);
        if (group != null && !group.MemberIds.Contains(friendId))
        {
            // Remove from other groups first (friend can only be in one group)
            foreach (var g in _data.FriendGroups)
            {
                g.MemberIds.Remove(friendId);
            }

            group.MemberIds.Add(friendId);
            OnFriendGroupUpdated?.Invoke(group);
            _ = SaveAsync();
        }
        return Task.CompletedTask;
    }

    public Task RemoveFriendFromGroupAsync(string groupId, string friendId)
    {
        var group = _data.FriendGroups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            group.MemberIds.Remove(friendId);
            OnFriendGroupUpdated?.Invoke(group);
            _ = SaveAsync();
        }
        return Task.CompletedTask;
    }

    public Task MoveFriendBetweenGroupsAsync(string friendId, string? fromGroupId, string toGroupId)
    {
        if (fromGroupId != null)
        {
            var fromGroup = _data.FriendGroups.FirstOrDefault(g => g.Id == fromGroupId);
            fromGroup?.MemberIds.Remove(friendId);
        }

        var toGroup = _data.FriendGroups.FirstOrDefault(g => g.Id == toGroupId);
        if (toGroup != null && !toGroup.MemberIds.Contains(friendId))
        {
            toGroup.MemberIds.Add(friendId);
        }

        _ = SaveAsync();
        return Task.CompletedTask;
    }

    public FriendGroup? GetFriendGroup(string friendId)
    {
        return _data.FriendGroups.FirstOrDefault(g => g.MemberIds.Contains(friendId));
    }

    public List<FriendDto> GetFriendsInGroup(string groupId, IEnumerable<FriendDto> allFriends)
    {
        var group = _data.FriendGroups.FirstOrDefault(g => g.Id == groupId);
        if (group == null) return new List<FriendDto>();

        return allFriends.Where(f => group.MemberIds.Contains(f.UserId)).ToList();
    }

    #endregion

    #region Activity History & Interaction Tracking

    public Task<FriendInteractionHistory> GetInteractionHistoryAsync(string friendId)
    {
        var history = _data.InteractionHistories.FirstOrDefault(h => h.FriendId == friendId);
        if (history == null)
        {
            history = new FriendInteractionHistory { FriendId = friendId };
            _data.InteractionHistories.Add(history);
        }
        return Task.FromResult(history);
    }

    public async Task RecordInteractionAsync(string friendId, InteractionType type, string? details = null)
    {
        var history = await GetInteractionHistoryAsync(friendId);
        var evt = new InteractionEvent
        {
            FriendId = friendId,
            Type = type,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        history.RecentInteractions.Insert(0, evt);

        // Keep only last 100 interactions per friend
        while (history.RecentInteractions.Count > 100)
            history.RecentInteractions.RemoveAt(history.RecentInteractions.Count - 1);

        // Update counters
        switch (type)
        {
            case InteractionType.MessageSent:
                history.TotalMessagesSent++;
                history.LastMessageSent = DateTime.UtcNow;
                break;
            case InteractionType.MessageReceived:
                history.TotalMessagesReceived++;
                history.LastMessageReceived = DateTime.UtcNow;
                break;
            case InteractionType.VoiceCallStarted:
            case InteractionType.VoiceCallEnded:
                history.LastVoiceCall = DateTime.UtcNow;
                if (type == InteractionType.VoiceCallEnded && int.TryParse(details, out var minutes))
                    history.TotalVoiceMinutes += minutes;
                break;
            case InteractionType.GameSessionStarted:
            case InteractionType.GameSessionEnded:
                history.LastGameTogether = DateTime.UtcNow;
                if (type == InteractionType.GameSessionEnded)
                    history.TotalGameSessions++;
                break;
        }

        // Add to recent interactions
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            RecentInteractions.Insert(0, evt);
            while (RecentInteractions.Count > 50)
                RecentInteractions.RemoveAt(RecentInteractions.Count - 1);
        });

        OnNewInteraction?.Invoke(evt);
        await SaveAsync();
    }

    public Task<List<FriendInteractionSummary>> GetTopFriendsByInteractionAsync(int count = 5)
    {
        var summaries = _data.InteractionHistories
            .Select(h => new FriendInteractionSummary
            {
                FriendId = h.FriendId,
                FriendUsername = h.FriendUsername,
                TotalInteractions = h.TotalMessagesSent + h.TotalMessagesReceived + h.TotalGameSessions,
                LastInteraction = new[] { h.LastMessageSent, h.LastMessageReceived, h.LastVoiceCall, h.LastGameTogether }.Max()
            })
            .OrderByDescending(s => s.TotalInteractions)
            .Take(count)
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<FriendshipStats> GetFriendshipStatsAsync()
    {
        var stats = new FriendshipStats
        {
            TotalFriends = _friendService.Friends.Count,
            OnlineFriends = _friendService.Friends.Count(f => f.IsOnline),
            TotalConversations = _friendService.Conversations.Count,
            ActiveConversationsToday = _friendService.Conversations.Count(c =>
                c.LastMessageAt.HasValue && c.LastMessageAt.Value.Date == DateTime.UtcNow.Date),
            TotalMessagesThisWeek = _data.InteractionHistories
                .Sum(h => h.RecentInteractions
                    .Count(i => i.Timestamp > DateTime.UtcNow.AddDays(-7) &&
                               (i.Type == InteractionType.MessageSent || i.Type == InteractionType.MessageReceived))),
            TotalVoiceMinutesThisWeek = _data.InteractionHistories.Sum(h => h.TotalVoiceMinutes)
        };

        // Calculate streaks
        var activeDays = _data.InteractionHistories
            .SelectMany(h => h.RecentInteractions)
            .Select(i => i.Timestamp.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (activeDays.Count > 0)
        {
            stats.LastActiveDay = activeDays.First();
            stats.CurrentDailyStreak = CalculateStreak(activeDays);
        }

        return Task.FromResult(stats);
    }

    private int CalculateStreak(List<DateTime> activeDays)
    {
        if (activeDays.Count == 0) return 0;

        var streak = 0;
        var today = DateTime.UtcNow.Date;
        var checkDate = today;

        foreach (var day in activeDays)
        {
            if (day.Date == checkDate || day.Date == checkDate.AddDays(-1))
            {
                streak++;
                checkDate = day.Date;
            }
            else if (day.Date < checkDate.AddDays(-1))
            {
                break;
            }
        }

        return streak;
    }

    #endregion

    #region Rich Presence

    public Task SetPresenceAsync(RichPresence presence)
    {
        CurrentPresence = presence;
        OnPresenceUpdated?.Invoke(presence);
        return Task.CompletedTask;
    }

    public Task ClearPresenceAsync()
    {
        CurrentPresence = null;
        OnPresenceUpdated?.Invoke(null!);
        return Task.CompletedTask;
    }

    public Task SetCustomActivityAsync(string emoji, string text, TimeSpan? duration = null)
    {
        CurrentActivity = new CustomActivity
        {
            Emoji = emoji,
            Text = text,
            ExpiresAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null
        };
        return SaveAsync();
    }

    public Task ClearCustomActivityAsync()
    {
        CurrentActivity = null;
        return SaveAsync();
    }

    public RichPresence? GetFriendPresence(string friendId)
    {
        return FriendPresences.FirstOrDefault(p => p.UserId == friendId);
    }

    #endregion

    #region Message Reactions

    public Task AddReactionAsync(string messageId, string emoji, bool isCustom = false, string? customUrl = null)
    {
        var reaction = new MessageReaction
        {
            MessageId = messageId,
            Emoji = emoji,
            IsCustomEmoji = isCustom,
            CustomEmojiUrl = customUrl,
            UserId = "current_user", // Would be replaced with actual user ID
            Username = "CurrentUser"
        };

        _data.MessageReactions.Add(reaction);
        OnReactionAdded?.Invoke(messageId, reaction);
        return SaveAsync();
    }

    public Task RemoveReactionAsync(string messageId, string emoji)
    {
        var reaction = _data.MessageReactions
            .FirstOrDefault(r => r.MessageId == messageId && r.Emoji == emoji && r.UserId == "current_user");

        if (reaction != null)
        {
            _data.MessageReactions.Remove(reaction);
            OnReactionRemoved?.Invoke(messageId, emoji);
        }

        return SaveAsync();
    }

    public Task<List<MessageReaction>> GetMessageReactionsAsync(string messageId)
    {
        return Task.FromResult(_data.MessageReactions.Where(r => r.MessageId == messageId).ToList());
    }

    #endregion

    #region Pinned Messages

    public Task PinMessageAsync(string conversationId, string messageId, string? note = null)
    {
        var pin = new PinnedMessage
        {
            ConversationId = conversationId,
            MessageId = messageId,
            Note = note,
            PinnedByUserId = "current_user",
            PinnedByUsername = "CurrentUser"
        };

        _data.PinnedMessages.Add(pin);

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            PinnedMessages.Add(pin);
        });

        OnMessagePinned?.Invoke(pin);
        return SaveAsync();
    }

    public Task UnpinMessageAsync(string pinnedMessageId)
    {
        var pin = _data.PinnedMessages.FirstOrDefault(p => p.Id == pinnedMessageId);
        if (pin != null)
        {
            _data.PinnedMessages.Remove(pin);

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var obsPin = PinnedMessages.FirstOrDefault(p => p.Id == pinnedMessageId);
                if (obsPin != null)
                    PinnedMessages.Remove(obsPin);
            });

            OnMessageUnpinned?.Invoke(pinnedMessageId);
        }
        return SaveAsync();
    }

    public Task<List<PinnedMessage>> GetPinnedMessagesAsync(string conversationId)
    {
        return Task.FromResult(_data.PinnedMessages
            .Where(p => p.ConversationId == conversationId)
            .OrderByDescending(p => p.PinnedAt)
            .ToList());
    }

    #endregion

    #region Friend Recommendations

    public async Task RefreshRecommendationsAsync()
    {
        // Clear existing recommendations
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Recommendations.Clear());
        _data.Recommendations.Clear();

        // Get mutual friends for all friends
        var friends = _friendService.Friends.ToList();
        var potentialRecommendations = new Dictionary<string, FriendRecommendation>();

        foreach (var friend in friends)
        {
            try
            {
                var mutuals = await _friendService.GetMutualFriendsAsync(friend.UserId);
                foreach (var mutual in mutuals)
                {
                    // Skip if already a friend or already recommended
                    if (friends.Any(f => f.UserId == mutual.Id)) continue;
                    if (potentialRecommendations.ContainsKey(mutual.Id))
                    {
                        // Add to mutual friends count
                        potentialRecommendations[mutual.Id].MutualFriends.Add(new MutualConnection
                        {
                            UserId = friend.UserId,
                            Username = friend.Username,
                            AvatarUrl = friend.AvatarUrl
                        });
                        continue;
                    }

                    potentialRecommendations[mutual.Id] = new FriendRecommendation
                    {
                        UserId = mutual.Id,
                        Username = mutual.Username,
                        DisplayName = mutual.DisplayName,
                        AvatarUrl = mutual.AvatarUrl,
                        Reason = RecommendationReason.MutualFriends,
                        MutualFriends = new List<MutualConnection>
                        {
                            new()
                            {
                                UserId = friend.UserId,
                                Username = friend.Username,
                                AvatarUrl = friend.AvatarUrl
                            }
                        }
                    };
                }
            }
            catch
            {
                // Continue even if one friend's mutuals fail to load
            }
        }

        // Score and sort recommendations
        foreach (var rec in potentialRecommendations.Values)
        {
            rec.RecommendationScore = rec.MutualFriends.Count * 10 + rec.SharedServers.Count * 5;
        }

        var topRecommendations = potentialRecommendations.Values
            .Where(r => !r.IsDismissed)
            .OrderByDescending(r => r.RecommendationScore)
            .Take(10)
            .ToList();

        _data.Recommendations.AddRange(topRecommendations);

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            foreach (var rec in topRecommendations)
            {
                Recommendations.Add(rec);
                OnNewRecommendation?.Invoke(rec);
            }
        });

        await SaveAsync();
    }

    public Task DismissRecommendationAsync(string userId)
    {
        var rec = _data.Recommendations.FirstOrDefault(r => r.UserId == userId);
        if (rec != null)
        {
            rec.IsDismissed = true;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var obsRec = Recommendations.FirstOrDefault(r => r.UserId == userId);
                if (obsRec != null)
                    Recommendations.Remove(obsRec);
            });
        }
        return SaveAsync();
    }

    public async Task<List<MutualConnection>> GetMutualFriendsAsync(string userId)
    {
        var mutuals = await _friendService.GetMutualFriendsAsync(userId);
        return mutuals.Select(m => new MutualConnection
        {
            UserId = m.Id,
            Username = m.Username,
            AvatarUrl = m.AvatarUrl
        }).ToList();
    }

    #endregion

    #region Persistence

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = await File.ReadAllTextAsync(_dataPath);
                _data = JsonSerializer.Deserialize<SocialData>(json) ?? new SocialData();

                // Load into observable collections
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    FriendGroups.Clear();
                    foreach (var group in _data.FriendGroups)
                        FriendGroups.Add(group);

                    PinnedMessages.Clear();
                    foreach (var pin in _data.PinnedMessages)
                        PinnedMessages.Add(pin);

                    Recommendations.Clear();
                    foreach (var rec in _data.Recommendations.Where(r => !r.IsDismissed))
                        Recommendations.Add(rec);
                });

                // Restore custom activity if not expired
                if (_data.CurrentActivity != null && !_data.CurrentActivity.IsExpired)
                    CurrentActivity = _data.CurrentActivity;
            }
            else
            {
                // Initialize with defaults
                _data = new SocialData();
                InitializeDefaultGroups();
                _data.FriendGroups.AddRange(FriendGroups);
            }
        }
        catch
        {
            // On error, start fresh
            _data = new SocialData();
            InitializeDefaultGroups();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _data.CurrentActivity = CurrentActivity;
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch
        {
            // Silently fail saves - not critical
        }
    }

    #endregion
}

/// <summary>
/// Data model for persisting social service state
/// </summary>
public class SocialData
{
    public List<FriendGroup> FriendGroups { get; set; } = new();
    public List<FriendInteractionHistory> InteractionHistories { get; set; } = new();
    public List<MessageReaction> MessageReactions { get; set; } = new();
    public List<PinnedMessage> PinnedMessages { get; set; } = new();
    public List<FriendRecommendation> Recommendations { get; set; } = new();
    public CustomActivity? CurrentActivity { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
