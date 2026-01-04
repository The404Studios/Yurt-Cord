using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Compression level for file operations
/// </summary>
public enum CompressionQuality
{
    Fastest = CompressionLevel.Fastest,
    Optimal = CompressionLevel.Optimal,
    SmallestSize = CompressionLevel.SmallestSize
}

/// <summary>
/// Compression result information
/// </summary>
public class CompressionResult
{
    public bool Success { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Provides file compression and decompression utilities
/// </summary>
public static class FileCompressionHelper
{
    /// <summary>
    /// Compresses a file using GZip compression
    /// </summary>
    public static async Task<CompressionResult> CompressFileAsync(
        string sourceFilePath,
        string destinationFilePath,
        CompressionQuality quality = CompressionQuality.Optimal)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CompressionResult();

        try
        {
            if (!File.Exists(sourceFilePath))
            {
                return new CompressionResult
                {
                    Success = false,
                    ErrorMessage = "Source file does not exist"
                };
            }

            var sourceFileInfo = new FileInfo(sourceFilePath);
            result.OriginalSize = sourceFileInfo.Length;

            using (var sourceStream = File.OpenRead(sourceFilePath))
            using (var destinationStream = File.Create(destinationFilePath))
            using (var compressionStream = new GZipStream(destinationStream, (CompressionLevel)quality))
            {
                await sourceStream.CopyToAsync(compressionStream);
            }

            var compressedFileInfo = new FileInfo(destinationFilePath);
            result.CompressedSize = compressedFileInfo.Length;
            result.CompressionRatio = (double)result.CompressedSize / result.OriginalSize;
            result.Success = true;

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            Debug.WriteLine($"Compressed {sourceFilePath} -> {destinationFilePath}: " +
                          $"{FormatBytes(result.OriginalSize)} -> {FormatBytes(result.CompressedSize)} " +
                          $"({result.CompressionRatio:P1}) in {result.Duration.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            Debug.WriteLine($"Failed to compress file: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Decompresses a GZip compressed file
    /// </summary>
    public static async Task<CompressionResult> DecompressFileAsync(
        string sourceFilePath,
        string destinationFilePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CompressionResult();

        try
        {
            if (!File.Exists(sourceFilePath))
            {
                return new CompressionResult
                {
                    Success = false,
                    ErrorMessage = "Source file does not exist"
                };
            }

            var sourceFileInfo = new FileInfo(sourceFilePath);
            result.CompressedSize = sourceFileInfo.Length;

            using (var sourceStream = File.OpenRead(sourceFilePath))
            using (var decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
            using (var destinationStream = File.Create(destinationFilePath))
            {
                await decompressionStream.CopyToAsync(destinationStream);
            }

            var decompressedFileInfo = new FileInfo(destinationFilePath);
            result.OriginalSize = decompressedFileInfo.Length;
            result.CompressionRatio = (double)result.CompressedSize / result.OriginalSize;
            result.Success = true;

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            Debug.WriteLine($"Decompressed {sourceFilePath} -> {destinationFilePath}: " +
                          $"{FormatBytes(result.CompressedSize)} -> {FormatBytes(result.OriginalSize)} " +
                          $"in {result.Duration.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            Debug.WriteLine($"Failed to decompress file: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Compresses byte array using GZip
    /// </summary>
    public static async Task<byte[]?> CompressBytesAsync(
        byte[] data,
        CompressionQuality quality = CompressionQuality.Optimal)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using (var compressionStream = new GZipStream(memoryStream, (CompressionLevel)quality))
            {
                await compressionStream.WriteAsync(data, 0, data.Length);
            }

            var compressed = memoryStream.ToArray();

            Debug.WriteLine($"Compressed bytes: {FormatBytes(data.Length)} -> {FormatBytes(compressed.Length)} " +
                          $"({(double)compressed.Length / data.Length:P1})");

            return compressed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to compress bytes: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Decompresses byte array from GZip
    /// </summary>
    public static async Task<byte[]?> DecompressBytesAsync(byte[] compressedData)
    {
        try
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();

            await decompressionStream.CopyToAsync(resultStream);

            var decompressed = resultStream.ToArray();

            Debug.WriteLine($"Decompressed bytes: {FormatBytes(compressedData.Length)} -> {FormatBytes(decompressed.Length)}");

            return decompressed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to decompress bytes: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Compresses a directory into a zip archive
    /// </summary>
    public static async Task<bool> CompressDirectoryAsync(
        string sourceDirectory,
        string destinationZipPath,
        CompressionQuality quality = CompressionQuality.Optimal)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(sourceDirectory))
                {
                    Debug.WriteLine($"Source directory does not exist: {sourceDirectory}");
                    return false;
                }

                // Delete existing zip if it exists
                if (File.Exists(destinationZipPath))
                {
                    File.Delete(destinationZipPath);
                }

                var stopwatch = Stopwatch.StartNew();

                ZipFile.CreateFromDirectory(
                    sourceDirectory,
                    destinationZipPath,
                    (CompressionLevel)quality,
                    includeBaseDirectory: false
                );

                stopwatch.Stop();

                var zipSize = new FileInfo(destinationZipPath).Length;

                Debug.WriteLine($"Compressed directory {sourceDirectory} to {destinationZipPath}: " +
                              $"{FormatBytes(zipSize)} in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to compress directory: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Extracts a zip archive to a directory
    /// </summary>
    public static async Task<bool> ExtractZipAsync(
        string sourceZipPath,
        string destinationDirectory,
        bool overwrite = true)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(sourceZipPath))
                {
                    Debug.WriteLine($"Source zip file does not exist: {sourceZipPath}");
                    return false;
                }

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                var stopwatch = Stopwatch.StartNew();

                ZipFile.ExtractToDirectory(sourceZipPath, destinationDirectory, overwrite);

                stopwatch.Stop();

                Debug.WriteLine($"Extracted {sourceZipPath} to {destinationDirectory} " +
                              $"in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to extract zip: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Estimates compression ratio for a file (sample-based for large files)
    /// </summary>
    public static async Task<double> EstimateCompressionRatioAsync(
        string filePath,
        int sampleSizeBytes = 1024 * 1024) // 1MB sample
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return 1.0;
            }

            var fileInfo = new FileInfo(filePath);
            var bytesToRead = Math.Min(sampleSizeBytes, fileInfo.Length);

            byte[] sample = new byte[bytesToRead];

            using (var fileStream = File.OpenRead(filePath))
            {
                await fileStream.ReadAsync(sample, 0, (int)bytesToRead);
            }

            var compressed = await CompressBytesAsync(sample);

            if (compressed == null)
            {
                return 1.0;
            }

            return (double)compressed.Length / sample.Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to estimate compression ratio: {ex.Message}");
            return 1.0;
        }
    }

    /// <summary>
    /// Checks if a file is likely already compressed
    /// </summary>
    public static bool IsLikelyCompressed(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var compressedExtensions = new[]
        {
            ".zip", ".gz", ".7z", ".rar", ".tar",
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".mp3", ".mp4", ".mkv", ".avi", ".flv",
            ".pdf", ".docx", ".xlsx", ".pptx"
        };

        return Array.Exists(compressedExtensions, ext => ext == extension);
    }

    /// <summary>
    /// Formats byte count for display
    /// </summary>
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
