using System.Buffers;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;

namespace VeaMarketplace.Client.Services.Streaming;

/// <summary>
/// High-performance frame buffer pool to eliminate GC pressure.
///
/// Features:
/// - Pre-allocated byte arrays for frame data
/// - Bitmap pool for capture operations
/// - Thread-safe concurrent access
/// - Automatic cleanup of unused buffers
///
/// This dramatically reduces GC pauses during streaming.
/// </summary>
public class FrameBufferPool : IDisposable
{
    private readonly int _maxWidth;
    private readonly int _maxHeight;
    private readonly int _poolSize;

    // Byte buffer pool (for raw frame data)
    private readonly ConcurrentBag<byte[]> _byteBuffers = new();
    private readonly int _maxBufferSize;

    // Bitmap pool (for capture operations)
    private readonly ConcurrentBag<PooledBitmap> _bitmapPool = new();

    // Encoded frame buffer pool
    private readonly ConcurrentBag<MemoryStream> _streamPool = new();

    private volatile bool _disposed;

    public FrameBufferPool(int maxWidth, int maxHeight, int poolSize = 5)
    {
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        _poolSize = poolSize;
        _maxBufferSize = maxWidth * maxHeight * 4; // RGBA

        // Pre-allocate buffers
        for (int i = 0; i < poolSize; i++)
        {
            _byteBuffers.Add(new byte[_maxBufferSize]);
            _streamPool.Add(new MemoryStream(_maxBufferSize / 4)); // Encoded is smaller
        }
    }

    /// <summary>
    /// Rent a byte buffer for frame data.
    /// </summary>
    public byte[] RentBuffer(int minSize)
    {
        if (_disposed) return new byte[minSize];

        // Try to get from pool
        if (_byteBuffers.TryTake(out var buffer) && buffer.Length >= minSize)
        {
            return buffer;
        }

        // Return to pool if too small, allocate new
        if (buffer != null)
        {
            _byteBuffers.Add(buffer);
        }

        return new byte[Math.Max(minSize, _maxBufferSize)];
    }

    /// <summary>
    /// Return a byte buffer to the pool.
    /// </summary>
    public void ReturnBuffer(byte[] buffer)
    {
        if (_disposed || buffer == null) return;

        // Only pool buffers of expected size
        if (buffer.Length == _maxBufferSize && _byteBuffers.Count < _poolSize * 2)
        {
            _byteBuffers.Add(buffer);
        }
    }

    /// <summary>
    /// Rent a bitmap for capture operations.
    /// </summary>
    public PooledBitmap RentBitmap(int width, int height)
    {
        if (_disposed)
        {
            return new PooledBitmap(new Bitmap(width, height, PixelFormat.Format24bppRgb), this, false);
        }

        // Try to get from pool with matching size
        if (_bitmapPool.TryTake(out var pooled) && pooled.Width == width && pooled.Height == height)
        {
            pooled.IsPooled = true;
            return pooled;
        }

        // Return mismatched bitmap to pool or dispose
        if (pooled != null)
        {
            if (pooled.Width == _maxWidth && pooled.Height == _maxHeight)
            {
                _bitmapPool.Add(pooled);
            }
            else
            {
                pooled.Bitmap.Dispose();
            }
        }

        // Create new bitmap
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        return new PooledBitmap(bitmap, this, true);
    }

    /// <summary>
    /// Return a bitmap to the pool.
    /// </summary>
    internal void ReturnBitmap(PooledBitmap pooled)
    {
        if (_disposed || pooled == null) return;

        if (pooled.IsPooled && _bitmapPool.Count < _poolSize * 2)
        {
            _bitmapPool.Add(pooled);
        }
        else
        {
            pooled.Bitmap.Dispose();
        }
    }

    /// <summary>
    /// Rent a memory stream for encoding.
    /// </summary>
    public MemoryStream RentStream()
    {
        if (_disposed)
        {
            return new MemoryStream();
        }

        if (_streamPool.TryTake(out var stream))
        {
            stream.SetLength(0);
            stream.Position = 0;
            return stream;
        }

        return new MemoryStream(_maxBufferSize / 4);
    }

    /// <summary>
    /// Return a memory stream to the pool.
    /// </summary>
    public void ReturnStream(MemoryStream stream)
    {
        if (_disposed || stream == null) return;

        if (_streamPool.Count < _poolSize * 2)
        {
            stream.SetLength(0);
            stream.Position = 0;
            _streamPool.Add(stream);
        }
        else
        {
            stream.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clear byte buffers (they'll be GC'd)
        _byteBuffers.Clear();

        // Dispose bitmaps
        while (_bitmapPool.TryTake(out var pooled))
        {
            try { pooled.Bitmap.Dispose(); } catch { }
        }

        // Dispose streams
        while (_streamPool.TryTake(out var stream))
        {
            try { stream.Dispose(); } catch { }
        }
    }
}

/// <summary>
/// Pooled bitmap wrapper that returns to pool on dispose.
/// </summary>
public class PooledBitmap : IDisposable
{
    public Bitmap Bitmap { get; }
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    internal bool IsPooled { get; set; }
    private readonly FrameBufferPool? _pool;
    private bool _disposed;

    public PooledBitmap(Bitmap bitmap, FrameBufferPool? pool, bool isPooled)
    {
        Bitmap = bitmap;
        _pool = pool;
        IsPooled = isPooled;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pool != null && IsPooled)
        {
            _pool.ReturnBitmap(this);
        }
        else
        {
            Bitmap.Dispose();
        }
    }

    // Implicit conversion for ease of use
    public static implicit operator Bitmap(PooledBitmap pooled) => pooled.Bitmap;
}

/// <summary>
/// Array pool wrapper for frame data with automatic return.
/// </summary>
public class RentedBuffer : IDisposable
{
    public byte[] Data { get; }
    public int Length { get; }

    private readonly FrameBufferPool _pool;
    private bool _disposed;

    public RentedBuffer(FrameBufferPool pool, int size)
    {
        _pool = pool;
        Data = pool.RentBuffer(size);
        Length = size;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.ReturnBuffer(Data);
    }

    // Allow indexing
    public byte this[int index]
    {
        get => Data[index];
        set => Data[index] = value;
    }
}
