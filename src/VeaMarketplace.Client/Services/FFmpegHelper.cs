using System.Diagnostics;
using System.IO;
using FFmpeg.AutoGen;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Shared FFmpeg initialization and availability checking.
/// Used by both encoder and decoder to ensure codec compatibility.
/// </summary>
public static class FFmpegHelper
{
    private static bool _initialized;
    private static bool _isAvailable;
    private static readonly object _lock = new();
    private static string? _ffmpegPath;

    /// <summary>
    /// Gets whether FFmpeg is available for encoding/decoding.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _isAvailable;
        }
    }

    /// <summary>
    /// Gets the path where FFmpeg binaries were found.
    /// </summary>
    public static string? FFmpegPath => _ffmpegPath;

    /// <summary>
    /// Initialize FFmpeg and check availability.
    /// Safe to call multiple times.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                // Set FFmpeg binaries path - look in app directory first
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var ffmpegPath = Path.Combine(appDir, "ffmpeg");

                if (Directory.Exists(ffmpegPath))
                {
                    ffmpeg.RootPath = ffmpegPath;
                    _ffmpegPath = ffmpegPath;
                }
                else
                {
                    // Try common installation paths
                    var commonPaths = new[]
                    {
                        @"C:\ffmpeg\bin",
                        @"C:\Program Files\ffmpeg\bin",
                        Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? ""
                    };

                    foreach (var path in commonPaths)
                    {
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            ffmpeg.RootPath = path;
                            _ffmpegPath = path;
                            break;
                        }
                    }
                }

                // Try to actually use FFmpeg to verify it works
                // This will throw if binaries aren't found or are incompatible
                var version = ffmpeg.av_version_info();
                ffmpeg.avcodec_version();

                // Try to find at least one decoder to verify codecs work
                unsafe
                {
                    var decoder = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
                    if (decoder == null)
                    {
                        Debug.WriteLine("FFmpeg: H.264 decoder not found");
                        _isAvailable = false;
                        _initialized = true;
                        return;
                    }
                }

                _isAvailable = true;
                Debug.WriteLine($"FFmpeg initialized successfully. Version: {version}, Path: {_ffmpegPath ?? "system"}");
            }
            catch (DllNotFoundException ex)
            {
                Debug.WriteLine($"FFmpeg DLLs not found: {ex.Message}");
                Debug.WriteLine("H.264 encoding/decoding will be disabled. Install FFmpeg to enable.");
                _isAvailable = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FFmpeg initialization failed: {ex.Message}");
                _isAvailable = false;
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Check if a specific encoder is available.
    /// </summary>
    public static unsafe bool IsEncoderAvailable(string encoderName)
    {
        if (!IsAvailable) return false;

        try
        {
            var encoder = ffmpeg.avcodec_find_encoder_by_name(encoderName);
            return encoder != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a specific decoder is available.
    /// </summary>
    public static unsafe bool IsDecoderAvailable(string decoderName)
    {
        if (!IsAvailable) return false;

        try
        {
            var decoder = ffmpeg.avcodec_find_decoder_by_name(decoderName);
            return decoder != null;
        }
        catch
        {
            return false;
        }
    }
}
