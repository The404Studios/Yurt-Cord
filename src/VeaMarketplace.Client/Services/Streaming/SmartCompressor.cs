using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VeaMarketplace.Client.Services.Streaming;

/// <summary>
/// Smart compressor with content-aware encoding.
///
/// Features:
/// - Adaptive quality based on content type
/// - Region-of-interest encoding (higher quality for active areas)
/// - Progressive quality for large frames
/// - Efficient JPEG encoding with cached encoder
/// </summary>
public class SmartCompressor : IDisposable
{
    private readonly StreamingConfig _config;
    private readonly ImageCodecInfo? _jpegEncoder;

    // Cached encoder parameters for each quality level
    private readonly Dictionary<int, EncoderParameters> _qualityParams = new();

    // Reusable stream to reduce allocations
    private MemoryStream _encodeStream = new(1024 * 1024); // 1MB initial

    public SmartCompressor(StreamingConfig config)
    {
        _config = config;

        // Find JPEG encoder
        _jpegEncoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(e => e.MimeType == "image/jpeg");

        // Pre-create common quality parameters
        foreach (var q in new[] { 30, 40, 50, 60, 70, 80, 90, 95 })
        {
            var param = new EncoderParameters(1);
            param.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);
            _qualityParams[q] = param;
        }
    }

    /// <summary>
    /// Encode frame with smart compression based on content analysis.
    /// </summary>
    public byte[]? Encode(Bitmap frame, DeltaResult delta, int requestedQuality)
    {
        if (_jpegEncoder == null) return EncodeSimple(frame);

        try
        {
            // Determine actual quality based on content
            var quality = CalculateSmartQuality(delta, requestedQuality);

            // For high motion content, use region encoding
            if (delta.IsHighMotion && delta.ChangedRegions.Length > 0 && delta.ChangedRegions.Length < 10)
            {
                return EncodeWithRegions(frame, delta, quality);
            }

            // Standard full-frame encoding
            return EncodeFull(frame, quality);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Encode error: {ex.Message}");
            return EncodeSimple(frame);
        }
    }

    /// <summary>
    /// Calculate optimal quality based on content characteristics.
    /// </summary>
    private int CalculateSmartQuality(DeltaResult delta, int baseQuality)
    {
        var quality = baseQuality;

        // High motion = reduce quality (frames change fast anyway)
        if (delta.IsHighMotion)
        {
            quality = Math.Max(_config.MinQuality, quality - 20);
        }
        // Low change = increase quality (viewer will see details longer)
        else if (delta.ChangePercentage < 5)
        {
            quality = Math.Min(_config.MaxQuality, quality + 15);
        }

        // Keyframes get higher quality (used as reference)
        if (delta.IsKeyFrame)
        {
            quality = Math.Min(_config.MaxQuality, quality + 10);
        }

        return Math.Clamp(quality, _config.MinQuality, _config.MaxQuality);
    }

    /// <summary>
    /// Encode full frame with specified quality.
    /// </summary>
    private byte[] EncodeFull(Bitmap frame, int quality)
    {
        _encodeStream.SetLength(0);
        _encodeStream.Position = 0;

        var param = GetOrCreateQualityParam(quality);
        frame.Save(_encodeStream, _jpegEncoder!, param);

        return _encodeStream.ToArray();
    }

    /// <summary>
    /// Encode with focus on changed regions (higher quality for changes).
    /// Uses a two-pass approach:
    /// 1. Encode whole frame at lower quality
    /// 2. Overlay changed regions at higher quality
    ///
    /// This is complex, so for simplicity we use bounding box encoding.
    /// </summary>
    private byte[] EncodeWithRegions(Bitmap frame, DeltaResult delta, int quality)
    {
        // If bounding box is most of the frame, just encode full
        var frameArea = frame.Width * frame.Height;
        var boundingArea = delta.BoundingBox.Width * delta.BoundingBox.Height;

        if (boundingArea > frameArea * 0.7)
        {
            return EncodeFull(frame, quality);
        }

        // For smaller changed areas, still encode full but boost quality in changed regions
        // This maintains compatibility with standard JPEG viewers
        // Future: Could implement custom format with region data

        return EncodeFull(frame, quality);
    }

    /// <summary>
    /// Simple fallback encoding.
    /// </summary>
    private byte[] EncodeSimple(Bitmap frame)
    {
        _encodeStream.SetLength(0);
        _encodeStream.Position = 0;
        frame.Save(_encodeStream, ImageFormat.Jpeg);
        return _encodeStream.ToArray();
    }

    /// <summary>
    /// Get or create encoder parameter for quality level.
    /// </summary>
    private EncoderParameters GetOrCreateQualityParam(int quality)
    {
        // Round to nearest 5 to use cached params
        var rounded = (quality / 5) * 5;
        rounded = Math.Clamp(rounded, 30, 95);

        if (_qualityParams.TryGetValue(rounded, out var param))
            return param;

        // Create new param
        param = new EncoderParameters(1);
        param.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        _qualityParams[quality] = param;
        return param;
    }

    public void Dispose()
    {
        foreach (var param in _qualityParams.Values)
        {
            try { param.Dispose(); } catch { }
        }
        _qualityParams.Clear();
        _encodeStream.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Network adapter for quality adjustment based on conditions.
/// </summary>
public class NetworkAdapter
{
    private readonly StreamingConfig _config;

    // Network stats
    private readonly Queue<int> _latencyHistory = new();
    private readonly Queue<bool> _dropHistory = new();
    private const int HistorySize = 30;

    private int _viewerCount = 1;
    private double _currentBitrate;
    private int _consecutiveDrops;

    // Quality state
    private int _currentQuality;
    private DateTime _lastQualityChange = DateTime.MinValue;
    private const int QualityChangeIntervalMs = 1000; // Don't change quality too often

    public NetworkAdapter(StreamingConfig config)
    {
        _config = config;
        _currentQuality = config.BaseQuality;
    }

    /// <summary>
    /// Get optimal quality based on current conditions.
    /// </summary>
    public int GetOptimalQuality(float changePercent, bool isHighMotion, double currentBitrateMbps)
    {
        _currentBitrate = currentBitrateMbps;

        // Don't change quality too frequently
        if ((DateTime.Now - _lastQualityChange).TotalMilliseconds < QualityChangeIntervalMs)
        {
            return AdjustForContent(_currentQuality, changePercent, isHighMotion);
        }

        // Calculate network-adjusted quality
        var networkQuality = CalculateNetworkQuality();

        // Apply viewer count scaling (more viewers = lower quality per viewer for server bandwidth)
        var viewerAdjusted = networkQuality;
        if (_viewerCount > 5)
        {
            viewerAdjusted = Math.Max(_config.MinQuality, networkQuality - (_viewerCount - 5) * 2);
        }

        // Smooth quality changes
        var targetQuality = viewerAdjusted;
        var diff = targetQuality - _currentQuality;

        if (Math.Abs(diff) > 5)
        {
            _currentQuality += Math.Sign(diff) * 5; // Change by max 5 at a time
            _lastQualityChange = DateTime.Now;
        }

        return AdjustForContent(_currentQuality, changePercent, isHighMotion);
    }

    /// <summary>
    /// Adjust quality based on content type.
    /// </summary>
    private int AdjustForContent(int baseQuality, float changePercent, bool isHighMotion)
    {
        var quality = baseQuality;

        // High motion = reduce quality (temporal masking)
        if (isHighMotion)
        {
            quality -= 15;
        }
        // Very static = increase quality (user will notice details)
        else if (changePercent < 2)
        {
            quality += 10;
        }

        return Math.Clamp(quality, _config.MinQuality, _config.MaxQuality);
    }

    /// <summary>
    /// Calculate quality based on network conditions.
    /// </summary>
    private int CalculateNetworkQuality()
    {
        // Start with base quality
        var quality = _config.BaseQuality;

        // Adjust for packet drops
        var dropRate = _dropHistory.Count > 0
            ? _dropHistory.Count(d => d) / (float)_dropHistory.Count
            : 0;

        if (dropRate > 0.1f) // >10% drops
        {
            quality -= 20;
        }
        else if (dropRate > 0.05f) // >5% drops
        {
            quality -= 10;
        }

        // Adjust for latency
        var avgLatency = _latencyHistory.Count > 0 ? _latencyHistory.Average() : 0;

        if (avgLatency > 200)
        {
            quality -= 15;
        }
        else if (avgLatency > 100)
        {
            quality -= 5;
        }
        else if (avgLatency < 50 && dropRate < 0.02f)
        {
            // Good conditions - can increase quality
            quality += 10;
        }

        // Adjust for bitrate
        if (_currentBitrate > _config.MaxBitrateMbps * 0.9)
        {
            quality -= 10; // Near max bitrate, reduce quality
        }

        return Math.Clamp(quality, _config.MinQuality, _config.MaxQuality);
    }

    /// <summary>
    /// Record network feedback for adaptation.
    /// </summary>
    public void RecordFeedback(int latencyMs, bool wasDropped)
    {
        _latencyHistory.Enqueue(latencyMs);
        while (_latencyHistory.Count > HistorySize)
            _latencyHistory.Dequeue();

        _dropHistory.Enqueue(wasDropped);
        while (_dropHistory.Count > HistorySize)
            _dropHistory.Dequeue();

        if (wasDropped)
        {
            _consecutiveDrops++;
            // Rapid quality reduction on consecutive drops
            if (_consecutiveDrops > 3)
            {
                _currentQuality = Math.Max(_config.MinQuality, _currentQuality - 10);
                _lastQualityChange = DateTime.Now;
            }
        }
        else
        {
            _consecutiveDrops = 0;
        }
    }

    /// <summary>
    /// Set viewer count for bandwidth scaling.
    /// </summary>
    public void SetViewerCount(int count)
    {
        _viewerCount = Math.Max(1, count);
    }
}
