using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VeaMarketplace.Client.Services.Streaming;

/// <summary>
/// Delta frame encoder - detects and encodes only changed screen regions.
///
/// Algorithm:
/// 1. Divide frame into NxN blocks (default 16x16)
/// 2. Compare block checksums with previous frame
/// 3. Mark changed blocks and compute bounding box
/// 4. Detect motion level (high change = video/animation)
/// 5. Return delta info for smart encoding
///
/// Benefits:
/// - 50-90% bandwidth savings for static content
/// - Fast block-based comparison (no pixel-by-pixel)
/// - Motion detection for adaptive quality
/// </summary>
public class DeltaFrameEncoder : IDisposable
{
    private readonly StreamingConfig _config;
    private readonly FrameBufferPool _bufferPool;

    // Previous frame data for comparison
    private byte[]? _previousFrameData;
    private int _previousWidth;
    private int _previousHeight;

    // Block checksums for fast comparison
    private uint[]? _previousBlockChecksums;
    private int _blocksX;
    private int _blocksY;

    // Keyframe control
    private int _framesSinceKeyframe;
    private bool _forceKeyFrame;

    // Motion detection
    private readonly Queue<float> _changeHistory = new();
    private const int MotionHistorySize = 10;
    private const float HighMotionThreshold = 15f; // 15% change = high motion

    public DeltaFrameEncoder(StreamingConfig config, FrameBufferPool bufferPool)
    {
        _config = config;
        _bufferPool = bufferPool;
    }

