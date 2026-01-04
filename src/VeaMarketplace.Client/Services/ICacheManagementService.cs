using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

public class CacheStats
{
    public long TotalSizeBytes { get; set; }
    public int FileCount { get; set; }
    public DateTime? OldestFileDate { get; set; }
    public DateTime? NewestFileDate { get; set; }
    public Dictionary<string, long> SizeByCategory { get; set; } = new();
}

public interface ICacheManagementService
{
    Task<CacheStats> GetCacheStatsAsync();
    Task<long> ClearCacheAsync(TimeSpan? olderThan = null);
    Task<long> ClearCacheCategoryAsync(string category);
    Task<bool> ClearAllCachesAsync();
    Task<bool> OptimizeCacheAsync(long maxSizeBytes);
    Task<List<string>> GetCacheCategoriesAsync();
}

public class CacheManagementService : ICacheManagementService
{
    private readonly string _baseCachePath;
    private readonly object _lock = new();

    public CacheManagementService()
    {
        // Use the cache directory from settings or default
        _baseCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YurtCord",
            "Cache"
        );

        EnsureCacheDirectoryExists();
    }

    public async Task<CacheStats> GetCacheStatsAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return GetCacheStatsCore();
            }
        });
    }

    // Synchronous version for use within locked contexts (avoids deadlock)
    private CacheStats GetCacheStatsCore()
    {
        var stats = new CacheStats();

        if (!Directory.Exists(_baseCachePath))
        {
            return stats;
        }

        try
        {
            var allFiles = Directory.GetFiles(_baseCachePath, "*.*", SearchOption.AllDirectories);
            stats.FileCount = allFiles.Length;

            var fileInfos = allFiles.Select(f => new FileInfo(f)).ToList();

            stats.TotalSizeBytes = fileInfos.Sum(f => f.Length);
            stats.OldestFileDate = fileInfos.Any() ? fileInfos.Min(f => f.CreationTime) : null;
            stats.NewestFileDate = fileInfos.Any() ? fileInfos.Max(f => f.CreationTime) : null;

            // Calculate size by category (subdirectories)
            var categories = Directory.GetDirectories(_baseCachePath);
            foreach (var category in categories)
            {
                var categoryName = Path.GetFileName(category);
                var categoryFiles = Directory.GetFiles(category, "*.*", SearchOption.AllDirectories);
                var categorySize = categoryFiles.Sum(f => new FileInfo(f).Length);
                stats.SizeByCategory[categoryName] = categorySize;
            }

            Debug.WriteLine($"Cache stats: {stats.FileCount} files, {FormatBytes(stats.TotalSizeBytes)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get cache stats: {ex.Message}");
        }

        return stats;
    }

    public async Task<long> ClearCacheAsync(TimeSpan? olderThan = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                long bytesCleared = 0;

                if (!Directory.Exists(_baseCachePath))
                {
                    return bytesCleared;
                }

                try
                {
                    var cutoffDate = olderThan.HasValue
                        ? DateTime.Now - olderThan.Value
                        : DateTime.MaxValue;

                    var allFiles = Directory.GetFiles(_baseCachePath, "*.*", SearchOption.AllDirectories);

                    foreach (var file in allFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);

                            if (fileInfo.LastAccessTime < cutoffDate || !olderThan.HasValue)
                            {
                                var fileSize = fileInfo.Length;
                                File.Delete(file);
                                bytesCleared += fileSize;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to delete cache file {file}: {ex.Message}");
                        }
                    }

                    // Clean up empty directories
                    CleanupEmptyDirectories(_baseCachePath);

                    Debug.WriteLine($"Cleared {FormatBytes(bytesCleared)} from cache");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clear cache: {ex.Message}");
                }

                return bytesCleared;
            }
        });
    }

    public async Task<long> ClearCacheCategoryAsync(string category)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                long bytesCleared = 0;
                var categoryPath = Path.Combine(_baseCachePath, category);

                if (!Directory.Exists(categoryPath))
                {
                    return bytesCleared;
                }

                try
                {
                    var files = Directory.GetFiles(categoryPath, "*.*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        try
                        {
                            var fileSize = new FileInfo(file).Length;
                            File.Delete(file);
                            bytesCleared += fileSize;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to delete file {file}: {ex.Message}");
                        }
                    }

                    // Remove the category directory if empty
                    try
                    {
                        Directory.Delete(categoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete category directory: {ex.Message}");
                    }

                    Debug.WriteLine($"Cleared {FormatBytes(bytesCleared)} from category '{category}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clear cache category '{category}': {ex.Message}");
                }

                return bytesCleared;
            }
        });
    }

    public async Task<bool> ClearAllCachesAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    if (Directory.Exists(_baseCachePath))
                    {
                        Directory.Delete(_baseCachePath, true);
                        EnsureCacheDirectoryExists();
                        Debug.WriteLine("All caches cleared successfully");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clear all caches: {ex.Message}");
                }

                return false;
            }
        });
    }

    public async Task<bool> OptimizeCacheAsync(long maxSizeBytes)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    var stats = GetCacheStatsCore();

                    if (stats.TotalSizeBytes <= maxSizeBytes)
                    {
                        Debug.WriteLine("Cache size is within limits, no optimization needed");
                        return true;
                    }

                    long bytesToRemove = stats.TotalSizeBytes - maxSizeBytes;
                    long bytesRemoved = 0;

                    // Get all files sorted by last access time (oldest first)
                    var allFiles = Directory.GetFiles(_baseCachePath, "*.*", SearchOption.AllDirectories)
                        .Select(f => new FileInfo(f))
                        .OrderBy(f => f.LastAccessTime)
                        .ToList();

                    foreach (var file in allFiles)
                    {
                        if (bytesRemoved >= bytesToRemove)
                        {
                            break;
                        }

                        try
                        {
                            var fileSize = file.Length;
                            file.Delete();
                            bytesRemoved += fileSize;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to delete file during optimization: {ex.Message}");
                        }
                    }

                    CleanupEmptyDirectories(_baseCachePath);

                    Debug.WriteLine($"Cache optimized: removed {FormatBytes(bytesRemoved)}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to optimize cache: {ex.Message}");
                    return false;
                }
            }
        });
    }

    public async Task<List<string>> GetCacheCategoriesAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var categories = new List<string>();

                if (!Directory.Exists(_baseCachePath))
                {
                    return categories;
                }

                try
                {
                    var directories = Directory.GetDirectories(_baseCachePath);
                    categories.AddRange(directories.Select(Path.GetFileName).Where(d => d != null)!);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to get cache categories: {ex.Message}");
                }

                return categories;
            }
        });
    }

    private void EnsureCacheDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_baseCachePath))
            {
                Directory.CreateDirectory(_baseCachePath);
                Debug.WriteLine($"Cache directory created: {_baseCachePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create cache directory: {ex.Message}");
        }
    }

    private void CleanupEmptyDirectories(string path)
    {
        try
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                CleanupEmptyDirectories(directory);

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    try
                    {
                        Directory.Delete(directory);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete empty directory {directory}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to cleanup empty directories: {ex.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
