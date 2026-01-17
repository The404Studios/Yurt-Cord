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
/// - Short array pool for audio packets
/// - Tiered buffer sizes for optimal memory usage
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

    // Small buffer pool (for audio frames, encoded data)
    private readonly ConcurrentBag<byte[]> _smallBuffers = new();
    private const int SmallBufferSize = 8192; // 8KB for audio/small data

    // Medium buffer pool (for encoded frames)
    private readonly ConcurrentBag<byte[]> _mediumBuffers = new();
    private const int MediumBufferSize = 262144; // 256KB for encoded frames

    // Short array pool for PCM audio samples
    private readonly ConcurrentBag<short[]> _shortBuffers = new();
    private const int ShortBufferSize = 960; // Opus frame size

    // Bitmap pool (for capture operations)
    private readonly ConcurrentBag<PooledBitmap> _bitmapPool = new();

    // Encoded frame buffer pool
    private readonly ConcurrentBag<MemoryStream> _streamPool = new();

    // Statistics for monitoring
    private long _totalRented;
    private long _totalReturned;
    private long _allocationsAvoided;

    public long TotalRented => Interlocked.Read(ref _totalRented);
    public long TotalReturned => Interlocked.Read(ref _totalReturned);
    public long AllocationsAvoided => Interlocked.Read(ref _allocationsAvoided);

    private volatile bool _disposed;

    public FrameBufferPool(int maxWidth, int maxHeight, int poolSize = 5)
    {
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        _poolSize = poolSize;
        _maxBufferSize = maxWidth * maxHeight * 4; // RGBA

        // Pre-allocate buffers for different sizes
        for (int i = 0; i < poolSize; i++)
        {
            _byteBuffers.Add(new byte[_maxBufferSize]);
            _streamPool.Add(new MemoryStream(_maxBufferSize / 4)); // Encoded is smaller
        }

        // Pre-allocate small buffers for audio
        for (int i = 0; i < poolSize * 2; i++)
        {
            _smallBuffers.Add(new byte[SmallBufferSize]);
            _shortBuffers.Add(new short[ShortBufferSize]);
        }

        // Pre-allocate medium buffers for encoded frames
        for (int i = 0; i < poolSize; i++)
        {
            _mediumBuffers.Add(new byte[MediumBufferSize]);
        }
    }

    /// <summary>
    /// Rent a byte buffer for frame data. Uses tiered pools for efficiency.
    /// </summary>
    public byte[] RentBuffer(int minSize)
    {
        if (_disposed) return new byte[minSize];

        Interlocked.Increment(ref _totalRented);

        // Use appropriate pool based on size
        if (minSize <= SmallBufferSize)
        {
            if (_smallBuffers.TryTake(out var smallBuf))
            {
                Interlocked.Increment(ref _allocationsAvoided);
                return smallBuf;
            }
            return new byte[SmallBufferSize];
        }

        if (minSize <= MediumBufferSize)
        {
            if (_mediumBuffers.TryTake(out var medBuf))
            {
                Interlocked.Increment(ref _allocationsAvoided);
                return medBuf;
            }
            return new byte[MediumBufferSize];
        }

        // Try to get from large buffer pool
        if (_byteBuffers.TryTake(out var buffer) && buffer.Length >= minSize)
        {
            Interlocked.Increment(ref _allocationsAvoided);
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

        Interlocked.Increment(ref _totalReturned);

        // Route to appropriate pool based on size
        if (buffer.Length == SmallBufferSize && _smallBuffers.Count < _poolSize * 4)
        {
            _smallBuffers.Add(buffer);
            return;
        }

        if (buffer.Length == MediumBufferSize && _mediumBuffers.Count < _poolSize * 2)
        {
            _mediumBuffers.Add(buffer);
            return;
        }

        // Only pool buffers of expected size
        if (buffer.Length == _maxBufferSize && _byteBuffers.Count < _poolSize * 2)
        {
            _byteBuffers.Add(buffer);
        }
    }

    /// <summary>
    /// Rent a short array for PCM audio samples.
    /// </summary>
    public short[] RentShortBuffer(int minSize = ShortBufferSize)
    {
        if (_disposed) return new short[minSize];

        Interlocked.Increment(ref _totalRented);

        if (_shortBuffers.TryTake(out var buffer) && buffer.Length >= minSize)
        {
            Interlocked.Increment(ref _allocationsAvoided);
            return buffer;
        }

        if (buffer != null)
        {
            _shortBuffers.Add(buffer);
        }

        return new short[Math.Max(minSize, ShortBufferSize)];
    }

    /// <summary>
    /// Return a short buffer to the pool.
    /// </summary>
    public void ReturnShortBuffer(short[] buffer)
    {
        if (_disposed || buffer == null) return;

        Interlocked.Increment(ref _totalReturned);

        if (buffer.Length == ShortBufferSize && _shortBuffers.Count < _poolSize * 4)
        {
            Array.Clear(buffer, 0, buffer.Length);
            _shortBuffers.Add(buffer);
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
        _smallBuffers.Clear();
        _mediumBuffers.Clear();
        _shortBuffers.Clear();

        // Dispose bitmaps
        while (_bitmapPool.TryTake(out var pooled))
        {
            try { pooled.Bitmap.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error disposing bitmap: {ex.Message}"); }
        }

        // Dispose streams
        while (_streamPool.TryTake(out var stream))
        {
            try { stream.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error disposing stream: {ex.Message}"); }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Get pool statistics for monitoring.
    /// </summary>
    public (int Small, int Medium, int Large, int Bitmaps, int Streams, int Shorts) GetPoolCounts()
    {
        return (_smallBuffers.Count, _mediumBuffers.Count, _byteBuffers.Count,
                _bitmapPool.Count, _streamPool.Count, _shortBuffers.Count);
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

        GC.SuppressFinalize(this);
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
        GC.SuppressFinalize(this);
    }

    // Allow indexing
    public byte this[int index]
    {
        get => Data[index];
        set => Data[index] = value;
    }
}
