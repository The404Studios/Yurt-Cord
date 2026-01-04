using System.Collections.Concurrent;
using System.Diagnostics;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Client-side message throttling to prevent overwhelming the server with 1000+ concurrent users.
/// Implements token bucket algorithm for smooth rate limiting.
/// </summary>
public class MessageThrottlingHelper
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly int _maxMessagesPerMinute;
    private readonly int _burstSize;

    public MessageThrottlingHelper(int maxMessagesPerMinute = 60, int burstSize = 10)
    {
        _maxMessagesPerMinute = maxMessagesPerMinute;
        _burstSize = burstSize;
    }

    /// <summary>
    /// Check if a message can be sent for the given key (e.g., channel or user)
    /// </summary>
    public bool CanSendMessage(string key)
    {
        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket
        {
            Tokens = _burstSize,
            MaxTokens = _burstSize,
            RefillRate = _maxMessagesPerMinute / 60.0, // Tokens per second
            LastRefill = DateTime.UtcNow
        });

        return bucket.TryConsume();
    }

    /// <summary>
    /// Get time until next message can be sent
    /// </summary>
    public TimeSpan GetTimeUntilNextMessage(string key)
    {
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            return TimeSpan.Zero;
        }

        if (bucket.Tokens >= 1)
        {
            return TimeSpan.Zero;
        }

        var tokensNeeded = 1 - bucket.Tokens;
        var secondsNeeded = tokensNeeded / bucket.RefillRate;
        return TimeSpan.FromSeconds(secondsNeeded);
    }

    /// <summary>
    /// Get current available tokens for a key
    /// </summary>
    public double GetAvailableTokens(string key)
    {
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            return _burstSize;
        }

        bucket.Refill();
        return bucket.Tokens;
    }

    /// <summary>
    /// Reset throttling for a specific key
    /// </summary>
    public void Reset(string key)
    {
        _buckets.TryRemove(key, out _);
    }

    /// <summary>
    /// Reset all throttling
    /// </summary>
    public void ResetAll()
    {
        _buckets.Clear();
    }

    private class TokenBucket
    {
        private readonly object _lock = new();

        public double Tokens { get; set; }
        public required int MaxTokens { get; set; }
        public required double RefillRate { get; set; }
        public DateTime LastRefill { get; set; }

        public void Refill()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - LastRefill).TotalSeconds;
                var tokensToAdd = elapsed * RefillRate;

                Tokens = Math.Min(MaxTokens, Tokens + tokensToAdd);
                LastRefill = now;
            }
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                Refill();

                if (Tokens >= 1)
                {
                    Tokens -= 1;
                    return true;
                }

                return false;
            }
        }
    }
}

/// <summary>
/// Debouncing helper to prevent rapid-fire events (e.g., typing indicators)
/// </summary>
public class DebounceHelper
{
    private readonly Dictionary<string, CancellationTokenSource> _pendingActions = new();
    private readonly object _lock = new();

    /// <summary>
    /// Debounce an action by the specified delay. Only the last action within the delay window will execute.
    /// </summary>
    public void Debounce(string key, Action action, TimeSpan delay)
    {
        lock (_lock)
        {
            // Cancel any pending action for this key
            if (_pendingActions.TryGetValue(key, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            // Schedule new action
            var cts = new CancellationTokenSource();
            _pendingActions[key] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, cts.Token);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        action();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when debounce is called again
                }
                finally
                {
                    lock (_lock)
                    {
                        if (_pendingActions.TryGetValue(key, out var currentCts) && currentCts == cts)
                        {
                            _pendingActions.Remove(key);
                        }
                        cts.Dispose();
                    }
                }
            }, cts.Token);
        }
    }

    /// <summary>
    /// Cancel all pending debounced actions
    /// </summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var cts in _pendingActions.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _pendingActions.Clear();
        }
    }
}

/// <summary>
/// Batching helper to batch multiple events together before processing
/// </summary>
public class BatchingHelper<T>
{
    private readonly List<T> _batch = new();
    private readonly object _lock = new();
    private readonly int _maxBatchSize;
    private readonly TimeSpan _maxBatchDelay;
    private DateTime _batchStartTime = DateTime.UtcNow;
    private readonly Action<List<T>> _processBatch;
    private CancellationTokenSource? _flushCts;

    public BatchingHelper(int maxBatchSize, TimeSpan maxBatchDelay, Action<List<T>> processBatch)
    {
        _maxBatchSize = maxBatchSize;
        _maxBatchDelay = maxBatchDelay;
        _processBatch = processBatch;
    }

    /// <summary>
    /// Add an item to the batch
    /// </summary>
    public void Add(T item)
    {
        lock (_lock)
        {
            if (_batch.Count == 0)
            {
                _batchStartTime = DateTime.UtcNow;
                ScheduleFlush();
            }

            _batch.Add(item);

            // Flush if batch is full
            if (_batch.Count >= _maxBatchSize)
            {
                FlushNow();
            }
        }
    }

    /// <summary>
    /// Add multiple items to the batch
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            if (_batch.Count == 0)
            {
                _batchStartTime = DateTime.UtcNow;
                ScheduleFlush();
            }

            _batch.AddRange(items);

            // Flush if batch is full
            if (_batch.Count >= _maxBatchSize)
            {
                FlushNow();
            }
        }
    }

    /// <summary>
    /// Flush the current batch immediately
    /// </summary>
    public void FlushNow()
    {
        lock (_lock)
        {
            _flushCts?.Cancel();
            _flushCts?.Dispose();
            _flushCts = null;

            if (_batch.Count > 0)
            {
                var batchCopy = new List<T>(_batch);
                _batch.Clear();

                try
                {
                    _processBatch(batchCopy);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing batch: {ex.Message}");
                }
            }
        }
    }

    private void ScheduleFlush()
    {
        _flushCts?.Cancel();
        _flushCts?.Dispose();

        _flushCts = new CancellationTokenSource();
        var cts = _flushCts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_maxBatchDelay, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    FlushNow();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when canceled
            }
        }, cts.Token);
    }

    public void Dispose()
    {
        FlushNow();
        _flushCts?.Dispose();
    }
}
