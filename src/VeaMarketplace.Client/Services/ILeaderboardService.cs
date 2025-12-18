using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using VeaMarketplace.Client.Models;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for managing leaderboards and user rankings
/// </summary>
public interface ILeaderboardService
{
    // Leaderboard Data
    ObservableCollection<LeaderboardEntry> TopSellers { get; }
    ObservableCollection<LeaderboardEntry> TopMessagers { get; }
    ObservableCollection<LeaderboardEntry> TopBuyers { get; }
    ObservableCollection<LeaderboardEntry> MostActiveUsers { get; }
    ObservableCollection<LeaderboardEntry> TopRatedSellers { get; }
    ObservableCollection<LeaderboardEntry> WeeklyStars { get; }

    // Current user stats
    UserStats? CurrentUserStats { get; }

    // Profile Posts
    ObservableCollection<ProfilePost> CurrentProfilePosts { get; }

    // Events
    event Action<LeaderboardEntry>? OnLeaderboardUpdated;
    event Action<UserStats>? OnStatsUpdated;
    event Action<ProfilePost>? OnNewProfilePost;
    event Action<string>? OnProfilePostDeleted;
    event Action<ProfilePost>? OnProfilePostLiked;

    // Leaderboard operations
    Task RefreshLeaderboardsAsync();
    Task<List<LeaderboardEntry>> GetTopSellersAsync(int count = 10);
    Task<List<LeaderboardEntry>> GetTopMessagersAsync(int count = 10);
    Task<List<LeaderboardEntry>> GetTopBuyersAsync(int count = 10);
    Task<List<LeaderboardEntry>> GetMostActiveUsersAsync(int count = 10);
    Task<List<LeaderboardEntry>> GetTopRatedSellersAsync(int count = 10);
    Task<List<LeaderboardEntry>> GetWeeklyStarsAsync(int count = 10);

    // User stats
    Task<UserStats> GetUserStatsAsync(string userId);
    Task IncrementStatAsync(StatType type, int amount = 1);

    // Profile posts
    Task<List<ProfilePost>> GetProfilePostsAsync(string userId);
    Task CreateProfilePostAsync(string userId, string content, List<string>? attachmentUrls = null);
    Task DeleteProfilePostAsync(string postId);
    Task LikeProfilePostAsync(string postId);
    Task UnlikeProfilePostAsync(string postId);
    Task<ProfilePost> GetProfilePostAsync(string postId);

    // Persistence
    Task LoadAsync();
    Task SaveAsync();
}

#region Leaderboard Models

/// <summary>
/// A single entry in a leaderboard
/// </summary>
public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int Rank { get; set; }
    public long Score { get; set; }
    public string ScoreDisplay { get; set; } = string.Empty;
    public LeaderboardType Type { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int RankChange { get; set; } // Positive = moved up, negative = moved down
    public bool IsCurrentUser { get; set; }

    // Badges/achievements
    public List<string> Badges { get; set; } = new();
    public bool IsVerified { get; set; }
    public bool IsTopContributor { get; set; }

    // UI helpers
    public string RankDisplay => Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"#{Rank}"
    };

    public string RankChangeDisplay => RankChange switch
    {
        > 0 => $"↑{RankChange}",
        < 0 => $"↓{Math.Abs(RankChange)}",
        _ => "—"
    };

    public string RankChangeColor => RankChange switch
    {
        > 0 => "#43B581",
        < 0 => "#F04747",
        _ => "#96989D"
    };
}

/// <summary>
/// Types of leaderboards available
/// </summary>
public enum LeaderboardType
{
    TopSellers,
    TopMessagers,
    TopBuyers,
    MostActive,
    TopRatedSellers,
    WeeklyStars,
    MonthlyStars,
    AllTimeLegends
}