    /// <summary>
    /// Compute delta between current frame and previous frame.
    /// </summary>
    public DeltaResult ComputeDelta(Bitmap frame, int frameNumber)
    {
        var result = new DeltaResult();

        // Lock bitmap for fast access
        var bitmapData = frame.LockBits(
            new System.Drawing.Rectangle(0, 0, frame.Width, frame.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            var stride = bitmapData.Stride;
            var dataLength = stride * frame.Height;
            var currentData = new byte[dataLength];
            Marshal.Copy(bitmapData.Scan0, currentData, 0, dataLength);

            // Check if keyframe is needed
            result.IsKeyFrame = ShouldSendKeyFrame(frame.Width, frame.Height);

            if (result.IsKeyFrame || _previousFrameData == null)
            {
                // Keyframe: everything changed
                result.ChangePercentage = 100f;
                result.BoundingBox = new System.Drawing.Rectangle(0, 0, frame.Width, frame.Height);
                result.ChangedRegions = new[] { result.BoundingBox };
            }
            else
            {
                // Delta frame: compute changes
                ComputeBlockChanges(currentData, frame.Width, frame.Height, stride, result);
            }

            // Update motion detection
            UpdateMotionDetection(result.ChangePercentage);
            result.IsHighMotion = IsHighMotion();

            // Store current frame for next comparison
            _previousFrameData = currentData;
            _previousWidth = frame.Width;
            _previousHeight = frame.Height;
            _framesSinceKeyframe++;

            return result;
        }
        finally
        {
            frame.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// Block-based change detection using checksums.
    /// Much faster than pixel comparison.
    /// </summary>
    private void ComputeBlockChanges(byte[] currentData, int width, int height, int stride, DeltaResult result)
    {
        var blockSize = _config.BlockSize;
        var newBlocksX = (width + blockSize - 1) / blockSize;
        var newBlocksY = (height + blockSize - 1) / blockSize;
        var totalBlocks = newBlocksX * newBlocksY;

        // Compute current block checksums
        var currentChecksums = new uint[totalBlocks];
        var changedBlocks = new bool[totalBlocks];
        var changedCount = 0;

        // Bounds tracking
        int minX = width, minY = height, maxX = 0, maxY = 0;

        for (int by = 0; by < newBlocksY; by++)
        {
            for (int bx = 0; bx < newBlocksX; bx++)
            {
                var blockIndex = by * newBlocksX + bx;
                var checksum = ComputeBlockChecksum(currentData, width, height, stride, bx, by, blockSize);
                currentChecksums[blockIndex] = checksum;

                // Compare with previous
                bool changed = _previousBlockChecksums == null ||
                               blockIndex >= _previousBlockChecksums.Length ||
                               _previousBlockChecksums[blockIndex] != checksum;

                if (changed)
                {
                    changedBlocks[blockIndex] = true;
                    changedCount++;

                    // Update bounds
                    var blockX = bx * blockSize;
                    var blockY = by * blockSize;
                    minX = Math.Min(minX, blockX);
                    minY = Math.Min(minY, blockY);
                    maxX = Math.Max(maxX, Math.Min(blockX + blockSize, width));
                    maxY = Math.Max(maxY, Math.Min(blockY + blockSize, height));
                }
            }
        }

        // Store checksums for next frame
        _previousBlockChecksums = currentChecksums;
        _blocksX = newBlocksX;
        _blocksY = newBlocksY;

        // Calculate results
        result.ChangePercentage = (changedCount * 100f) / totalBlocks;

        if (changedCount > 0)
        {
            result.BoundingBox = new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
            result.ChangedRegions = MergeChangedRegions(changedBlocks, newBlocksX, newBlocksY, blockSize, width, height);
        }
        else
        {
            result.BoundingBox = System.Drawing.Rectangle.Empty;
            result.ChangedRegions = Array.Empty<System.Drawing.Rectangle>();
        }
    }

    /// <summary>
    /// Fast block checksum using FNV-1a hash.
    /// </summary>
    private uint ComputeBlockChecksum(byte[] data, int width, int height, int stride, int blockX, int blockY, int blockSize)
    {
        const uint FnvPrime = 16777619;
        const uint FnvOffset = 2166136261;

        uint hash = FnvOffset;

        var startX = blockX * blockSize;
        var startY = blockY * blockSize;
        var endX = Math.Min(startX + blockSize, width);
        var endY = Math.Min(startY + blockSize, height);

        // Sample every 4th pixel for speed (still detects changes well)
        for (var y = startY; y < endY; y += 2)
        {
            var rowOffset = y * stride;
            for (var x = startX; x < endX; x += 2)
            {
                var pixelOffset = rowOffset + x * 3;
                if (pixelOffset + 2 < data.Length)
                {
                    // Hash RGB values
                    hash ^= data[pixelOffset];
                    hash *= FnvPrime;
                    hash ^= data[pixelOffset + 1];
                    hash *= FnvPrime;
                    hash ^= data[pixelOffset + 2];
                    hash *= FnvPrime;
                }
            }
        }

        return hash;
    }

    /// <summary>
    /// Merge adjacent changed blocks into larger rectangles.
    /// Reduces encoding overhead.
    /// </summary>
    private Rectangle[] MergeChangedRegions(bool[] changedBlocks, int blocksX, int blocksY, int blockSize, int width, int height)
    {
        var regions = new List<Rectangle>();
        var visited = new bool[changedBlocks.Length];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                var idx = by * blocksX + bx;
                if (!changedBlocks[idx] || visited[idx]) continue;

                // Flood fill to find connected region
                int maxBx = bx, maxBy = by;

                // Expand horizontally
                while (maxBx + 1 < blocksX && changedBlocks[by * blocksX + maxBx + 1] && !visited[by * blocksX + maxBx + 1])
                    maxBx++;

                // Expand vertically (checking entire row)
                bool canExpand = true;
                while (canExpand && maxBy + 1 < blocksY)
                {
                    for (int x = bx; x <= maxBx; x++)
                    {
                        var checkIdx = (maxBy + 1) * blocksX + x;
                        if (!changedBlocks[checkIdx] || visited[checkIdx])
                        {
                            canExpand = false;
                            break;
                        }
                    }
                    if (canExpand) maxBy++;
                }

                // Mark as visited and add region
                for (int y = by; y <= maxBy; y++)
                {
                    for (int x = bx; x <= maxBx; x++)
                    {
                        visited[y * blocksX + x] = true;
                    }
                }

                var rect = new System.Drawing.Rectangle(
                    bx * blockSize,
                    by * blockSize,
                    Math.Min((maxBx - bx + 1) * blockSize, width - bx * blockSize),
                    Math.Min((maxBy - by + 1) * blockSize, height - by * blockSize)
                );
                regions.Add(rect);
            }
        }

        return regions.ToArray();
    }

    private bool ShouldSendKeyFrame(int width, int height)
    {
        // Force keyframe if requested
        if (_forceKeyFrame)
        {
            _forceKeyFrame = false;
            _framesSinceKeyframe = 0;
            return true;
        }

        // Keyframe if resolution changed
        if (width != _previousWidth || height != _previousHeight)
        {
            _framesSinceKeyframe = 0;
            return true;
        }

        // Periodic keyframe
        if (_framesSinceKeyframe >= _config.KeyFrameInterval)
        {
            _framesSinceKeyframe = 0;
            return true;
        }

        return false;
    }

    private void UpdateMotionDetection(float changePercent)
    {
        _changeHistory.Enqueue(changePercent);
        while (_changeHistory.Count > MotionHistorySize)
            _changeHistory.Dequeue();
    }

    private bool IsHighMotion()
    {
        if (_changeHistory.Count < 3) return false;
        return _changeHistory.Average() > HighMotionThreshold;
    }

    public void RequestKeyFrame()
    {
        _forceKeyFrame = true;
    }

    public void Reset()
    {
        _previousFrameData = null;
        _previousBlockChecksums = null;
        _framesSinceKeyframe = 0;
        _changeHistory.Clear();
    }

    public void Dispose()
    {
        _previousFrameData = null;
        _previousBlockChecksums = null;
    }
}
