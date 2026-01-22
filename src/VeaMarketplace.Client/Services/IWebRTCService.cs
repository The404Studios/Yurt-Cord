using System.Collections.Concurrent;
using System.Diagnostics;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for managing WebRTC peer-to-peer connections.
/// Provides lower latency audio/video for 1:1 and small group calls.
/// Falls back to server relay when P2P connection fails.
/// </summary>
public interface IWebRTCService
{
    /// <summary>Whether the service is initialized and ready.</summary>
    bool IsInitialized { get; }

    /// <summary>Currently active P2P connections by peer connection ID.</summary>
    IReadOnlyDictionary<string, P2PConnectionState> ActiveConnections { get; }

    /// <summary>Fired when a P2P connection is established.</summary>
    event Action<string, string>? OnP2PConnected; // connectionId, username

    /// <summary>Fired when a P2P connection fails or disconnects.</summary>
    event Action<string, string>? OnP2PDisconnected; // connectionId, reason

    /// <summary>Fired when audio data is received from a P2P peer.</summary>
    event Action<string, byte[]>? OnP2PAudioReceived; // connectionId, audioData

    /// <summary>Fired when we need to send a signaling message through SignalR.</summary>
    event Action<string, string, string>? OnSignalingMessage; // targetConnectionId, messageType, data

    /// <summary>Initialize the WebRTC service.</summary>
    Task InitializeAsync();

    /// <summary>Request a P2P connection with a peer.</summary>
    Task<bool> RequestP2PConnectionAsync(string targetConnectionId, string targetUserId, string targetUsername);

    /// <summary>Handle incoming P2P connection request.</summary>
    Task HandleP2PRequestAsync(string fromConnectionId, string fromUserId, string fromUsername);

    /// <summary>Handle incoming SDP offer.</summary>
    Task HandleOfferAsync(string fromConnectionId, string sdpOffer);

    /// <summary>Handle incoming SDP answer.</summary>
    Task HandleAnswerAsync(string fromConnectionId, string sdpAnswer);

    /// <summary>Handle incoming ICE candidate.</summary>
    Task HandleICECandidateAsync(string fromConnectionId, string candidate, string sdpMid, int sdpMLineIndex);

    /// <summary>Send audio data to a P2P peer.</summary>
    Task SendAudioAsync(string targetConnectionId, byte[] audioData);

    /// <summary>Close a specific P2P connection.</summary>
    Task CloseConnectionAsync(string connectionId);

    /// <summary>Close all P2P connections.</summary>
    Task CloseAllConnectionsAsync();

    /// <summary>Check if we have an active P2P connection with a peer.</summary>
    bool HasP2PConnection(string connectionId);
}

/// <summary>
/// State of a P2P connection.
/// </summary>
public class P2PConnectionState
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public P2PConnectionStatus Status { get; set; } = P2PConnectionStatus.Connecting;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}

public enum P2PConnectionStatus
{
    Connecting,
    Connected,
    Disconnected,
    Failed
}

/// <summary>
/// WebRTC service implementation.
/// Note: This is a simplified implementation that uses SignalR for signaling
/// and prepares the infrastructure for full WebRTC when a native library is available.
/// For WPF, consider using WebView2 with WebRTC or a native library like libwebrtc.
/// </summary>
public class WebRTCService : IWebRTCService
{
    private readonly ConcurrentDictionary<string, P2PConnectionState> _connections = new();
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public IReadOnlyDictionary<string, P2PConnectionState> ActiveConnections => _connections;

    public event Action<string, string>? OnP2PConnected;
    public event Action<string, string>? OnP2PDisconnected;
#pragma warning disable CS0067 // Event is never used - reserved for future WebRTC audio implementation
    public event Action<string, byte[]>? OnP2PAudioReceived;
#pragma warning restore CS0067
    public event Action<string, string, string>? OnSignalingMessage;

    public Task InitializeAsync()
    {
        // In a full implementation, this would initialize the WebRTC native library
        // For now, we set up the infrastructure for signaling
        _isInitialized = true;
        Debug.WriteLine("WebRTCService: Initialized (signaling infrastructure ready)");
        return Task.CompletedTask;
    }

