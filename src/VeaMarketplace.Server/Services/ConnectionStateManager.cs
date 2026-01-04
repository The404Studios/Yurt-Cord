using System.Collections.Concurrent;
using System.Diagnostics;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// High-performance connection state manager optimized for 1000+ concurrent users.
/// Uses lock-free concurrent data structures for maximum throughput.
/// </summary>
public class ConnectionStateManager
{
    // Lock-free concurrent dictionaries for maximum performance
    private readonly ConcurrentDictionary<string, UserConnectionState> _userConnections = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userIdToConnections = new();
    private readonly object _setLock = new(); // Only for HashSet modifications

    // Metrics for monitoring
    private long _totalConnections = 0;
    private long _peakConnections = 0;
    private readonly System.Timers.Timer _metricsTimer;

    public ConnectionStateManager()
    {
        // Report metrics every 30 seconds
        _metricsTimer = new System.Timers.Timer(30000);
        _metricsTimer.Elapsed += (s, e) => LogMetrics();
        _metricsTimer.Start();
    }

    /// <summary>
    /// Register a new connection for a user
    /// </summary>
    public void AddConnection(string connectionId, string userId, string hubName)
    {
        var state = new UserConnectionState
        {
            ConnectionId = connectionId,
            UserId = userId,
            HubName = hubName,
            ConnectedAt = DateTime.UtcNow
        };

        _userConnections[connectionId] = state;

        // Track all connections for this user
        lock (_setLock)
        {
            if (!_userIdToConnections.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                _userIdToConnections[userId] = connections;
            }
            connections.Add(connectionId);
        }

        // Update metrics
        var currentCount = Interlocked.Increment(ref _totalConnections);
        UpdatePeakConnections(currentCount);

        Debug.WriteLine($"[ConnectionState] User {userId} connected to {hubName} (ConnectionId: {connectionId}). Total: {currentCount}");
    }

    /// <summary>
    /// Remove a connection when user disconnects
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        if (_userConnections.TryRemove(connectionId, out var state))
        {
            lock (_setLock)
            {
                if (_userIdToConnections.TryGetValue(state.UserId, out var connections))
                {
                    connections.Remove(connectionId);

                    // Clean up if no more connections
                    if (connections.Count == 0)
                    {
                        _userIdToConnections.TryRemove(state.UserId, out _);
                    }
                }
            }

            var currentCount = Interlocked.Decrement(ref _totalConnections);
            Debug.WriteLine($"[ConnectionState] User {state.UserId} disconnected from {state.HubName}. Total: {currentCount}");
        }
    }

    /// <summary>
    /// Get all connection IDs for a specific user
    /// </summary>
    public List<string> GetUserConnections(string userId)
    {
        lock (_setLock)
        {
            if (_userIdToConnections.TryGetValue(userId, out var connections))
            {
                return connections.ToList();
            }
        }
        return new List<string>();
    }

    /// <summary>
    /// Get all connection IDs for a specific hub
    /// </summary>
    public List<string> GetHubConnections(string hubName)
    {
        return _userConnections.Values
            .Where(c => c.HubName == hubName)
            .Select(c => c.ConnectionId)
            .ToList();
    }

    /// <summary>
    /// Check if a user is online (has any active connections)
    /// </summary>
    public bool IsUserOnline(string userId)
    {
        lock (_setLock)
        {
            return _userIdToConnections.ContainsKey(userId);
        }
    }

    /// <summary>
    /// Get list of all online user IDs
    /// </summary>
    public List<string> GetOnlineUserIds()
    {
        lock (_setLock)
        {
            return _userIdToConnections.Keys.ToList();
        }
    }

    /// <summary>
    /// Get count of online users
    /// </summary>
    public int GetOnlineUserCount()
    {
        lock (_setLock)
        {
            return _userIdToConnections.Count;
        }
    }

    /// <summary>
    /// Get total number of active connections
    /// </summary>
    public long GetTotalConnections()
    {
        return Interlocked.Read(ref _totalConnections);
    }

    /// <summary>
    /// Get peak connections count
    /// </summary>
    public long GetPeakConnections()
    {
        return Interlocked.Read(ref _peakConnections);
    }

    /// <summary>
    /// Get connection state by connection ID
    /// </summary>
    public UserConnectionState? GetConnectionState(string connectionId)
    {
        _userConnections.TryGetValue(connectionId, out var state);
        return state;
    }

    /// <summary>
    /// Update last activity time for a connection
    /// </summary>
    public void UpdateActivity(string connectionId)
    {
        if (_userConnections.TryGetValue(connectionId, out var state))
        {
            state.LastActivityAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Get connections that haven't been active for the specified duration
    /// </summary>
    public List<string> GetInactiveConnections(TimeSpan inactivityThreshold)
    {
        var cutoff = DateTime.UtcNow - inactivityThreshold;
        return _userConnections.Values
            .Where(c => c.LastActivityAt < cutoff)
            .Select(c => c.ConnectionId)
            .ToList();
    }

    private void UpdatePeakConnections(long currentCount)
    {
        long currentPeak;
        do
        {
            currentPeak = Interlocked.Read(ref _peakConnections);
            if (currentCount <= currentPeak) break;
        }
        while (Interlocked.CompareExchange(ref _peakConnections, currentCount, currentPeak) != currentPeak);
    }

    private void LogMetrics()
    {
        var total = GetTotalConnections();
        var peak = GetPeakConnections();
        var onlineUsers = GetOnlineUserCount();

        Debug.WriteLine($"[ConnectionMetrics] Connections: {total}, Online Users: {onlineUsers}, Peak: {peak}");
    }

    public void Dispose()
    {
        _metricsTimer?.Stop();
        _metricsTimer?.Dispose();
    }
}

/// <summary>
/// State information for a single connection
/// </summary>
public class UserConnectionState
{
    public required string ConnectionId { get; set; }
    public required string UserId { get; set; }
    public required string HubName { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
