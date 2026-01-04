using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides image optimization and resizing utilities to reduce memory usage
/// </summary>
public static class ImageOptimizationHelper
{
    private const int DefaultMaxWidth = 1920;
    private const int DefaultMaxHeight = 1080;
    private const int ThumbnailSize = 256;
    private const int AvatarSize = 128;

    /// <summary>
    /// Loads an image from a file path with optimization
    /// </summary>
    public static BitmapImage? LoadOptimizedImage(string imagePath, int maxWidth = DefaultMaxWidth, int maxHeight = DefaultMaxHeight)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                Debug.WriteLine($"Image file not found: {imagePath}");
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = maxWidth;
            bitmap.DecodePixelHeight = maxHeight;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // Makes it thread-safe and improves performance

            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load optimized image from {imagePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads an image from a URL with optimization
    /// </summary>
    public static BitmapImage? LoadOptimizedImageFromUrl(string imageUrl, int maxWidth = DefaultMaxWidth, int maxHeight = DefaultMaxHeight)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = maxWidth;
            bitmap.DecodePixelHeight = maxHeight;
            bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load optimized image from URL {imageUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a thumbnail version of an image
    /// </summary>
    public static BitmapImage? LoadThumbnail(string imagePath)
    {
        return LoadOptimizedImage(imagePath, ThumbnailSize, ThumbnailSize);
    }

    /// <summary>
    /// Loads an avatar-sized image
    /// </summary>
    public static BitmapImage? LoadAvatar(string imageUrl)
    {
        return LoadOptimizedImageFromUrl(imageUrl, AvatarSize, AvatarSize);
    }

    /// <summary>
    /// Resizes a BitmapSource to fit within maximum dimensions
    /// </summary>
    public static BitmapSource? ResizeImage(BitmapSource source, int maxWidth, int maxHeight)
    {
        try
        {
            if (source == null)
            {
                return null;
            }

            double scale = Math.Min(
                (double)maxWidth / source.PixelWidth,
                (double)maxHeight / source.PixelHeight
            );

            if (scale >= 1.0)
            {
                // No need to resize
                return source;
            }

            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);

            var transformedBitmap = new TransformedBitmap(source, new ScaleTransform(scale, scale));

            var resized = new RenderTargetBitmap(
                newWidth,
                newHeight,
                source.DpiX,
                source.DpiY,
                PixelFormats.Pbgra32
            );

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(transformedBitmap, new System.Windows.Rect(0, 0, newWidth, newHeight));
            }

            resized.Render(visual);
            resized.Freeze();

            return resized;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resize image: {ex.Message}");
            return source;
        }
    }

    /// <summary>
    /// Converts an image to JPEG format with specified quality
    /// </summary>
    public static byte[]? ConvertToJpeg(BitmapSource source, int quality = 85)
    {
        try
        {
            if (source == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = quality
            };

            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);

            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to convert image to JPEG: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts an image to PNG format
    /// </summary>
    public static byte[]? ConvertToPng(BitmapSource source)
    {
        try
        {
            if (source == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);

            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to convert image to PNG: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the optimal image size based on display size
    /// </summary>
    public static (int width, int height) GetOptimalSize(int displayWidth, int displayHeight, double devicePixelRatio = 1.0)
    {
        // Account for high-DPI displays
        int optimalWidth = (int)(displayWidth * devicePixelRatio);
        int optimalHeight = (int)(displayHeight * devicePixelRatio);

        // Cap at reasonable maximums to prevent excessive memory usage
        optimalWidth = Math.Min(optimalWidth, DefaultMaxWidth);
        optimalHeight = Math.Min(optimalHeight, DefaultMaxHeight);

        return (optimalWidth, optimalHeight);
    }

    /// <summary>
    /// Estimates memory usage of a bitmap
    /// </summary>
    public static long EstimateMemoryUsage(BitmapSource bitmap)
    {
        if (bitmap == null)
        {
            return 0;
        }

        // Rough estimate: width * height * bytes per pixel
        int bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
        return (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
    }

    /// <summary>
    /// Creates a placeholder image for failed loads
    /// </summary>
    public static BitmapSource CreatePlaceholder(int width, int height, Color backgroundColor)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                new SolidColorBrush(backgroundColor),
                null,
                new System.Windows.Rect(0, 0, width, height)
            );
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        return bitmap;
    }
}
