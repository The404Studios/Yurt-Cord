using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Orchestrates audio and video streaming to ensure smooth playback.
/// Manages memory, network I/O, and thread coordination as background processes.
///
/// Architecture:
/// - Memory Thread: Monitors memory usage, triggers GC when needed
/// - Network Monitor: Tracks connection quality, adjusts streaming parameters
/// - Voice Coordinator: Manages priority between audio and video
///
/// Priority hierarchy (highest to lowest):
/// 1. Audio Send Thread (Highest) - Voice must never be choppy
/// 2. Audio Receive/Decode - Incoming voice needs quick processing
/// 3. Screen Capture Thread (AboveNormal) - Consistent frame timing
/// 4. Screen Encode Thread (Normal) - CPU intensive, can yield
/// 5. Screen Send Task (Normal via ThreadPool) - Network I/O
/// </summary>
public class StreamingOrchestrator : IDisposable
{
    private static readonly Lazy<StreamingOrchestrator> _instance = new(() => new StreamingOrchestrator());
    public static StreamingOrchestrator Instance => _instance.Value;

    // Cancellation for background threads
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _disposed;

    // Voice activity state - use Interlocked for thread-safe updates
    private volatile bool _voiceActive;
    private volatile bool _isReceivingAudio;
    private volatile bool _isSendingAudio;
    private long _lastVoiceActivityTimeTicks = DateTime.MinValue.Ticks;  // Use ticks for Interlocked

    // Memory management
    private readonly Thread _memoryThread;
    private long _currentMemoryMb;  // Use Interlocked for thread-safe access
    private long _peakMemoryMb;     // Use Interlocked for thread-safe access
    private const long MemoryWarningThresholdMb = 500;  // Warn at 500MB
    private const long MemoryCleanupThresholdMb = 400;  // Cleanup at 400MB
    private const int MemoryCheckIntervalMs = 1000;     // Check every second

    // Network monitoring
    private readonly Thread _networkThread;
    private volatile int _networkLatencyMs;
    private volatile int _packetLossPercent;
    private volatile bool _networkCongested;
    private readonly ConcurrentQueue<long> _sendTimestamps = new();
    private readonly ConcurrentQueue<int> _latencySamples = new();
    private const int MaxLatencySamples = 30;
    private const int NetworkCheckIntervalMs = 500;

    // Buffer pool for reducing allocations
    private readonly ConcurrentBag<byte[]> _smallBufferPool = new();  // For audio (~4KB)
    private readonly ConcurrentBag<byte[]> _largeBufferPool = new();  // For video (~1MB)
    private const int SmallBufferSize = 4096;
    private const int LargeBufferSize = 1024 * 1024;
    private const int MaxPooledBuffers = 20;

    // Configuration
    private const int VoiceActivityTimeoutMs = 300;
    private const int VoiceYieldDelayMs = 10;
    private const int VoiceYieldMaxDelayMs = 20;
    private const int CongestedLatencyThresholdMs = 100;

    // Stats
    private long _audioPacketsSent;
    private long _audioPacketsReceived;
    private long _videoFramesSent;
    private long _videoFramesDropped;
    private long _gcCollections;
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

    // Properties
    public bool IsVoiceActive => _voiceActive;
    public bool IsReceivingAudio => _isReceivingAudio;
    public bool IsSendingAudio => _isSendingAudio;
    public bool IsNetworkCongested => _networkCongested;
    public int NetworkLatencyMs => _networkLatencyMs;
    public long CurrentMemoryMb => Interlocked.Read(ref _currentMemoryMb);

    // Events
    public event Action<bool>? OnVoiceActivityChanged;
    public event Action<bool>? OnNetworkCongestionChanged;
    public event Action<long>? OnMemoryWarning;

