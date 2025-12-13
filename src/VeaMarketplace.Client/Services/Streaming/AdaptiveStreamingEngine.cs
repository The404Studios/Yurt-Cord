using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VeaMarketplace.Client.Services.Streaming;

/// <summary>
/// High-performance adaptive streaming engine with intelligent compression.
///
/// Architecture:
/// - Delta encoding: Only transmit changed screen regions
/// - Adaptive quality: Adjust compression based on content and network
/// - Smart buffering: Pre-allocated pools to eliminate GC pressure
/// - Region-of-interest: Higher quality for active areas
/// - Network adaptation: Dynamic bitrate based on conditions
/// </summary>
public class AdaptiveStreamingEngine : IDisposable
{
    // === Configuration ===
    private readonly StreamingConfig _config;
    private volatile bool _disposed;

    // === Frame Processing ===
    private readonly DeltaFrameEncoder _deltaEncoder;
    private readonly SmartCompressor _compressor;
    private readonly FrameBufferPool _bufferPool;
    private readonly NetworkAdapter _networkAdapter;

    // === Statistics ===
    private readonly StreamingStats _stats = new();
    private readonly Stopwatch _sessionTimer = new();

    // === Events ===
    public event Action<EncodedFrame>? OnFrameReady;
    public event Action<StreamingStats>? OnStatsUpdated;

    public StreamingStats Stats => _stats;
    public bool IsRunning { get; private set; }

    public AdaptiveStreamingEngine(StreamingConfig? config = null)
    {
        _config = config ?? new StreamingConfig();
        _bufferPool = new FrameBufferPool(_config.MaxWidth, _config.MaxHeight);
        _deltaEncoder = new DeltaFrameEncoder(_config, _bufferPool);
        _compressor = new SmartCompressor(_config);
        _networkAdapter = new NetworkAdapter(_config);
    }

    /// <summary>
    /// Process a captured frame with intelligent encoding.
    /// Returns encoded data ready for transmission.
    /// </summary>
    public EncodedFrame? ProcessFrame(Bitmap frame, int frameNumber)
    {
        if (_disposed || frame == null) return null;

        var timer = Stopwatch.StartNew();

        try
        {
            // Step 1: Detect changes from previous frame
            var deltaResult = _deltaEncoder.ComputeDelta(frame, frameNumber);

            // Skip if no significant changes
            if (deltaResult.ChangePercentage < _config.MinChangeThreshold && frameNumber > 0)
            {
                _stats.FramesSkipped++;
                return null;
            }

            // Step 2: Determine optimal quality based on content and network
            var quality = _networkAdapter.GetOptimalQuality(
                deltaResult.ChangePercentage,
                deltaResult.IsHighMotion,
                _stats.CurrentBitrateMbps
            );

            // Step 3: Encode with smart compression
            var encodedData = _compressor.Encode(
                frame,
                deltaResult,
                quality
            );

            if (encodedData == null || encodedData.Length == 0)
                return null;

            // Step 4: Update statistics
            timer.Stop();
            UpdateStats(encodedData.Length, timer.ElapsedMilliseconds, deltaResult);

            return new EncodedFrame
            {
                Data = encodedData,
                Width = frame.Width,
                Height = frame.Height,
                FrameNumber = frameNumber,
                IsKeyFrame = deltaResult.IsKeyFrame,
                Quality = quality,
                ChangePercentage = deltaResult.ChangePercentage,
                EncodingTimeMs = timer.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Frame processing error: {ex.Message}");
            _stats.EncodingErrors++;
            return null;
        }
    }

    /// <summary>
    /// Record network feedback for adaptive quality adjustment.
    /// </summary>
    public void RecordNetworkFeedback(int latencyMs, bool wasDropped)
    {
        _networkAdapter.RecordFeedback(latencyMs, wasDropped);
    }

    /// <summary>
    /// Set the number of active viewers for optimization.
    /// </summary>
    public void SetViewerCount(int count)
    {
        _networkAdapter.SetViewerCount(count);
    }

    /// <summary>
    /// Force a keyframe on the next encode.
    /// </summary>
    public void RequestKeyFrame()
    {
        _deltaEncoder.RequestKeyFrame();
    }

    private void UpdateStats(int frameSize, long encodingTimeMs, DeltaResult delta)
    {
        _stats.FramesProcessed++;
        _stats.TotalBytesSent += frameSize;
        _stats.LastFrameSizeBytes = frameSize;
        _stats.LastEncodingTimeMs = encodingTimeMs;
        _stats.AverageChangePercent = (_stats.AverageChangePercent * 0.9) + (delta.ChangePercentage * 0.1);

        // Calculate bitrate (rolling average over 1 second)
        if (_sessionTimer.ElapsedMilliseconds > 0)
        {
            _stats.CurrentBitrateMbps = (_stats.TotalBytesSent * 8.0) / (_sessionTimer.ElapsedMilliseconds * 1000.0);
        }

        OnStatsUpdated?.Invoke(_stats);
    }

    public void Start()
    {
        IsRunning = true;
        _sessionTimer.Restart();
        _stats.Reset();
    }

    public void Stop()
    {
        IsRunning = false;
        _sessionTimer.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _deltaEncoder.Dispose();
        _compressor.Dispose();
        _bufferPool.Dispose();
    }
}

/// <summary>
/// Streaming configuration with sensible defaults.
/// </summary>
public class StreamingConfig
{
    // Resolution limits
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;

