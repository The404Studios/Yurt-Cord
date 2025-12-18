using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for caching images locally to improve performance and reduce network requests.
/// Images are stored in the user's local app data folder.
/// </summary>
public interface IImageCacheService
{
    Task<BitmapImage?> GetImageAsync(string imageUrl, bool forceRefresh = false);
    void ClearCache();
    void ClearExpiredCache(TimeSpan maxAge);
    long GetCacheSize();
}

public class ImageCacheService : IImageCacheService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly TimeSpan _defaultCacheExpiry = TimeSpan.FromDays(7);
    private static readonly object _cacheLock = new();

    // In-memory cache for frequently accessed images
    private readonly Dictionary<string, (BitmapImage image, DateTime cachedAt)> _memoryCache = new();
    private const int MaxMemoryCacheItems = 100;

    public ImageCacheService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Set up cache directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "Plugin", "ImageCache");

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Get an image from cache or download it
    /// </summary>
    public async Task<BitmapImage?> GetImageAsync(string imageUrl, bool forceRefresh = false)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        var cacheKey = GetCacheKey(imageUrl);

        // Check memory cache first
        if (!forceRefresh && _memoryCache.TryGetValue(cacheKey, out var memCached))
        {
            if (DateTime.Now - memCached.cachedAt < TimeSpan.FromMinutes(30))
            {
                return memCached.image;
            }
        }

        // Check disk cache
        var cachedFilePath = GetCachedFilePath(cacheKey);
        if (!forceRefresh && File.Exists(cachedFilePath))
        {
            var fileInfo = new FileInfo(cachedFilePath);
            if (DateTime.Now - fileInfo.LastWriteTime < _defaultCacheExpiry)
            {
                try
                {
                    var bitmap = LoadFromDiskCache(cachedFilePath);
                    if (bitmap != null)
                    {
                        AddToMemoryCache(cacheKey, bitmap);
                        return bitmap;
                    }
                }
                catch
                {
                    // Cache file corrupted, will re-download
                }
            }
        }

        // Download and cache
        try
        {
            var imageData = await _httpClient.GetByteArrayAsync(imageUrl);
            await SaveToDiskCacheAsync(cachedFilePath, imageData);

            var bitmap = CreateBitmapFromBytes(imageData);
            if (bitmap != null)
            {
                AddToMemoryCache(cacheKey, bitmap);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clear all cached images
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _memoryCache.Clear();

            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(_cacheDirectory))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Ignore errors during cache cleanup
            }
        }
    }

    /// <summary>
    /// Clear cached images older than the specified age
    /// </summary>
    public void ClearExpiredCache(TimeSpan maxAge)
    {
        lock (_cacheLock)
        {
            // Clear expired memory cache entries
            var expiredKeys = _memoryCache
                .Where(kvp => DateTime.Now - kvp.Value.cachedAt > maxAge)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _memoryCache.Remove(key);
            }

            // Clear expired disk cache entries
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    var cutoffTime = DateTime.Now - maxAge;
                    foreach (var file in Directory.GetFiles(_cacheDirectory))
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffTime)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during cache cleanup
            }
        }
    }

    /// <summary>
    /// Get total size of cached images in bytes
    /// </summary>
    public long GetCacheSize()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return 0;

            return Directory.GetFiles(_cacheDirectory)
                .Sum(file => new FileInfo(file).Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string GetCacheKey(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hashBytes);
    }

    private string GetCachedFilePath(string cacheKey)
    {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.cache");
    }

    private static BitmapImage? LoadFromDiskCache(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath);
        bitmap.EndInit();
        bitmap.Freeze(); // Make it thread-safe
        return bitmap;
    }

    private static async Task SaveToDiskCacheAsync(string filePath, byte[] data)
    {
        await File.WriteAllBytesAsync(filePath, data);
    }

    private static BitmapImage? CreateBitmapFromBytes(byte[] imageData)
    {
        using var ms = new MemoryStream(imageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze(); // Make it thread-safe
        return bitmap;
    }

    private void AddToMemoryCache(string key, BitmapImage image)
    {
        lock (_cacheLock)
        {
            // Evict oldest entries if cache is full
            if (_memoryCache.Count >= MaxMemoryCacheItems)
            {
                var oldest = _memoryCache
                    .OrderBy(kvp => kvp.Value.cachedAt)
                    .FirstOrDefault();
                if (oldest.Key != null)
                {
                    _memoryCache.Remove(oldest.Key);
                }
            }

            _memoryCache[key] = (image, DateTime.Now);
        }
    }
}