    private StreamingOrchestrator()
    {
        // Start memory management thread
        _memoryThread = new Thread(MemoryManagementLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "StreamingMemoryManager"
        };
        _memoryThread.Start();

        // Start network monitoring thread
        _networkThread = new Thread(NetworkMonitorLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "StreamingNetworkMonitor"
        };
        _networkThread.Start();

        // Pre-allocate some buffers
        for (int i = 0; i < 5; i++)
        {
            _smallBufferPool.Add(new byte[SmallBufferSize]);
        }
        for (int i = 0; i < 3; i++)
        {
            _largeBufferPool.Add(new byte[LargeBufferSize]);
        }

        Debug.WriteLine("[Orchestrator] Started with memory and network management threads");
    }

    #region Voice Activity Management

    /// <summary>
    /// Signal that audio is being sent (local user speaking)
    /// </summary>
    public void SignalAudioSend()
    {
        if (_disposed) return;
        _isSendingAudio = true;
        Interlocked.Exchange(ref _lastVoiceActivityTimeTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _audioPacketsSent);
        UpdateVoiceActive(true);
    }

    /// <summary>
    /// Signal that audio is being received (remote user speaking)
    /// </summary>
    public void SignalAudioReceive()
    {
        if (_disposed) return;
        _isReceivingAudio = true;
        Interlocked.Exchange(ref _lastVoiceActivityTimeTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _audioPacketsReceived);
        UpdateVoiceActive(true);
    }

    /// <summary>
    /// Signal that a video frame is being sent
    /// </summary>
    public void SignalVideoFrameSent()
    {
        if (_disposed) return;
        Interlocked.Increment(ref _videoFramesSent);
        _sendTimestamps.Enqueue(Stopwatch.GetTimestamp());

        // Keep queue bounded
        while (_sendTimestamps.Count > 100)
            _sendTimestamps.TryDequeue(out _);
    }

    /// <summary>
    /// Signal that a video frame was dropped
    /// </summary>
    public void SignalVideoFrameDropped()
    {
        if (_disposed) return;
        Interlocked.Increment(ref _videoFramesDropped);
    }

    /// <summary>
    /// Record network latency sample
    /// </summary>
    public void RecordLatency(int latencyMs)
    {
        if (_disposed) return;
        _latencySamples.Enqueue(latencyMs);
        while (_latencySamples.Count > MaxLatencySamples)
            _latencySamples.TryDequeue(out _);
    }

    /// <summary>
    /// Get the delay to apply before sending a video frame.
    /// </summary>
    public int GetVideoYieldDelay()
    {
        if (!_voiceActive) return 0;

        var lastActivityTicks = Interlocked.Read(ref _lastVoiceActivityTimeTicks);
        var elapsed = (DateTime.UtcNow - new DateTime(lastActivityTicks)).TotalMilliseconds;
        if (elapsed < 100) return VoiceYieldMaxDelayMs;
        return VoiceYieldDelayMs;
    }

    /// <summary>
    /// Check if video frame should be skipped to prioritize audio.
    /// </summary>
    public bool ShouldSkipVideoFrame(int frameNumber)
    {
        // Skip if voice very active or network congested
        var lastActivityTicks = Interlocked.Read(ref _lastVoiceActivityTimeTicks);
        if (_voiceActive && (DateTime.UtcNow - new DateTime(lastActivityTicks)).TotalMilliseconds < 50)
        {
            return frameNumber % 2 == 0;
        }

        if (_networkCongested)
        {
            return frameNumber % 3 == 0; // Skip every 3rd frame when congested
        }

        return false;
    }

    private void UpdateVoiceActive(bool active)
    {
        if (_voiceActive != active)
        {
            _voiceActive = active;
            OnVoiceActivityChanged?.Invoke(active);
        }
    }

    #endregion

    #region Memory Management

    /// <summary>
    /// Get a small buffer from the pool (for audio packets ~4KB)
    /// </summary>
    public byte[] RentSmallBuffer()
    {
        if (_smallBufferPool.TryTake(out var buffer))
            return buffer;
        return new byte[SmallBufferSize];
    }