    // Quality settings
    public int BaseQuality { get; set; } = 70;        // JPEG quality 0-100
    public int MinQuality { get; set; } = 30;         // Minimum during congestion
    public int MaxQuality { get; set; } = 95;         // Maximum for static content

    // Delta encoding
    public float MinChangeThreshold { get; set; } = 0.1f;  // Skip if less than 0.1% changed
    public int KeyFrameInterval { get; set; } = 300;       // Force keyframe every N frames
    public int BlockSize { get; set; } = 16;               // Block size for change detection

    // Network adaptation
    public int TargetBitrateMbps { get; set; } = 8;
    public int MaxBitrateMbps { get; set; } = 30;
    public int MinBitrateMbps { get; set; } = 1;

    // Performance
    public int TargetFps { get; set; } = 60;
    public int BufferPoolSize { get; set; } = 5;
}

/// <summary>
/// Result of delta computation between frames.
/// </summary>
public class DeltaResult
{
    public bool IsKeyFrame { get; set; }
    public float ChangePercentage { get; set; }
    public bool IsHighMotion { get; set; }
    public System.Drawing.Rectangle[] ChangedRegions { get; set; } = Array.Empty<System.Drawing.Rectangle>();
    public System.Drawing.Rectangle BoundingBox { get; set; }
}

/// <summary>
/// Encoded frame ready for transmission.
/// </summary>
public class EncodedFrame
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameNumber { get; set; }
    public bool IsKeyFrame { get; set; }
    public int Quality { get; set; }
    public float ChangePercentage { get; set; }
    public long EncodingTimeMs { get; set; }
}

/// <summary>
/// Streaming statistics.
/// </summary>
public class StreamingStats
{
    public long FramesProcessed { get; set; }
    public long FramesSkipped { get; set; }
    public long TotalBytesSent { get; set; }
    public int LastFrameSizeBytes { get; set; }
    public long LastEncodingTimeMs { get; set; }
    public double CurrentBitrateMbps { get; set; }
    public double AverageChangePercent { get; set; }
    public int EncodingErrors { get; set; }

    public double CompressionRatio => FramesProcessed > 0
        ? (double)TotalBytesSent / (FramesProcessed * LastFrameSizeBytes)
        : 0;

    public void Reset()
    {
        FramesProcessed = 0;
        FramesSkipped = 0;
        TotalBytesSent = 0;
        LastFrameSizeBytes = 0;
        LastEncodingTimeMs = 0;
        CurrentBitrateMbps = 0;
        AverageChangePercent = 0;
        EncodingErrors = 0;
    }
}