/// <summary>
/// Comprehensive user statistics
/// </summary>
public class UserStats
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    // Marketplace stats
    public int TotalItemsSold { get; set; }
    public int TotalItemsBought { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalSpent { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int PositiveReviews { get; set; }
    public int NeutralReviews { get; set; }
    public int NegativeReviews { get; set; }

    // Social stats
    public int TotalMessagesSent { get; set; }
    public int TotalMessagesReceived { get; set; }
    public int TotalVoiceMinutes { get; set; }
    public int TotalFriends { get; set; }
    public int TotalFollowers { get; set; }
    public int TotalFollowing { get; set; }

    // Activity stats
    public int TotalProfileViews { get; set; }
    public int TotalPostsCreated { get; set; }
    public int TotalLikesReceived { get; set; }
    public int TotalLikesGiven { get; set; }
    public int LoginStreak { get; set; }
    public int LongestLoginStreak { get; set; }
    public DateTime LastActive { get; set; }
    public DateTime JoinedAt { get; set; }

    // Achievements
    public List<Achievement> Achievements { get; set; } = new();
    public int TotalAchievements { get; set; }
    public int TotalAchievementPoints { get; set; }

    // Weekly/Monthly stats
    public int WeeklySales { get; set; }
    public int WeeklyPurchases { get; set; }
    public int WeeklyMessages { get; set; }
    public int MonthlySales { get; set; }
    public int MonthlyPurchases { get; set; }
    public int MonthlyMessages { get; set; }

    // Computed properties
    public double PositiveRatingPercentage => TotalReviews > 0
        ? (double)PositiveReviews / TotalReviews * 100
        : 0;

    public string ReputationLevel => PositiveRatingPercentage switch
    {
        >= 98 => "Legendary",
        >= 95 => "Excellent",
        >= 90 => "Great",
        >= 80 => "Good",
        >= 70 => "Average",
        _ => "New"
    };

    public string ReputationColor => ReputationLevel switch
    {
        "Legendary" => "#FFD700",
        "Excellent" => "#43B581",
        "Great" => "#5865F2",
        "Good" => "#7289DA",
        "Average" => "#96989D",
        _ => "#FFFFFF"
    };

    public string AccountAge
    {
        get
        {
            var age = DateTime.UtcNow - JoinedAt;
            if (age.TotalDays < 30) return $"{(int)age.TotalDays} days";
            if (age.TotalDays < 365) return $"{(int)(age.TotalDays / 30)} months";
            var years = (int)(age.TotalDays / 365);
            var months = (int)((age.TotalDays % 365) / 30);
            return months > 0 ? $"{years}y {months}m" : $"{years} years";
        }
    }
}

/// <summary>
/// Type of statistic to increment
/// </summary>
public enum StatType
{
    MessagesSent,
    MessagesReceived,
    ItemsSold,
    ItemsBought,
    OrdersCompleted,
    VoiceMinutes,
    ProfileViews,
    PostsCreated,
    LikesGiven,
    LikesReceived
}

/// <summary>
/// User achievement/badge
/// </summary>
public class Achievement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🏆";
    public string? IconUrl { get; set; }
    public AchievementRarity Rarity { get; set; }
    public int Points { get; set; }
    public DateTime UnlockedAt { get; set; }
    public bool IsHidden { get; set; }
    public double Progress { get; set; }
    public int ProgressTarget { get; set; }

    public string RarityColor => Rarity switch
    {
        AchievementRarity.Common => "#96989D",
        AchievementRarity.Uncommon => "#43B581",
        AchievementRarity.Rare => "#5865F2",
        AchievementRarity.Epic => "#9B59B6",
        AchievementRarity.Legendary => "#FFD700",
        _ => "#FFFFFF"
    };

    public bool IsComplete => Progress >= ProgressTarget;
}

/// <summary>
/// Achievement rarity levels
/// </summary>
public enum AchievementRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

#endregion

#region Profile Posts

/// <summary>
/// A post on a user's profile (like a Facebook wall post)
/// </summary>
public class ProfilePost
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProfileUserId { get; set; } = string.Empty; // Whose profile this is on
    public string AuthorId { get; set; } = string.Empty; // Who wrote the post
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> AttachmentUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }

    // Engagement
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public List<string> LikedByUserIds { get; set; } = new();
    public List<ProfilePostComment> Comments { get; set; } = new();

    // UI helpers
    public bool IsEdited => EditedAt.HasValue;
    public bool HasAttachments => AttachmentUrls.Count > 0;
    public bool IsOwnPost => AuthorId == ProfileUserId;

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return CreatedAt.ToString("MMM d, yyyy");
        }
    }
}