    /// <summary>
    /// Return a small buffer to the pool
    /// </summary>
    public void ReturnSmallBuffer(byte[] buffer)
    {
        if (buffer.Length == SmallBufferSize && _smallBufferPool.Count < MaxPooledBuffers)
        {
            Array.Clear(buffer, 0, buffer.Length);
            _smallBufferPool.Add(buffer);
        }
    }

    /// <summary>
    /// Get a large buffer from the pool (for video frames ~1MB)
    /// </summary>
    public byte[] RentLargeBuffer()
    {
        if (_largeBufferPool.TryTake(out var buffer))
            return buffer;
        return new byte[LargeBufferSize];
    }

    /// <summary>
    /// Return a large buffer to the pool
    /// </summary>
    public void ReturnLargeBuffer(byte[] buffer)
    {
        if (buffer.Length == LargeBufferSize && _largeBufferPool.Count < MaxPooledBuffers)
        {
            // Don't clear large buffers - too expensive
            _largeBufferPool.Add(buffer);
        }
    }

    /// <summary>
    /// Memory management background loop
    /// </summary>
    private void MemoryManagementLoop()
    {
        var lastGcGen2 = GC.CollectionCount(2);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(MemoryCheckIntervalMs);

                // Get current memory usage
                var memoryBytes = GC.GetTotalMemory(false);
                var currentMb = memoryBytes / (1024 * 1024);
                Interlocked.Exchange(ref _currentMemoryMb, currentMb);

                // Update peak memory atomically
                long peakMb;
                do
                {
                    peakMb = Interlocked.Read(ref _peakMemoryMb);
                } while (currentMb > peakMb && Interlocked.CompareExchange(ref _peakMemoryMb, currentMb, peakMb) != peakMb);

                // Track GC collections
                var currentGcGen2 = GC.CollectionCount(2);
                if (currentGcGen2 > lastGcGen2)
                {
                    Interlocked.Add(ref _gcCollections, currentGcGen2 - lastGcGen2);
                    lastGcGen2 = currentGcGen2;
                }

                // Memory warning
                if (currentMb > MemoryWarningThresholdMb)
                {
                    OnMemoryWarning?.Invoke(currentMb);
                    Debug.WriteLine($"[Orchestrator] Memory warning: {currentMb}MB");
                }

                // Proactive cleanup at threshold
                if (currentMb > MemoryCleanupThresholdMb)
                {
                    // Request GC to clean up Gen0/Gen1 without blocking
                    GC.Collect(1, GCCollectionMode.Optimized, false);

                    // Clear excess pooled buffers
                    while (_smallBufferPool.Count > 5)
                        _smallBufferPool.TryTake(out _);
                    while (_largeBufferPool.Count > 2)
                        _largeBufferPool.TryTake(out _);
                }

                // Compact LOH periodically if memory is high
                if (currentMb > MemoryWarningThresholdMb && _uptimeStopwatch.Elapsed.TotalMinutes > 5)
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] Memory thread error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Network Monitoring

    /// <summary>
    /// Network monitoring background loop
    /// </summary>
    private void NetworkMonitorLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(NetworkCheckIntervalMs);

                // Calculate average latency
                if (_latencySamples.Count > 0)
                {
                    var samples = _latencySamples.ToArray();
                    _networkLatencyMs = (int)samples.Average();

                    // Detect congestion
                    var wasCongested = _networkCongested;
                    _networkCongested = _networkLatencyMs > CongestedLatencyThresholdMs;

                    if (wasCongested != _networkCongested)
                    {
                        OnNetworkCongestionChanged?.Invoke(_networkCongested);
                        Debug.WriteLine($"[Orchestrator] Network congestion: {_networkCongested} (latency: {_networkLatencyMs}ms)");
                    }
                }

                // Calculate packet loss from frame drops
                var totalFrames = _videoFramesSent + _videoFramesDropped;
                if (totalFrames > 0)
                {
                    _packetLossPercent = (int)((_videoFramesDropped * 100) / totalFrames);
                }

