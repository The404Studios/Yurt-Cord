using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using DrawingRectangle = System.Drawing.Rectangle;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Detects motion/changes between video frames for optimized encoding.
/// Only encodes changed regions, dramatically reducing bandwidth and CPU usage
/// for static or mostly-static content (code editors, documents, etc.).
/// </summary>
public class MotionDetector : IDisposable
{
    private byte[]? _previousFrameHash;
    private DateTime _lastFullFrameTime = DateTime.MinValue;
    private int _framesSinceFullFrame;

    // Configuration
    private const int BlockSize = 16; // 16x16 pixel blocks for motion detection
    private const int SensitivityThreshold = 15; // 0-255, lower = more sensitive
    private const int FullFrameInterval = 120; // Send full frame every 120 frames (2 seconds at 60 FPS)
    private const double MinChangeRatio = 0.02; // Minimum 2% of blocks must change to send frame

    /// <summary>
    /// Statistics about motion detection
    /// </summary>
    public class MotionStats
    {
        public int TotalBlocks { get; set; }
        public int ChangedBlocks { get; set; }
        public double ChangePercentage { get; set; }
        public bool IsFullFrame { get; set; }
        public bool IsSkipped { get; set; }
    }

    /// <summary>
    /// Analyzes a frame and returns changed regions that need encoding.
    /// Returns null if frame should be skipped (no significant changes).
    /// </summary>
    public (List<DrawingRectangle> ChangedRegions, MotionStats Stats)? DetectChanges(Bitmap currentFrame)
    {
        if (currentFrame == null)
            return null;

        var width = currentFrame.Width;
        var height = currentFrame.Height;
        var blocksX = (width + BlockSize - 1) / BlockSize;
        var blocksY = (height + BlockSize - 1) / BlockSize;
        var totalBlocks = blocksX * blocksY;

        // Get hash of current frame (fast comparison)
        var currentHash = ComputeFrameBlockHashes(currentFrame, blocksX, blocksY);

        _framesSinceFullFrame++;

        // Force full frame periodically for error recovery
        var forceFullFrame = _framesSinceFullFrame >= FullFrameInterval ||
                            _previousFrameHash == null ||
                            (DateTime.UtcNow - _lastFullFrameTime).TotalSeconds > 5;

        if (forceFullFrame)
        {
            _previousFrameHash = currentHash;
            _framesSinceFullFrame = 0;
            _lastFullFrameTime = DateTime.UtcNow;

            return (new List<DrawingRectangle> { new DrawingRectangle(0, 0, width, height) },
                    new MotionStats
                    {
                        TotalBlocks = totalBlocks,
                        ChangedBlocks = totalBlocks,
                        ChangePercentage = 100.0,
                        IsFullFrame = true,
                        IsSkipped = false
                    });
        }

        // Detect changed blocks
        var changedBlocks = new List<DrawingRectangle>();
        int changedCount = 0;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int blockIdx = by * blocksX + bx;

                // Compare current block hash with previous
                if (Math.Abs(currentHash[blockIdx] - _previousFrameHash[blockIdx]) > SensitivityThreshold)
                {
                    // Block changed - add to changed regions
                    int x = bx * BlockSize;
                    int y = by * BlockSize;
                    int blockWidth = Math.Min(BlockSize, width - x);
                    int blockHeight = Math.Min(BlockSize, height - y);

                    changedBlocks.Add(new DrawingRectangle(x, y, blockWidth, blockHeight));
                    changedCount++;
                }
            }
        }

        _previousFrameHash = currentHash;

        var changePercentage = (double)changedCount / totalBlocks * 100.0;

        // Skip frame if changes are too small (noise, minor variations)
        if (changePercentage < MinChangeRatio * 100)
        {
            return (new List<DrawingRectangle>(),
                    new MotionStats
                    {
                        TotalBlocks = totalBlocks,
                        ChangedBlocks = changedCount,
                        ChangePercentage = changePercentage,
                        IsFullFrame = false,
                        IsSkipped = true
                    });
        }

        // Merge adjacent changed blocks for more efficient encoding
        var mergedRegions = MergeAdjacentBlocks(changedBlocks, width, height);

        return (mergedRegions,
                new MotionStats
                {
                    TotalBlocks = totalBlocks,
                    ChangedBlocks = changedCount,
                    ChangePercentage = changePercentage,
                    IsFullFrame = false,
                    IsSkipped = false
                });
    }

    /// <summary>
    /// Computes a hash (average luminance) for each block in the frame.
    /// This is much faster than pixel-by-pixel comparison.
    /// </summary>
    private byte[] ComputeFrameBlockHashes(Bitmap frame, int blocksX, int blocksY)
    {
        var hashes = new byte[blocksX * blocksY];

        var bitmapData = frame.LockBits(
            new DrawingRectangle(0, 0, frame.Width, frame.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;
                int stride = bitmapData.Stride;

                for (int by = 0; by < blocksY; by++)
                {
                    for (int bx = 0; bx < blocksX; bx++)
                    {
                        // Calculate average luminance for this block
                        long sum = 0;
                        int pixelCount = 0;

                        int startX = bx * BlockSize;
                        int startY = by * BlockSize;
                        int endX = Math.Min(startX + BlockSize, frame.Width);
                        int endY = Math.Min(startY + BlockSize, frame.Height);

                        for (int y = startY; y < endY; y++)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                int idx = y * stride + x * 3;
                                // Luminance = 0.299*R + 0.587*G + 0.114*B (fast integer approximation)
                                sum += (ptr[idx + 2] * 299 + ptr[idx + 1] * 587 + ptr[idx] * 114) / 1000;
                                pixelCount++;
                            }
                        }

                        hashes[by * blocksX + bx] = (byte)(sum / Math.Max(1, pixelCount));
                    }
                }
            }
        }
        finally
        {
            frame.UnlockBits(bitmapData);
        }

        return hashes;
    }

    /// <summary>
    /// Merges adjacent changed blocks into larger rectangles for more efficient encoding.
    /// Example: 4 adjacent 16x16 blocks become one 32x32 block.
    /// </summary>
    private List<DrawingRectangle> MergeAdjacentBlocks(List<DrawingRectangle> blocks, int frameWidth, int frameHeight)
    {
        if (blocks.Count == 0)
            return blocks;

        // Simple horizontal merge (can be extended to 2D merge for better results)
        var merged = new List<DrawingRectangle>();
        blocks.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

        DrawingRectangle? current = null;

        foreach (var block in blocks)
        {
            if (current == null)
            {
                current = block;
                continue;
            }

            // Try to merge horizontally if on same row and adjacent
            if (current.Value.Y == block.Y &&
                current.Value.Right == block.X &&
                current.Value.Height == block.Height)
            {
                // Merge by extending width
                current = new DrawingRectangle(
                    current.Value.X,
                    current.Value.Y,
                    current.Value.Width + block.Width,
                    current.Value.Height);
            }
            else
            {
                // Can't merge - add current and start new
                merged.Add(current.Value);
                current = block;
            }
        }

        if (current != null)
            merged.Add(current.Value);

        return merged;
    }

    /// <summary>
    /// Resets motion detection state (e.g., when switching displays or quality).
    /// </summary>
    public void Reset()
    {
        _previousFrameHash = null;
        _framesSinceFullFrame = 0;
        _lastFullFrameTime = DateTime.MinValue;
    }

    public void Dispose()
    {
        _previousFrameHash = null;
    }
}