    public async Task<bool> RequestP2PConnectionAsync(string targetConnectionId, string targetUserId, string targetUsername)
    {
        if (!_isInitialized)
        {
            Debug.WriteLine("WebRTCService: Not initialized");
            return false;
        }

        // Check if we already have a connection
        if (_connections.ContainsKey(targetConnectionId))
        {
            Debug.WriteLine($"WebRTCService: Already have connection to {targetConnectionId}");
            return true;
        }

        // Create new connection state
        var state = new P2PConnectionState
        {
            ConnectionId = targetConnectionId,
            UserId = targetUserId,
            Username = targetUsername,
            Status = P2PConnectionStatus.Connecting,
            ConnectedAt = DateTime.UtcNow
        };

        _connections[targetConnectionId] = state;

        // In a full implementation, this would:
        // 1. Create RTCPeerConnection
        // 2. Add local audio/video tracks
        // 3. Create SDP offer
        // 4. Send offer through SignalR

        // For now, simulate the signaling request
        Debug.WriteLine($"WebRTCService: Requesting P2P connection to {targetUsername} ({targetConnectionId})");

        // Signal through SignalR that we want to establish P2P
        OnSignalingMessage?.Invoke(targetConnectionId, "P2PRequest", targetUserId);

        // Start connection timeout
        _ = Task.Run(async () =>
        {
            await Task.Delay(AppConstants.WebRTC.P2PConnectionTimeoutMs);
            if (_connections.TryGetValue(targetConnectionId, out var conn) && conn.Status == P2PConnectionStatus.Connecting)
            {
                conn.Status = P2PConnectionStatus.Failed;
                OnP2PDisconnected?.Invoke(targetConnectionId, "Connection timeout");
                _connections.TryRemove(targetConnectionId, out _);
            }
        });

        return await Task.FromResult(true);
    }

    public Task HandleP2PRequestAsync(string fromConnectionId, string fromUserId, string fromUsername)
    {
        Debug.WriteLine($"WebRTCService: Received P2P request from {fromUsername} ({fromConnectionId})");

        // Create connection state for incoming request
        var state = new P2PConnectionState
        {
            ConnectionId = fromConnectionId,
            UserId = fromUserId,
            Username = fromUsername,
            Status = P2PConnectionStatus.Connecting,
            ConnectedAt = DateTime.UtcNow
        };

        _connections[fromConnectionId] = state;

        // In a full implementation, this would:
        // 1. Create RTCPeerConnection
        // 2. Wait for offer and create answer

        return Task.CompletedTask;
    }

    public Task HandleOfferAsync(string fromConnectionId, string sdpOffer)
    {
        Debug.WriteLine($"WebRTCService: Received SDP offer from {fromConnectionId}");

        // In a full implementation, this would:
        // 1. Set remote description with the offer
        // 2. Create SDP answer
        // 3. Send answer back through SignalR

        if (_connections.TryGetValue(fromConnectionId, out var conn))
        {
            conn.LastActivityAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task HandleAnswerAsync(string fromConnectionId, string sdpAnswer)
    {
        Debug.WriteLine($"WebRTCService: Received SDP answer from {fromConnectionId}");

        // In a full implementation, this would:
        // 1. Set remote description with the answer
        // 2. Complete ICE negotiation

        if (_connections.TryGetValue(fromConnectionId, out var conn))
        {
            conn.LastActivityAt = DateTime.UtcNow;
            conn.Status = P2PConnectionStatus.Connected;
            OnP2PConnected?.Invoke(fromConnectionId, conn.Username);
        }

        return Task.CompletedTask;
    }

    public Task HandleICECandidateAsync(string fromConnectionId, string candidate, string sdpMid, int sdpMLineIndex)
    {
        Debug.WriteLine($"WebRTCService: Received ICE candidate from {fromConnectionId}");

        // In a full implementation, this would:
        // Add the ICE candidate to the peer connection

        if (_connections.TryGetValue(fromConnectionId, out var conn))
        {
            conn.LastActivityAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task SendAudioAsync(string targetConnectionId, byte[] audioData)
    {
        if (!_connections.TryGetValue(targetConnectionId, out var conn))
        {
            return Task.CompletedTask;
        }

        if (conn.Status != P2PConnectionStatus.Connected)
        {
            return Task.CompletedTask;
        }

        // In a full implementation, this would send audio through the RTCDataChannel
        // or via the audio track
        conn.BytesSent += audioData.Length;
        conn.LastActivityAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task CloseConnectionAsync(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var conn))
        {
            conn.Status = P2PConnectionStatus.Disconnected;
            OnP2PDisconnected?.Invoke(connectionId, "Connection closed");
            Debug.WriteLine($"WebRTCService: Closed connection to {conn.Username}");
        }

        return Task.CompletedTask;
    }

    public Task CloseAllConnectionsAsync()
    {
        foreach (var connectionId in _connections.Keys.ToList())
        {
            _ = CloseConnectionAsync(connectionId);
        }

        return Task.CompletedTask;
    }

    public bool HasP2PConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var conn) &&
               conn.Status == P2PConnectionStatus.Connected;
    }
}
