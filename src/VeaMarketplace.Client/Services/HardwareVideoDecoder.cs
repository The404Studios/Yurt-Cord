using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Hardware-accelerated video decoder using FFmpeg.
/// Decodes H.264 streams with NVDEC/AMD/QSV hardware acceleration.
/// </summary>
public unsafe class HardwareVideoDecoder : IDisposable
{
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private AVFrame* _rgbFrame;
    private AVPacket* _packet;
    private SwsContext* _swsContext;
    private byte[] _rgbBuffer = Array.Empty<byte>();
    private bool _initialized;
    private bool _disposed;
    private readonly object _lock = new();

    public string DecoderName { get; private set; } = "none";
    public bool IsHardwareAccelerated { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    // Decoder priority: Hardware > Software
    private static readonly string[] HardwareDecoders =
    {
        "h264_cuvid",   // NVIDIA CUDA
        "h264_qsv",     // Intel Quick Sync
        "h264_d3d11va", // Direct3D 11 (Windows)
    };

    private const string SoftwareDecoder = "h264";

    /// <summary>
    /// Initialize the decoder.
    /// Automatically selects best available hardware decoder.
    /// </summary>
    public bool Initialize(byte[]? extradata = null)
    {
        lock (_lock)
        {
            if (_initialized) return true;

            // Try hardware decoders first
            foreach (var decoderName in HardwareDecoders)
            {
                if (TryInitializeDecoder(decoderName, true, extradata))
                {
                    _initialized = true;
                    return true;
                }
            }

            // Fall back to software decoder
            if (TryInitializeDecoder(SoftwareDecoder, false, extradata))
            {
                _initialized = true;
                return true;
            }

            Debug.WriteLine("Failed to initialize any video decoder");
            return false;
        }
    }

    private bool TryInitializeDecoder(string decoderName, bool isHardware, byte[]? extradata)
    {
        try
        {
            var codec = ffmpeg.avcodec_find_decoder_by_name(decoderName);
            if (codec == null)
            {
                // Try generic decoder
                codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
                if (codec == null)
                {
                    Debug.WriteLine($"Decoder {decoderName} not found");
                    return false;
                }
                decoderName = "h264 (generic)";
                isHardware = false;
            }

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
            {
                Debug.WriteLine($"Failed to allocate codec context for {decoderName}");
                return false;
            }

            // Set extradata if provided (SPS/PPS)
            if (extradata != null && extradata.Length > 0)
            {
                _codecContext->extradata = (byte*)ffmpeg.av_malloc((ulong)(extradata.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE));
                _codecContext->extradata_size = extradata.Length;
                Marshal.Copy(extradata, 0, (IntPtr)_codecContext->extradata, extradata.Length);
            }

            // Low-latency settings
            _codecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            _codecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;

            AVDictionary* opts = null;
            ffmpeg.av_dict_set(&opts, "threads", "auto", 0);

            var ret = ffmpeg.avcodec_open2(_codecContext, codec, &opts);
            ffmpeg.av_dict_free(&opts);

            if (ret < 0)
            {
                Debug.WriteLine($"Failed to open decoder {decoderName}: {GetErrorMessage(ret)}");
                ffmpeg.avcodec_free_context(&_codecContext);
                _codecContext = null;
                return false;
            }

            _frame = ffmpeg.av_frame_alloc();
            _rgbFrame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();

            DecoderName = decoderName;
            IsHardwareAccelerated = isHardware;

            Debug.WriteLine($"Initialized decoder: {decoderName} (Hardware: {isHardware})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception initializing decoder {decoderName}: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    /// <summary>
    /// Decode H.264 data to a WPF BitmapSource.
    /// Returns null if decoding fails.
    /// </summary>
    public BitmapSource? DecodeFrame(byte[] h264Data)
    {
        if (!_initialized || _disposed || h264Data == null || h264Data.Length == 0)
            return null;

        lock (_lock)
        {
            try
            {
                fixed (byte* dataPtr = h264Data)
                {
                    _packet->data = dataPtr;
                    _packet->size = h264Data.Length;

                    var ret = ffmpeg.avcodec_send_packet(_codecContext, _packet);
                    if (ret < 0)
                    {
                        Debug.WriteLine($"Error sending packet: {GetErrorMessage(ret)}");
                        return null;
                    }

                    ret = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
                    if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                    {
                        return null; // Need more data
                    }
                    if (ret < 0)
                    {
                        Debug.WriteLine($"Error receiving frame: {GetErrorMessage(ret)}");
                        return null;
                    }

                    // Update dimensions if changed
                    if (Width != _frame->width || Height != _frame->height)
                    {
                        Width = _frame->width;
                        Height = _frame->height;
                        InitializeColorConverter();
                    }

                    // Convert YUV420P to BGR24
                    ffmpeg.sws_scale(_swsContext,
                        _frame->data, _frame->linesize, 0, Height,
                        _rgbFrame->data, _rgbFrame->linesize);

                    // Copy to managed buffer
                    var stride = _rgbFrame->linesize[0];
                    var bufferSize = stride * Height;

                    if (_rgbBuffer.Length < bufferSize)
                    {
                        _rgbBuffer = new byte[bufferSize];
                    }

                    Marshal.Copy((IntPtr)_rgbFrame->data[0], _rgbBuffer, 0, bufferSize);

                    // Create WPF BitmapSource
                    var bitmap = BitmapSource.Create(
                        Width, Height,
                        96, 96,
                        PixelFormats.Bgr24,
                        null,
                        _rgbBuffer,
                        stride);

                    bitmap.Freeze(); // Make thread-safe
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decode error: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Decode to raw BGR24 bytes (for scenarios where BitmapSource isn't needed).
    /// </summary>
    public (byte[]? data, int width, int height, int stride) DecodeFrameRaw(byte[] h264Data)
    {
        if (!_initialized || _disposed || h264Data == null || h264Data.Length == 0)
            return (null, 0, 0, 0);

        lock (_lock)
        {
            try
            {
                fixed (byte* dataPtr = h264Data)
                {
                    _packet->data = dataPtr;
                    _packet->size = h264Data.Length;

                    var ret = ffmpeg.avcodec_send_packet(_codecContext, _packet);
                    if (ret < 0) return (null, 0, 0, 0);

                    ret = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
                    if (ret < 0) return (null, 0, 0, 0);

                    if (Width != _frame->width || Height != _frame->height)
                    {
                        Width = _frame->width;
                        Height = _frame->height;
                        InitializeColorConverter();
                    }

                    ffmpeg.sws_scale(_swsContext,
                        _frame->data, _frame->linesize, 0, Height,
                        _rgbFrame->data, _rgbFrame->linesize);

                    var stride = _rgbFrame->linesize[0];
                    var bufferSize = stride * Height;

                    if (_rgbBuffer.Length < bufferSize)
                    {
                        _rgbBuffer = new byte[bufferSize];
                    }

                    Marshal.Copy((IntPtr)_rgbFrame->data[0], _rgbBuffer, 0, bufferSize);

                    return (_rgbBuffer, Width, Height, stride);
                }
            }
            catch
            {
                return (null, 0, 0, 0);
            }
        }
    }

    private void InitializeColorConverter()
    {
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
        }

        _swsContext = ffmpeg.sws_getContext(
            Width, Height, AVPixelFormat.AV_PIX_FMT_YUV420P,
            Width, Height, AVPixelFormat.AV_PIX_FMT_BGR24,
            ffmpeg.SWS_FAST_BILINEAR, null, null, null);

        // Allocate RGB frame buffer
        _rgbFrame->format = (int)AVPixelFormat.AV_PIX_FMT_BGR24;
        _rgbFrame->width = Width;
        _rgbFrame->height = Height;

        if (_rgbFrame->data[0] != null)
        {
            ffmpeg.av_freep(&_rgbFrame->data[0]);
        }

        var bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, Width, Height, 1);
        var buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);

        var data = new byte_ptrArray4();
        var linesize = new int_array4();

        ffmpeg.av_image_fill_arrays(ref data, ref linesize, buffer, AVPixelFormat.AV_PIX_FMT_BGR24, Width, Height, 1);

        for (var i = 0; i < 4; i++)
        {
            _rgbFrame->data[(uint)i] = data[(uint)i];
            _rgbFrame->linesize[(uint)i] = linesize[(uint)i];
        }
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

        if (_rgbFrame != null)
        {
            if (_rgbFrame->data[0] != null)
            {
                ffmpeg.av_freep(&_rgbFrame->data[0]);
            }
            fixed (AVFrame** f = &_rgbFrame)
                ffmpeg.av_frame_free(f);
        }

        if (_frame != null)
        {
            fixed (AVFrame** f = &_frame)
                ffmpeg.av_frame_free(f);
        }

        if (_codecContext != null)
        {
            if (_codecContext->extradata != null)
            {
                ffmpeg.av_freep(&_codecContext->extradata);
            }
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

    ~HardwareVideoDecoder()
    {
        Dispose();
    }
}