                // Voice activity timeout check
                if (_voiceActive)
                {
                    var lastActivityTicks = Interlocked.Read(ref _lastVoiceActivityTimeTicks);
                    var elapsed = (DateTime.UtcNow - new DateTime(lastActivityTicks)).TotalMilliseconds;
                    if (elapsed > VoiceActivityTimeoutMs)
                    {
                        _isSendingAudio = false;
                        _isReceivingAudio = false;
                        UpdateVoiceActive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Orchestrator] Network thread error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get recommended bitrate based on network conditions
    /// </summary>
    public int GetRecommendedBitrateKbps(int baseBitrateKbps)
    {
        if (_networkCongested)
        {
            // Reduce bitrate when congested
            return Math.Max(1000, baseBitrateKbps / 2);
        }

        if (_networkLatencyMs > 50)
        {
            // Slightly reduce for high latency
            return Math.Max(2000, (int)(baseBitrateKbps * 0.75));
        }

        return baseBitrateKbps;
    }

    /// <summary>
    /// Get recommended FPS based on network conditions
    /// </summary>
    public int GetRecommendedFps(int targetFps)
    {
        if (_networkCongested)
        {
            return Math.Max(15, targetFps / 2);
        }

        if (_voiceActive)
        {
            // Reduce FPS when voice active to prioritize audio
            return Math.Max(20, (int)(targetFps * 0.75));
        }

        return targetFps;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get current streaming statistics
    /// </summary>
    public StreamingStats GetStats()
    {
        return new StreamingStats
        {
            AudioPacketsSent = Interlocked.Read(ref _audioPacketsSent),
            AudioPacketsReceived = Interlocked.Read(ref _audioPacketsReceived),
            VideoFramesSent = Interlocked.Read(ref _videoFramesSent),
            VideoFramesDropped = Interlocked.Read(ref _videoFramesDropped),
            IsVoiceActive = _voiceActive,
            NetworkLatencyMs = _networkLatencyMs,
            IsNetworkCongested = _networkCongested,
            CurrentMemoryMb = Interlocked.Read(ref _currentMemoryMb),
            PeakMemoryMb = Interlocked.Read(ref _peakMemoryMb),
            GCCollections = Interlocked.Read(ref _gcCollections),
            UptimeSeconds = (int)_uptimeStopwatch.Elapsed.TotalSeconds
        };
    }

    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStats()
    {
        Interlocked.Exchange(ref _audioPacketsSent, 0);
        Interlocked.Exchange(ref _audioPacketsReceived, 0);
        Interlocked.Exchange(ref _videoFramesSent, 0);
        Interlocked.Exchange(ref _videoFramesDropped, 0);
        Interlocked.Exchange(ref _gcCollections, 0);
        Interlocked.Exchange(ref _peakMemoryMb, Interlocked.Read(ref _currentMemoryMb));
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();

        try { _memoryThread.Join(1000); } catch { }
        try { _networkThread.Join(1000); } catch { }

        _cts.Dispose();

        // Clear pools
        _smallBufferPool.Clear();
        _largeBufferPool.Clear();

        Debug.WriteLine("[Orchestrator] Disposed");
    }
}

/// <summary>
/// Statistics about streaming performance
/// </summary>
public class StreamingStats
{
    public long AudioPacketsSent { get; set; }
    public long AudioPacketsReceived { get; set; }
    public long VideoFramesSent { get; set; }
    public long VideoFramesDropped { get; set; }
    public bool IsVoiceActive { get; set; }
    public int NetworkLatencyMs { get; set; }
    public bool IsNetworkCongested { get; set; }
    public long CurrentMemoryMb { get; set; }
    public long PeakMemoryMb { get; set; }
    public long GCCollections { get; set; }
    public int UptimeSeconds { get; set; }

    public double VideoDropRate => VideoFramesSent > 0
        ? (double)VideoFramesDropped / (VideoFramesSent + VideoFramesDropped) * 100
        : 0;
}
