using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Rate limiting strategy
/// </summary>
public enum RateLimitStrategy
{
    SlidingWindow,
    FixedWindow,
    TokenBucket,
    LeakyBucket
}

/// <summary>
/// Rate limit result
/// </summary>
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int RemainingRequests { get; set; }
    public TimeSpan RetryAfter { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Rate limit configuration
/// </summary>
public class RateLimitConfig
{
    public int MaxRequests { get; set; } = 100;
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);
    public RateLimitStrategy Strategy { get; set; } = RateLimitStrategy.SlidingWindow;
}

public interface IRateLimitingService
{
    Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitConfig? config = null);
    Task ResetRateLimitAsync(string key);
    Task<int> GetRemainingRequestsAsync(string key);
    Task<Dictionary<string, int>> GetAllRateLimitsAsync();
}

public class RateLimitingService : IRateLimitingService
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly RateLimitConfig _defaultConfig;
    private readonly System.Threading.Timer _cleanupTimer;

    public RateLimitingService(RateLimitConfig? defaultConfig = null)
    {
        _defaultConfig = defaultConfig ?? new RateLimitConfig();

        // Cleanup old buckets every 5 minutes
        _cleanupTimer = new System.Threading.Timer(_ => CleanupExpiredBuckets(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitConfig? config = null)
    {
        await Task.CompletedTask;

        var effectiveConfig = config ?? _defaultConfig;
        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket(effectiveConfig));

        return bucket.TryConsume();
    }

    public async Task ResetRateLimitAsync(string key)
    {
        await Task.CompletedTask;

        if (_buckets.TryRemove(key, out _))
        {
            Debug.WriteLine($"Rate limit reset for key: {key}");
        }
    }

    public async Task<int> GetRemainingRequestsAsync(string key)
    {
        await Task.CompletedTask;

        if (_buckets.TryGetValue(key, out var bucket))
        {
            return bucket.GetRemainingRequests();
        }

        return _defaultConfig.MaxRequests;
    }

    public async Task<Dictionary<string, int>> GetAllRateLimitsAsync()
    {
        await Task.CompletedTask;

        var result = new Dictionary<string, int>();

        foreach (var kvp in _buckets)
        {
            result[kvp.Key] = kvp.Value.GetRemainingRequests();
        }

        return result;
    }

    private void CleanupExpiredBuckets()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _buckets)
        {
            if (kvp.Value.IsExpired(now))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            Debug.WriteLine($"Cleaned up {keysToRemove.Count} expired rate limit buckets");
        }
    }

    private class RateLimitBucket
    {
        private readonly RateLimitConfig _config;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DateTime _lastAccess;

        public RateLimitBucket(RateLimitConfig config)
        {
            _config = config;
            _lastAccess = DateTime.UtcNow;
        }

        public RateLimitResult TryConsume()
        {
            _lock.Wait();
            try
            {
                _lastAccess = DateTime.UtcNow;
                CleanupOldTimestamps();

                var currentCount = _timestamps.Count;

                if (currentCount >= _config.MaxRequests)
                {
                    // Rate limit exceeded
                    var oldestTimestamp = _timestamps.TryPeek(out var ts) ? ts : DateTime.UtcNow;
                    var retryAfter = oldestTimestamp.Add(_config.WindowSize) - DateTime.UtcNow;

                    Debug.WriteLine($"Rate limit exceeded: {currentCount}/{_config.MaxRequests}");

                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        RetryAfter = retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero,
                        Reason = $"Rate limit exceeded: {_config.MaxRequests} requests per {_config.WindowSize.TotalSeconds}s"
                    };
                }

                // Allow request
                _timestamps.Enqueue(DateTime.UtcNow);

                return new RateLimitResult
                {
                    IsAllowed = true,
                    RemainingRequests = _config.MaxRequests - _timestamps.Count,
                    RetryAfter = TimeSpan.Zero
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        public int GetRemainingRequests()
        {
            _lock.Wait();
            try
            {
                CleanupOldTimestamps();
                return Math.Max(0, _config.MaxRequests - _timestamps.Count);
            }
            finally
            {
                _lock.Release();
            }
        }

        public bool IsExpired(DateTime now)
        {
            // Consider bucket expired if not accessed for 2x the window size
            return now - _lastAccess > _config.WindowSize.Add(_config.WindowSize);
        }

        private void CleanupOldTimestamps()
        {
            var cutoff = DateTime.UtcNow - _config.WindowSize;

            while (_timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
            {
                _timestamps.TryDequeue(out _);
            }
        }
    }
}

/// <summary>
/// Extension methods for rate limiting
/// </summary>
public static class RateLimitingExtensions
{
    public static async Task<T> WithRateLimitAsync<T>(
        this Task<T> task,
        IRateLimitingService rateLimiter,
        string key,
        RateLimitConfig? config = null)
    {
        var result = await rateLimiter.CheckRateLimitAsync(key, config);

        if (!result.IsAllowed)
        {
            throw new RateLimitExceededException(result.Reason ?? "Rate limit exceeded", result.RetryAfter);
        }

        return await task;
    }

    public static async Task WithRateLimitAsync(
        this Task task,
        IRateLimitingService rateLimiter,
        string key,
        RateLimitConfig? config = null)
    {
        var result = await rateLimiter.CheckRateLimitAsync(key, config);

        if (!result.IsAllowed)
        {
            throw new RateLimitExceededException(result.Reason ?? "Rate limit exceeded", result.RetryAfter);
        }

        await task;
    }
}

public class RateLimitExceededException : Exception
{
    public TimeSpan RetryAfter { get; }

    public RateLimitExceededException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }
}
