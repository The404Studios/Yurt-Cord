using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Hardware-accelerated video encoder using FFmpeg.
/// Supports NVENC (NVIDIA), AMF (AMD), QSV (Intel), with software fallback.
/// </summary>
public unsafe class HardwareVideoEncoder : IDisposable
{
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private SwsContext* _swsContext;
    private byte*[] _srcData;
    private int[] _srcLinesize;
    private bool _initialized;
    private bool _disposed;
    private readonly object _lock = new();

    public string EncoderName { get; private set; } = "none";
    public bool IsHardwareAccelerated { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Fps { get; private set; }
    public int Bitrate { get; private set; }

    // Encoder priority: NVENC > AMF > QSV > Software
    private static readonly string[] HardwareEncoders =
    {
        "h264_nvenc",   // NVIDIA
        "h264_amf",     // AMD
        "h264_qsv",     // Intel Quick Sync
        "h264_mf",      // Windows Media Foundation (uses GPU if available)
    };

    private const string SoftwareEncoder = "libx264";

    static HardwareVideoEncoder()
    {
        try
        {
            // Set FFmpeg binaries path - look in app directory first
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ffmpegPath = Path.Combine(appDir, "ffmpeg");

            if (Directory.Exists(ffmpegPath))
            {
                ffmpeg.RootPath = ffmpegPath;
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
                        break;
                    }
                }
            }

            // Initialize FFmpeg - this will throw if binaries not found
            ffmpeg.avdevice_register_all();
            Debug.WriteLine($"FFmpeg initialized. Version: {ffmpeg.av_version_info()}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FFmpeg initialization failed: {ex.Message}");
            Debug.WriteLine("Please install FFmpeg binaries to use hardware encoding.");
        }
    }

    /// <summary>
    /// Initialize the encoder with specified parameters.
    /// Automatically selects best available hardware encoder.
    /// </summary>
    public bool Initialize(int width, int height, int fps = 60, int bitrateKbps = 6000)
    {
        lock (_lock)
        {
            if (_initialized) return true;

            Width = width;
            Height = height;
            Fps = fps;
            Bitrate = bitrateKbps * 1000;

            // Try hardware encoders first, fall back to software
            foreach (var encoderName in HardwareEncoders)
            {
                if (TryInitializeEncoder(encoderName, true))
                {
                    _initialized = true;
                    return true;
                }
            }

            // Fall back to software encoder
            if (TryInitializeEncoder(SoftwareEncoder, false))
            {
                _initialized = true;
                return true;
            }

            Debug.WriteLine("Failed to initialize any video encoder");
            return false;
        }
    }

    private bool TryInitializeEncoder(string encoderName, bool isHardware)
    {
        try
        {
            var codec = ffmpeg.avcodec_find_encoder_by_name(encoderName);
            if (codec == null)
            {
                Debug.WriteLine($"Encoder {encoderName} not found");
                return false;
            }

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
            {
                Debug.WriteLine($"Failed to allocate codec context for {encoderName}");
                return false;
            }

            // Configure encoder
            _codecContext->width = Width;
            _codecContext->height = Height;
            _codecContext->time_base = new AVRational { num = 1, den = Fps };
            _codecContext->framerate = new AVRational { num = Fps, den = 1 };
            _codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            _codecContext->bit_rate = Bitrate;
            _codecContext->gop_size = Fps; // Keyframe every second
            _codecContext->max_b_frames = 0; // No B-frames for low latency

            // Low-latency settings
            _codecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;

            // Codec-specific options for low latency
            AVDictionary* opts = null;

            if (encoderName.Contains("nvenc"))
            {
                // NVENC specific settings for low latency
                ffmpeg.av_dict_set(&opts, "preset", "p1", 0); // Fastest preset
                ffmpeg.av_dict_set(&opts, "tune", "ull", 0);  // Ultra low latency
                ffmpeg.av_dict_set(&opts, "zerolatency", "1", 0);
                ffmpeg.av_dict_set(&opts, "rc", "cbr", 0);    // Constant bitrate
            }
            else if (encoderName.Contains("amf"))
            {
                // AMD AMF settings
                ffmpeg.av_dict_set(&opts, "usage", "ultralowlatency", 0);
                ffmpeg.av_dict_set(&opts, "quality", "speed", 0);
                ffmpeg.av_dict_set(&opts, "rc", "cbr", 0);
            }
            else if (encoderName.Contains("qsv"))
            {
                // Intel QSV settings
                ffmpeg.av_dict_set(&opts, "preset", "veryfast", 0);
                ffmpeg.av_dict_set(&opts, "look_ahead", "0", 0);
            }
            else if (encoderName == SoftwareEncoder)
            {
                // x264 software encoder settings
                ffmpeg.av_dict_set(&opts, "preset", "ultrafast", 0);
                ffmpeg.av_dict_set(&opts, "tune", "zerolatency", 0);
                ffmpeg.av_dict_set(&opts, "crf", "23", 0);
            }

            var ret = ffmpeg.avcodec_open2(_codecContext, codec, &opts);
            ffmpeg.av_dict_free(&opts);

            if (ret < 0)
            {
                Debug.WriteLine($"Failed to open encoder {encoderName}: {GetErrorMessage(ret)}");
                ffmpeg.avcodec_free_context(&_codecContext);
                _codecContext = null;
                return false;
            }

            // Allocate frame
            _frame = ffmpeg.av_frame_alloc();
            _frame->format = (int)_codecContext->pix_fmt;
            _frame->width = Width;
            _frame->height = Height;

            ret = ffmpeg.av_frame_get_buffer(_frame, 0);
            if (ret < 0)
            {
                Debug.WriteLine($"Failed to allocate frame buffer: {GetErrorMessage(ret)}");
                Cleanup();
                return false;
            }

            // Allocate packet
            _packet = ffmpeg.av_packet_alloc();

            // Initialize color space converter (BGR24 -> YUV420P)
            _swsContext = ffmpeg.sws_getContext(
                Width, Height, AVPixelFormat.AV_PIX_FMT_BGR24,
                Width, Height, AVPixelFormat.AV_PIX_FMT_YUV420P,
                ffmpeg.SWS_FAST_BILINEAR, null, null, null);

            if (_swsContext == null)
            {
                Debug.WriteLine("Failed to create color space converter");
                Cleanup();
                return false;
            }

            _srcData = new byte*[4];
            _srcLinesize = new int[4];

            EncoderName = encoderName;
            IsHardwareAccelerated = isHardware;

            Debug.WriteLine($"Initialized encoder: {encoderName} (Hardware: {isHardware})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception initializing {encoderName}: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    /// <summary>
    /// Encode a bitmap frame to H.264.
    /// Returns null if encoding fails.
    /// </summary>
    public byte[]? EncodeFrame(Bitmap bitmap, long frameNumber)
    {
        if (!_initialized || _disposed) return null;

        lock (_lock)
        {
            try
            {
                // Make frame writable
                var ret = ffmpeg.av_frame_make_writable(_frame);
                if (ret < 0)
                {
                    Debug.WriteLine($"Failed to make frame writable: {GetErrorMessage(ret)}");
                    return null;
                }

                // Lock bitmap and get pointer to pixel data
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    // Setup source data for color conversion
                    _srcData[0] = (byte*)bitmapData.Scan0;
                    _srcLinesize[0] = bitmapData.Stride;

                    fixed (byte** srcDataPtr = _srcData)
                    fixed (int* srcLinesizePtr = _srcLinesize)
                    {
                        // Convert BGR24 to YUV420P
                        ffmpeg.sws_scale(_swsContext,
                            srcDataPtr, srcLinesizePtr, 0, Height,
                            _frame->data, _frame->linesize);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                _frame->pts = frameNumber;

                // Send frame to encoder
                ret = ffmpeg.avcodec_send_frame(_codecContext, _frame);
                if (ret < 0)
                {
                    Debug.WriteLine($"Error sending frame: {GetErrorMessage(ret)}");
                    return null;
                }

                // Receive encoded packet
                ret = ffmpeg.avcodec_receive_packet(_codecContext, _packet);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                {
                    return null; // Need more frames or end of stream
                }
                if (ret < 0)
                {
                    Debug.WriteLine($"Error receiving packet: {GetErrorMessage(ret)}");
                    return null;
                }

                // Copy packet data to managed array
                var encodedData = new byte[_packet->size];
                Marshal.Copy((IntPtr)_packet->data, encodedData, 0, _packet->size);

                ffmpeg.av_packet_unref(_packet);

                return encodedData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Encode error: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Encode raw BGR24 pixel data to H.264.
    /// </summary>
    public byte[]? EncodeRawFrame(byte[] bgrData, int stride, long frameNumber)
    {
        if (!_initialized || _disposed) return null;

        lock (_lock)
        {
            try
            {
                var ret = ffmpeg.av_frame_make_writable(_frame);
                if (ret < 0) return null;

                fixed (byte* srcPtr = bgrData)
                {
                    _srcData[0] = srcPtr;
                    _srcLinesize[0] = stride;

                    fixed (byte** srcDataPtr = _srcData)
                    fixed (int* srcLinesizePtr = _srcLinesize)
                    {
                        ffmpeg.sws_scale(_swsContext,
                            srcDataPtr, srcLinesizePtr, 0, Height,
                            _frame->data, _frame->linesize);
                    }
                }

                _frame->pts = frameNumber;

                ret = ffmpeg.avcodec_send_frame(_codecContext, _frame);
                if (ret < 0) return null;

                ret = ffmpeg.avcodec_receive_packet(_codecContext, _packet);
                if (ret < 0) return null;

                var encodedData = new byte[_packet->size];
                Marshal.Copy((IntPtr)_packet->data, encodedData, 0, _packet->size);

                ffmpeg.av_packet_unref(_packet);
                return encodedData;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Get codec extradata (SPS/PPS) needed by decoder.
    /// Should be sent to receiver before first frame.
    /// </summary>
    public byte[]? GetCodecExtradata()
    {
        if (_codecContext == null || _codecContext->extradata == null)
            return null;

        var extradata = new byte[_codecContext->extradata_size];
        Marshal.Copy((IntPtr)_codecContext->extradata, extradata, 0, _codecContext->extradata_size);
        return extradata;
    }

    private static string GetErrorMessage(int error)
    {
        var buffer = stackalloc byte[1024];
        ffmpeg.av_strerror(error, buffer, 1024);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? "Unknown error";
    }

    private void Cleanup()
    {
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        if (_packet != null)
        {
            fixed (AVPacket** p = &_packet)
                ffmpeg.av_packet_free(p);
        }

        if (_frame != null)
        {
            fixed (AVFrame** f = &_frame)
                ffmpeg.av_frame_free(f);
        }

        if (_codecContext != null)
        {
            fixed (AVCodecContext** c = &_codecContext)
                ffmpeg.avcodec_free_context(c);
        }

        _initialized = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            Cleanup();
        }

        GC.SuppressFinalize(this);
    }

    ~HardwareVideoEncoder()
    {
        Dispose();
    }
}
