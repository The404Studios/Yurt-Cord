using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Caches thumbnail previews of active screen shares for UI display.
/// Provides low-bandwidth preview images (~5 KB vs 100+ KB for full frames).
/// </summary>
public class ScreenShareThumbnailCache : IDisposable
{
    private readonly ConcurrentDictionary<string, ThumbnailInfo> _cache = new();

    // Configuration
    private const int ThumbnailWidth = 320;
    private const int ThumbnailHeight = 180;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _expirationTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Information about a cached thumbnail
    /// </summary>
    public class ThumbnailInfo
    {
        public BitmapSource Thumbnail { get; set; } = null!;
        public DateTime LastUpdated { get; set; }
        public string SharerUsername { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Event fired when a thumbnail is updated
    /// </summary>
    public event Action<string, BitmapSource>? OnThumbnailUpdated;

    /// <summary>
    /// Updates or creates a thumbnail for a screen share.
    /// Rate-limited to avoid excessive updates.
    /// </summary>
    public void UpdateThumbnail(string sharerConnectionId, BitmapSource fullFrame,
        string sharerUsername = "", string channelId = "")
    {
        if (fullFrame == null)
            return;

        // Check if update is needed (rate limiting)
        if (_cache.TryGetValue(sharerConnectionId, out var existing))
        {
            if (DateTime.UtcNow - existing.LastUpdated < _updateInterval)
                return; // Too soon, skip update
        }

        try
        {
            // Create thumbnail by scaling down
            var thumbnail = CreateThumbnail(fullFrame);

            if (thumbnail != null)
            {
                var info = new ThumbnailInfo
                {
                    Thumbnail = thumbnail,
                    LastUpdated = DateTime.UtcNow,
                    SharerUsername = sharerUsername,
                    ChannelId = channelId,
                    Width = fullFrame.PixelWidth,
                    Height = fullFrame.PixelHeight,
                    IsActive = true
                };

                _cache[sharerConnectionId] = info;

                // Fire event for UI updates
                OnThumbnailUpdated?.Invoke(sharerConnectionId, thumbnail);

                Debug.WriteLine($"Updated thumbnail for {sharerConnectionId} ({sharerUsername})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create thumbnail: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a cached thumbnail for a screen share.
    /// Returns null if not cached or expired.
    /// </summary>
    public ThumbnailInfo? GetThumbnail(string sharerConnectionId)
    {
        if (_cache.TryGetValue(sharerConnectionId, out var info))
        {
            // Check if expired
            if (DateTime.UtcNow - info.LastUpdated > _expirationTime)
            {
                _cache.TryRemove(sharerConnectionId, out _);
                return null;
            }

            return info;
        }

        return null;
    }

    /// <summary>
    /// Gets all active thumbnails
    /// </summary>
    public IEnumerable<ThumbnailInfo> GetAllThumbnails()
    {
        // Clean up expired thumbnails
        var expired = _cache.Where(kvp =>
            DateTime.UtcNow - kvp.Value.LastUpdated > _expirationTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
        {
            _cache.TryRemove(key, out _);
        }

        return _cache.Values.Where(t => t.IsActive).ToList();
    }

    /// <summary>
    /// Marks a screen share as stopped and removes its thumbnail
    /// </summary>
    public void RemoveThumbnail(string sharerConnectionId)
    {
        if (_cache.TryRemove(sharerConnectionId, out var info))
        {
            Debug.WriteLine($"Removed thumbnail for {sharerConnectionId}");
        }
    }

    /// <summary>
    /// Creates a scaled-down thumbnail from a full frame
    /// </summary>
    private BitmapSource? CreateThumbnail(BitmapSource fullFrame)
    {
        try
        {
            // Calculate scale to maintain aspect ratio
            double scaleX = (double)ThumbnailWidth / fullFrame.PixelWidth;
            double scaleY = (double)ThumbnailHeight / fullFrame.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Create scaled bitmap
            var transform = new ScaleTransform(scale, scale);
            var thumbnail = new TransformedBitmap(fullFrame, transform);

            // Freeze for cross-thread use
            thumbnail.Freeze();

            return thumbnail;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateThumbnail error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears all cached thumbnails
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Debug.WriteLine("Cleared all thumbnails");
    }

    /// <summary>
    /// Gets thumbnail cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        var now = DateTime.UtcNow;
        var thumbnails = _cache.Values.ToList();

        return new CacheStats
        {
            TotalThumbnails = thumbnails.Count,
            ActiveThumbnails = thumbnails.Count(t => t.IsActive),
            ExpiredThumbnails = thumbnails.Count(t => now - t.LastUpdated > _expirationTime),
            OldestThumbnail = thumbnails.Any()
                ? thumbnails.Min(t => t.LastUpdated)
                : (DateTime?)null,
            NewestThumbnail = thumbnails.Any()
                ? thumbnails.Max(t => t.LastUpdated)
                : (DateTime?)null
        };
    }

    public class CacheStats
    {
        public int TotalThumbnails { get; set; }
        public int ActiveThumbnails { get; set; }
        public int ExpiredThumbnails { get; set; }
        public DateTime? OldestThumbnail { get; set; }
        public DateTime? NewestThumbnail { get; set; }
    }

    public void Dispose()
    {
        Clear();
    }
}