/// <summary>
/// A comment on a profile post
/// </summary>
public class ProfilePostComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PostId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LikeCount { get; set; }
    public List<string> LikedByUserIds { get; set; } = new();

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
            return $"{(int)span.TotalDays}d";
        }
    }
}

#endregion

/// <summary>
/// Implementation of the leaderboard service
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    private readonly IApiService _apiService;
    private readonly IFriendService _friendService;
    private readonly string _dataPath;
    private LeaderboardData _data = new();

    public ObservableCollection<LeaderboardEntry> TopSellers { get; } = new();
    public ObservableCollection<LeaderboardEntry> TopMessagers { get; } = new();
    public ObservableCollection<LeaderboardEntry> TopBuyers { get; } = new();
    public ObservableCollection<LeaderboardEntry> MostActiveUsers { get; } = new();
    public ObservableCollection<LeaderboardEntry> TopRatedSellers { get; } = new();
    public ObservableCollection<LeaderboardEntry> WeeklyStars { get; } = new();
    public ObservableCollection<ProfilePost> CurrentProfilePosts { get; } = new();

    public UserStats? CurrentUserStats { get; private set; }

    public event Action<LeaderboardEntry>? OnLeaderboardUpdated;
    public event Action<UserStats>? OnStatsUpdated;
    public event Action<ProfilePost>? OnNewProfilePost;
    public event Action<string>? OnProfilePostDeleted;
    public event Action<ProfilePost>? OnProfilePostLiked;

    public LeaderboardService(IApiService apiService, IFriendService friendService)
    {
        _apiService = apiService;
        _friendService = friendService;
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VeaMarketplace", "leaderboard_data.json");
    }

    #region Leaderboard Operations

    public async Task RefreshLeaderboardsAsync()
    {
        await Task.WhenAll(
            GetTopSellersAsync(),
            GetTopMessagersAsync(),
            GetTopBuyersAsync(),
            GetMostActiveUsersAsync(),
            GetTopRatedSellersAsync(),
            GetWeeklyStarsAsync()
        );
    }

    public Task<List<LeaderboardEntry>> GetTopSellersAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .OrderByDescending(s => s.TotalItemsSold)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = s.TotalItemsSold,
                ScoreDisplay = $"{s.TotalItemsSold} items sold",
                Type = LeaderboardType.TopSellers,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(TopSellers, entries);
        return Task.FromResult(entries);
    }

    public Task<List<LeaderboardEntry>> GetTopMessagersAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .OrderByDescending(s => s.TotalMessagesSent)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = s.TotalMessagesSent,
                ScoreDisplay = FormatNumber(s.TotalMessagesSent) + " messages",
                Type = LeaderboardType.TopMessagers,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(TopMessagers, entries);
        return Task.FromResult(entries);
    }

    public Task<List<LeaderboardEntry>> GetTopBuyersAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .OrderByDescending(s => s.TotalOrders)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = s.TotalOrders,
                ScoreDisplay = $"{s.TotalOrders} orders",
                Type = LeaderboardType.TopBuyers,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(TopBuyers, entries);
        return Task.FromResult(entries);
    }

    public Task<List<LeaderboardEntry>> GetMostActiveUsersAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .OrderByDescending(s => s.TotalMessagesSent + s.TotalPostsCreated + s.TotalLikesGiven)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = s.TotalMessagesSent + s.TotalPostsCreated + s.TotalLikesGiven,
                ScoreDisplay = FormatNumber(s.TotalMessagesSent + s.TotalPostsCreated + s.TotalLikesGiven) + " activity",
                Type = LeaderboardType.MostActive,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(MostActiveUsers, entries);
        return Task.FromResult(entries);
    }

    public Task<List<LeaderboardEntry>> GetTopRatedSellersAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .Where(s => s.TotalReviews >= 5) // Minimum reviews required
            .OrderByDescending(s => s.AverageRating)
            .ThenByDescending(s => s.TotalReviews)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = (long)(s.AverageRating * 100),
                ScoreDisplay = $"★ {s.AverageRating:F1} ({s.TotalReviews} reviews)",
                Type = LeaderboardType.TopRatedSellers,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(TopRatedSellers, entries);
        return Task.FromResult(entries);
    }

    public Task<List<LeaderboardEntry>> GetWeeklyStarsAsync(int count = 10)
    {
        var entries = _data.UserStats.Values
            .OrderByDescending(s => s.WeeklySales + s.WeeklyMessages / 10)
            .Take(count)
            .Select((s, i) => new LeaderboardEntry
            {
                UserId = s.UserId,
                Username = s.Username,
                AvatarUrl = s.AvatarUrl,
                Rank = i + 1,
                Score = s.WeeklySales + s.WeeklyMessages / 10,
                ScoreDisplay = $"{s.WeeklySales} sales, {s.WeeklyMessages} msgs",
                Type = LeaderboardType.WeeklyStars,
                IsCurrentUser = s.UserId == _apiService.CurrentUser?.Id
            })
            .ToList();

        UpdateCollection(WeeklyStars, entries);
        return Task.FromResult(entries);
    }

    private void UpdateCollection(ObservableCollection<LeaderboardEntry> collection, List<LeaderboardEntry> entries)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            collection.Clear();
            foreach (var entry in entries)
                collection.Add(entry);
        });
    }

    private string FormatNumber(int number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    #endregion

    #region User Stats

    public Task<UserStats> GetUserStatsAsync(string userId)
    {
        if (!_data.UserStats.TryGetValue(userId, out var stats))
        {
            stats = new UserStats
            {
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            _data.UserStats[userId] = stats;
        }

        if (userId == _apiService.CurrentUser?.Id)
        {
            CurrentUserStats = stats;
            OnStatsUpdated?.Invoke(stats);
        }

        return Task.FromResult(stats);
    }

    public async Task IncrementStatAsync(StatType type, int amount = 1)
    {
        var userId = _apiService.CurrentUser?.Id;
        if (string.IsNullOrEmpty(userId)) return;

        var stats = await GetUserStatsAsync(userId);
        switch (type)
        {
            case StatType.MessagesSent:
                stats.TotalMessagesSent += amount;
                stats.WeeklyMessages += amount;
                stats.MonthlyMessages += amount;
                break;
            case StatType.MessagesReceived:
                stats.TotalMessagesReceived += amount;
                break;
            case StatType.ItemsSold:
                stats.TotalItemsSold += amount;
                stats.WeeklySales += amount;
                stats.MonthlySales += amount;
                break;
            case StatType.ItemsBought:
                stats.TotalItemsBought += amount;
                stats.WeeklyPurchases += amount;
                stats.MonthlyPurchases += amount;
                break;
            case StatType.OrdersCompleted:
                stats.TotalOrders += amount;
                stats.CompletedOrders += amount;
                break;
            case StatType.VoiceMinutes:
                stats.TotalVoiceMinutes += amount;
                break;
            case StatType.ProfileViews:
                stats.TotalProfileViews += amount;
                break;
            case StatType.PostsCreated:
                stats.TotalPostsCreated += amount;
                break;
            case StatType.LikesGiven:
                stats.TotalLikesGiven += amount;
                break;
            case StatType.LikesReceived:
                stats.TotalLikesReceived += amount;
                break;
        }

        stats.LastActive = DateTime.UtcNow;
        CurrentUserStats = stats;
        OnStatsUpdated?.Invoke(stats);
        await SaveAsync();
    }

    #endregion

    #region Profile Posts

    public Task<List<ProfilePost>> GetProfilePostsAsync(string userId)
    {
        var posts = _data.ProfilePosts
            .Where(p => p.ProfileUserId == userId && !p.IsDeleted)
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CurrentProfilePosts.Clear();
            foreach (var post in posts)
                CurrentProfilePosts.Add(post);
        });

        return Task.FromResult(posts);
    }

    public async Task CreateProfilePostAsync(string userId, string content, List<string>? attachmentUrls = null)
    {
        var post = new ProfilePost
        {
            ProfileUserId = userId,
            AuthorId = _apiService.CurrentUser?.Id ?? "",
            AuthorUsername = _apiService.CurrentUser?.Username ?? "",
            AuthorAvatarUrl = _apiService.CurrentUser?.AvatarUrl,
            Content = content,
            AttachmentUrls = attachmentUrls ?? new List<string>()
        };

        _data.ProfilePosts.Add(post);

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CurrentProfilePosts.Insert(0, post);
        });

        OnNewProfilePost?.Invoke(post);
        await IncrementStatAsync(StatType.PostsCreated);
        await SaveAsync();
    }

    public async Task DeleteProfilePostAsync(string postId)
    {
        var post = _data.ProfilePosts.FirstOrDefault(p => p.Id == postId);
        if (post != null)
        {
            post.IsDeleted = true;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var uiPost = CurrentProfilePosts.FirstOrDefault(p => p.Id == postId);
                if (uiPost != null)
                    CurrentProfilePosts.Remove(uiPost);
            });

            OnProfilePostDeleted?.Invoke(postId);
            await SaveAsync();
        }
    }

    public async Task LikeProfilePostAsync(string postId)
    {
        var post = _data.ProfilePosts.FirstOrDefault(p => p.Id == postId);
        var userId = _apiService.CurrentUser?.Id;
        if (post == null || string.IsNullOrEmpty(userId)) return;

        if (!post.LikedByUserIds.Contains(userId))
        {
            post.LikedByUserIds.Add(userId);
            post.LikeCount++;
            OnProfilePostLiked?.Invoke(post);
            await IncrementStatAsync(StatType.LikesGiven);

            // Increment likes received for post author
            if (post.AuthorId != userId && _data.UserStats.TryGetValue(post.AuthorId, out var authorStats))
            {
                authorStats.TotalLikesReceived++;
            }

            await SaveAsync();
        }
    }

    public async Task UnlikeProfilePostAsync(string postId)
    {
        var post = _data.ProfilePosts.FirstOrDefault(p => p.Id == postId);
        var userId = _apiService.CurrentUser?.Id;
        if (post == null || string.IsNullOrEmpty(userId)) return;

        if (post.LikedByUserIds.Remove(userId))
        {
            post.LikeCount = Math.Max(0, post.LikeCount - 1);
            await SaveAsync();
        }
    }

    public Task<ProfilePost> GetProfilePostAsync(string postId)
    {
        var post = _data.ProfilePosts.FirstOrDefault(p => p.Id == postId);
        return Task.FromResult(post ?? new ProfilePost());
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
                _data = JsonSerializer.Deserialize<LeaderboardData>(json) ?? new LeaderboardData();
            }

            // Load current user stats
            if (_apiService.CurrentUser?.Id != null)
            {
                await GetUserStatsAsync(_apiService.CurrentUser.Id);
            }

            // Initialize default data if empty
            if (_data.UserStats.Count == 0)
            {
                InitializeSampleData();
            }

            await RefreshLeaderboardsAsync();
        }
        catch
        {
            _data = new LeaderboardData();
            InitializeSampleData();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch
        {
            // Silently fail
        }
    }

    private void InitializeSampleData()
    {
        // Add some sample users for leaderboard demonstration
        var sampleUsers = new[]
        {
            ("user1", "TopSeller", 150, 50, 10000, 5.0),
            ("user2", "MegaTrader", 120, 80, 8500, 4.8),
            ("user3", "FastBuyer", 30, 200, 12000, 4.5),
            ("user4", "ChatMaster", 50, 40, 50000, 4.2),
            ("user5", "PowerUser", 90, 90, 25000, 4.9)
        };

        foreach (var (id, username, sales, purchases, messages, rating) in sampleUsers)
        {
            _data.UserStats[id] = new UserStats
            {
                UserId = id,
                Username = username,
                TotalItemsSold = sales,
                TotalItemsBought = purchases,
                TotalMessagesSent = messages,
                TotalOrders = purchases,
                CompletedOrders = purchases,
                AverageRating = rating,
                TotalReviews = sales / 2,
                PositiveReviews = (int)(sales / 2 * 0.9),
                WeeklySales = sales / 4,
                WeeklyMessages = messages / 10,
                JoinedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 365))
            };
        }
    }

    #endregion
}

/// <summary>
/// Data model for persisting leaderboard state
/// </summary>
public class LeaderboardData
{
    public Dictionary<string, UserStats> UserStats { get; set; } = new();
    public List<ProfilePost> ProfilePosts { get; set; } = new();
    public List<LeaderboardEntry> CachedLeaderboards { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
