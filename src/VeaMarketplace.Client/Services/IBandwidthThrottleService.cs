using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

public enum ThrottleLevel
{
    None,
    Low,
    Medium,
    High,
    Extreme
}

public interface IBandwidthThrottleService
{
    ThrottleLevel CurrentThrottleLevel { get; set; }
    long BytesPerSecondLimit { get; }
    Task<bool> RequestBandwidthAsync(long bytes, CancellationToken cancellationToken = default);
    void RecordBandwidthUsage(long bytes);
    Task WaitForBandwidthAsync(long bytes, CancellationToken cancellationToken = default);
    BandwidthStats GetStats();
}

public class BandwidthStats
{
    public long BytesUsedThisSecond { get; set; }
    public long BytesThrottled { get; set; }
    public int ThrottleCount { get; set; }
    public double UtilizationPercent { get; set; }
}

public class BandwidthThrottleService : IBandwidthThrottleService
{
    private readonly ConcurrentQueue<(DateTime timestamp, long bytes)> _bandwidthWindow = new();
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
    private long _bytesThrottled;
    private int _throttleCount;

    private const int WindowSizeMs = 1000;

    public ThrottleLevel CurrentThrottleLevel { get; set; } = ThrottleLevel.None;

    public long BytesPerSecondLimit => GetBytesPerSecondLimit(CurrentThrottleLevel);

    public async Task<bool> RequestBandwidthAsync(long bytes, CancellationToken cancellationToken = default)
    {
        if (CurrentThrottleLevel == ThrottleLevel.None)
        {
            RecordBandwidthUsage(bytes);
            return true;
        }

        await _throttleSemaphore.WaitAsync(cancellationToken);
        try
        {
            CleanupWindow();

            var currentUsage = CalculateCurrentUsage();
            var limit = BytesPerSecondLimit;

            if (currentUsage + bytes <= limit)
            {
                RecordBandwidthUsage(bytes);
                return true;
            }

            Interlocked.Add(ref _bytesThrottled, bytes);
            Interlocked.Increment(ref _throttleCount);

            Debug.WriteLine($"Bandwidth request throttled: {bytes} bytes (current usage: {currentUsage}/{limit})");

            return false;
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }

    public void RecordBandwidthUsage(long bytes)
    {
        _bandwidthWindow.Enqueue((DateTime.UtcNow, bytes));
    }

    public async Task WaitForBandwidthAsync(long bytes, CancellationToken cancellationToken = default)
    {
        if (CurrentThrottleLevel == ThrottleLevel.None)
        {
            RecordBandwidthUsage(bytes);
            return;
        }

        const int maxRetries = 100;
        int retries = 0;

        while (!cancellationToken.IsCancellationRequested && retries < maxRetries)
        {
            if (await RequestBandwidthAsync(bytes, cancellationToken))
            {
                return;
            }

            // Wait a bit and try again
            await Task.Delay(100, cancellationToken);
            retries++;
        }

        if (retries >= maxRetries)
        {
            Debug.WriteLine($"Max retries reached waiting for bandwidth, allowing request through");
            RecordBandwidthUsage(bytes);
        }
    }

    public BandwidthStats GetStats()
    {
        CleanupWindow();

        var currentUsage = CalculateCurrentUsage();
        var limit = BytesPerSecondLimit;

        return new BandwidthStats
        {
            BytesUsedThisSecond = currentUsage,
            BytesThrottled = Interlocked.Read(ref _bytesThrottled),
            ThrottleCount = _throttleCount,
            UtilizationPercent = limit > 0 ? (currentUsage / (double)limit) * 100 : 0
        };
    }

    private void CleanupWindow()
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-WindowSizeMs);

        while (_bandwidthWindow.TryPeek(out var entry) && entry.timestamp < cutoff)
        {
            _bandwidthWindow.TryDequeue(out _);
        }
    }

    private long CalculateCurrentUsage()
    {
        long total = 0;

        foreach (var entry in _bandwidthWindow)
        {
            total += entry.bytes;
        }

        return total;
    }

    private static long GetBytesPerSecondLimit(ThrottleLevel level)
    {
        return level switch
        {
            ThrottleLevel.None => long.MaxValue,
            ThrottleLevel.Low => 10 * 1024 * 1024, // 10 MB/s
            ThrottleLevel.Medium => 5 * 1024 * 1024, // 5 MB/s
            ThrottleLevel.High => 2 * 1024 * 1024, // 2 MB/s
            ThrottleLevel.Extreme => 512 * 1024, // 512 KB/s
            _ => long.MaxValue
        };
    }
}
