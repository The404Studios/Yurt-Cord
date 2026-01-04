using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// HTTP-based screen streaming service.
/// Uploads frames via HTTP instead of SignalR to prevent voice lag.
///
/// Benefits over SignalR:
/// - Dedicated connection for video (doesn't block audio)
/// - Better handling of large binary data
/// - Can leverage HTTP/2 multiplexing
/// - Viewers poll independently (no broadcast bottleneck)
/// </summary>
public class HttpScreenStreamService : IDisposable
{
    private readonly HttpClient _httpClient;
    private string? _currentStreamId;
    private string? _authToken;
    private bool _disposed;

    // Cached JSON serializer options for performance
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Stats
    private long _framesSent;
    private long _bytesSent;
    private readonly Stopwatch _uploadTimer = new();

    public string? CurrentStreamId => _currentStreamId;
    public bool IsStreaming => _currentStreamId != null;
    public long FramesSent => _framesSent;
    public long BytesSent => _bytesSent;

    // Events
    public event Action<string, long, int, int>? OnFrameAvailable;

    public HttpScreenStreamService(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Start a new stream. Returns stream ID for frame uploads.
    /// </summary>
    public async Task<string?> StartStreamAsync(string channelId, string username)
    {
        if (_disposed) return null;

        try
        {
            var request = new
            {
                ChannelId = channelId,
                Username = username
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/api/stream/start", content);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Failed to start stream: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<StartStreamResponse>(json, JsonOptions);

            _currentStreamId = result?.StreamId;
            _framesSent = 0;
            _bytesSent = 0;

            Debug.WriteLine($"Started HTTP stream: {_currentStreamId}");
            return _currentStreamId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting stream: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Upload a frame to the stream.
    /// </summary>
    public async Task<bool> UploadFrameAsync(byte[] frameData, int width, int height)
    {
        if (_disposed || _currentStreamId == null) return false;

        try
        {
            _uploadTimer.Restart();

            using var content = new ByteArrayContent(frameData);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stream/{_currentStreamId}/frame");
            request.Content = content;
            request.Headers.Add("X-Frame-Width", width.ToString());
            request.Headers.Add("X-Frame-Height", height.ToString());

            var response = await _httpClient.SendAsync(request);

            _uploadTimer.Stop();

            if (response.IsSuccessStatusCode)
            {
                _framesSent++;
                _bytesSent += frameData.Length;
                return true;
            }

            Debug.WriteLine($"Frame upload failed: {response.StatusCode}");
            return false;
        }
        catch (TaskCanceledException)
        {
            // Timeout - frame dropped
            Debug.WriteLine("Frame upload timeout");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Frame upload error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get the latest frame from a stream (for viewers).
    /// </summary>
    public async Task<(byte[]? data, int width, int height, long frameNumber)> GetFrameAsync(string streamId, long? sinceFrame = null)
    {
        if (_disposed) return (null, 0, 0, 0);

        try
        {
            var url = $"/api/stream/{streamId}/frame";
            if (sinceFrame.HasValue)
                url += $"?since={sinceFrame.Value}";

            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                // No new frame
                return (null, 0, 0, sinceFrame ?? 0);
            }

            if (!response.IsSuccessStatusCode)
                return (null, 0, 0, 0);

            var data = await response.Content.ReadAsByteArrayAsync();

            int.TryParse(response.Headers.GetValues("X-Frame-Width").FirstOrDefault(), out var width);
            int.TryParse(response.Headers.GetValues("X-Frame-Height").FirstOrDefault(), out var height);
            long.TryParse(response.Headers.GetValues("X-Frame-Number").FirstOrDefault(), out var frameNumber);

            return (data, width, height, frameNumber);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get frame error: {ex.Message}");
            return (null, 0, 0, 0);
        }
    }

    /// <summary>
    /// Stop the current stream.
    /// </summary>
    public async Task StopStreamAsync()
    {
        if (_disposed || _currentStreamId == null) return;

        try
        {
            await _httpClient.PostAsync($"/api/stream/{_currentStreamId}/stop", null);
            Debug.WriteLine($"Stopped HTTP stream: {_currentStreamId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping stream: {ex.Message}");
        }
        finally
        {
            _currentStreamId = null;
        }
    }

    /// <summary>
    /// Get list of active streams in a channel.
    /// </summary>
    public async Task<List<StreamInfo>> GetChannelStreamsAsync(string channelId)
    {
        if (_disposed) return new List<StreamInfo>();

        try
        {
            var response = await _httpClient.GetAsync($"/api/stream/channel/{channelId}");
            if (!response.IsSuccessStatusCode)
                return new List<StreamInfo>();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<StreamInfo>>(json, JsonOptions) ?? new List<StreamInfo>();
        }
        catch
        {
            return new List<StreamInfo>();
        }
    }

    /// <summary>
    /// Handle SignalR notification that a new frame is available.
    /// Viewers can use this to trigger a frame fetch.
    /// </summary>
    public void HandleFrameAvailable(string streamId, long frameNumber, int width, int height)
    {
        OnFrameAvailable?.Invoke(streamId, frameNumber, width, height);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_currentStreamId != null)
            {
                // Fire and forget stop
                _ = StopStreamAsync();
            }
        }
        catch { }

        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private class StartStreamResponse
    {
        public string StreamId { get; set; } = "";
        public string UploadUrl { get; set; } = "";
        public string ViewUrl { get; set; } = "";
    }
}

public class StreamInfo
{
    public string StreamId { get; set; } = "";
    public string Username { get; set; } = "";
    public long FrameCount { get; set; }
    public double DurationSeconds { get; set; }
}
