using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VeaMarketplace.Client.Services;

public class ConnectionStats
{
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
    public int ReconnectionCount { get; set; }
    public DateTime ConnectedAt { get; set; }
    public TimeSpan Uptime { get; set; }
    public double AverageLatency { get; set; }
    public int CurrentLatency { get; set; }
    public double PacketLossRate { get; set; }
    public double BandwidthUsage { get; set; } // bytes per second
}

public interface IConnectionStatsService
{
    ConnectionStats GetStats();
    void RecordMessageSent(int byteCount);
    void RecordMessageReceived(int byteCount);
    void RecordLatency(int milliseconds);
    void RecordReconnection();
    void RecordPacketLoss();
    void RecordSuccessfulPacket();
    void Reset();
    event Action<ConnectionStats>? OnStatsUpdated;
}

public class ConnectionStatsService : IConnectionStatsService
{
    private long _bytesSent;
    private long _bytesReceived;
    private int _messagesSent;
    private int _messagesReceived;
    private int _reconnectionCount;
    private DateTime _connectedAt;
    private readonly List<int> _latencySamples = new();
    private int _currentLatency;
    private int _packetsLost;
    private int _packetsReceived;
    private readonly Queue<(DateTime timestamp, long bytes)> _bandwidthWindow = new();
    private readonly ReaderWriterLockSlim _lock = new();

    private const int LatencySampleSize = 100;
    private const int BandwidthWindowSeconds = 10;

    public event Action<ConnectionStats>? OnStatsUpdated;

    public ConnectionStatsService()
    {
        Reset();
    }

    public ConnectionStats GetStats()
    {
        _lock.EnterReadLock();
        try
        {
            CleanupBandwidthWindow();

            var stats = new ConnectionStats
            {
                BytesSent = _bytesSent,
                BytesReceived = _bytesReceived,
                MessagesSent = _messagesSent,
                MessagesReceived = _messagesReceived,
                ReconnectionCount = _reconnectionCount,
                ConnectedAt = _connectedAt,
                Uptime = DateTime.UtcNow - _connectedAt,
                AverageLatency = _latencySamples.Count > 0 ? _latencySamples.Average() : 0,
                CurrentLatency = _currentLatency,
                PacketLossRate = CalculatePacketLossRate(),
                BandwidthUsage = CalculateBandwidthUsage()
            };

            return stats;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void RecordMessageSent(int byteCount)
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Add(ref _bytesSent, byteCount);
            Interlocked.Increment(ref _messagesSent);
            _bandwidthWindow.Enqueue((DateTime.UtcNow, byteCount));

            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RecordMessageReceived(int byteCount)
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Add(ref _bytesReceived, byteCount);
            Interlocked.Increment(ref _messagesReceived);
            _bandwidthWindow.Enqueue((DateTime.UtcNow, byteCount));

            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RecordLatency(int milliseconds)
    {
        _lock.EnterWriteLock();
        try
        {
            _currentLatency = milliseconds;
            _latencySamples.Add(milliseconds);

            // Keep only recent samples
            if (_latencySamples.Count > LatencySampleSize)
            {
                _latencySamples.RemoveAt(0);
            }

            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RecordReconnection()
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Increment(ref _reconnectionCount);
            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RecordPacketLoss()
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Increment(ref _packetsLost);
            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RecordSuccessfulPacket()
    {
        _lock.EnterWriteLock();
        try
        {
            Interlocked.Increment(ref _packetsReceived);
            NotifyStatsUpdated();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Reset()
    {
        _lock.EnterWriteLock();
        try
        {
            _bytesSent = 0;
            _bytesReceived = 0;
            _messagesSent = 0;
            _messagesReceived = 0;
            _reconnectionCount = 0;
            _connectedAt = DateTime.UtcNow;
            _latencySamples.Clear();
            _currentLatency = 0;
            _packetsLost = 0;
            _packetsReceived = 0;
            _bandwidthWindow.Clear();

            Debug.WriteLine("Connection statistics reset");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private double CalculatePacketLossRate()
    {
        var totalPackets = _packetsLost + _packetsReceived;
        if (totalPackets == 0)
        {
            return 0;
        }

        return (double)_packetsLost / totalPackets;
    }

    private double CalculateBandwidthUsage()
    {
        if (_bandwidthWindow.Count == 0)
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddSeconds(-BandwidthWindowSeconds);
        var recentBytes = _bandwidthWindow
            .Where(x => x.timestamp >= cutoff)
            .Sum(x => x.bytes);

        return recentBytes / (double)BandwidthWindowSeconds;
    }

    private void CleanupBandwidthWindow()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-BandwidthWindowSeconds);
        while (_bandwidthWindow.Count > 0 && _bandwidthWindow.Peek().timestamp < cutoff)
        {
            _bandwidthWindow.Dequeue();
        }
    }

    private void NotifyStatsUpdated()
    {
        try
        {
            OnStatsUpdated?.Invoke(GetStats());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error notifying stats update: {ex.Message}");
        }
    }
}
