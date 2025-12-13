using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;

namespace VeaMarketplace.Server.Controllers;

/// <summary>
/// HTTP-based screen streaming controller.
/// Separates video frames from SignalR to prevent voice lag.
///
/// Architecture:
/// - Sharer POSTs frames to /api/stream/{streamId}/frame
/// - Server stores latest frame in memory
/// - Viewers GET frames via /api/stream/{streamId}/frame (polling)
/// - SignalR sends lightweight "FrameAvailable" notifications only
/// </summary>
[ApiController]
[Route("api/stream")]
public class ScreenStreamController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IHubContext<VoiceHub> _voiceHubContext;

    // In-memory frame storage: streamId -> frame data
    private static readonly ConcurrentDictionary<string, StreamFrame> _frames = new();
    private static readonly ConcurrentDictionary<string, StreamInfo> _streams = new();

    // Cleanup timer
    private static Timer? _cleanupTimer;
    private static readonly object _cleanupLock = new();

    public ScreenStreamController(AuthService authService, IHubContext<VoiceHub> voiceHubContext)
    {
        _authService = authService;
        _voiceHubContext = voiceHubContext;

        // Initialize cleanup timer (runs every 30 seconds)
        lock (_cleanupLock)
        {
            _cleanupTimer ??= new Timer(CleanupStaleStreams, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>
    /// Start a new screen stream. Returns a stream ID for frame uploads.
    /// </summary>
    [HttpPost("start")]
    public ActionResult<StartStreamResponse> StartStream(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] StartStreamRequest request)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized(new { error = "Unauthorized" });

        var streamId = Guid.NewGuid().ToString("N")[..16];

        _streams[streamId] = new StreamInfo
        {
            StreamId = streamId,
            UserId = userId,
            ChannelId = request.ChannelId,
            Username = request.Username,
            StartedAt = DateTime.UtcNow,
            LastFrameAt = DateTime.UtcNow
        };

        return Ok(new StartStreamResponse
        {
            StreamId = streamId,
            UploadUrl = $"/api/stream/{streamId}/frame",
            ViewUrl = $"/api/stream/{streamId}/frame"
        });
    }

    /// <summary>
    /// Upload a frame to the stream (sharer calls this)
    /// </summary>
    [HttpPost("{streamId}/frame")]
    public async Task<IActionResult> UploadFrame(
        string streamId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromHeader(Name = "X-Frame-Width")] int width,
        [FromHeader(Name = "X-Frame-Height")] int height)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized();

        if (!_streams.TryGetValue(streamId, out var stream))
            return NotFound(new { error = "Stream not found" });

        if (stream.UserId != userId)
            return Forbid();

        // Read frame data from request body
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var frameData = ms.ToArray();

        if (frameData.Length == 0)
            return BadRequest(new { error = "Empty frame" });

        // Store frame
        var frameNumber = stream.FrameCount++;
        _frames[streamId] = new StreamFrame
        {
            Data = frameData,
            Width = width,
            Height = height,
            FrameNumber = frameNumber,
            Timestamp = DateTime.UtcNow
        };

        stream.LastFrameAt = DateTime.UtcNow;
        stream.TotalBytes += frameData.Length;

        // Send lightweight notification via SignalR (just frame number, not data)
        // Viewers can then fetch the frame via HTTP if they want it
        await _voiceHubContext.Clients.Group($"voice_{stream.ChannelId}")
            .SendAsync("ScreenFrameAvailable", streamId, frameNumber, width, height);

        return Ok(new { frameNumber });
    }

    /// <summary>
    /// Get the latest frame from a stream (viewers call this)
    /// </summary>
    [HttpGet("{streamId}/frame")]
    public IActionResult GetFrame(string streamId, [FromQuery] long? since = null)
    {
        if (!_frames.TryGetValue(streamId, out var frame))
            return NotFound(new { error = "No frame available" });

        // If client already has this frame, return 304 Not Modified
        if (since.HasValue && frame.FrameNumber <= since.Value)
            return StatusCode(304);

        Response.Headers["X-Frame-Number"] = frame.FrameNumber.ToString();
        Response.Headers["X-Frame-Width"] = frame.Width.ToString();
        Response.Headers["X-Frame-Height"] = frame.Height.ToString();
        Response.Headers["Cache-Control"] = "no-cache, no-store";

        return File(frame.Data, "image/jpeg");
    }

    /// <summary>
    /// Get stream info
    /// </summary>
    [HttpGet("{streamId}/info")]
    public IActionResult GetStreamInfo(string streamId)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
            return NotFound(new { error = "Stream not found" });

        return Ok(new
        {
            stream.StreamId,
            stream.Username,
            stream.ChannelId,
            stream.FrameCount,
            stream.TotalBytes,
            StartedAt = stream.StartedAt,
            DurationSeconds = (DateTime.UtcNow - stream.StartedAt).TotalSeconds
        });
    }

    /// <summary>
    /// Stop a stream
    /// </summary>
    [HttpPost("{streamId}/stop")]
    public IActionResult StopStream(
        string streamId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized();

        if (!_streams.TryGetValue(streamId, out var stream))
            return NotFound(new { error = "Stream not found" });

        if (stream.UserId != userId)
            return Forbid();

        // Remove stream and frame
        _streams.TryRemove(streamId, out _);
        _frames.TryRemove(streamId, out _);

        return Ok(new { message = "Stream stopped" });
    }

    /// <summary>
    /// List active streams in a channel
    /// </summary>
    [HttpGet("channel/{channelId}")]
    public IActionResult GetChannelStreams(string channelId)
    {
        var streams = _streams.Values
            .Where(s => s.ChannelId == channelId)
            .Select(s => new
            {
                s.StreamId,
                s.Username,
                s.FrameCount,
                DurationSeconds = (DateTime.UtcNow - s.StartedAt).TotalSeconds
            })
            .ToList();

        return Ok(streams);
    }

    private string? ValidateToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization))
            return null;

        var token = authorization.StartsWith("Bearer ")
            ? authorization[7..]
            : authorization;

        return _authService.ValidateToken(token);
    }

    private static void CleanupStaleStreams(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        foreach (var kvp in _streams)
        {
            if (kvp.Value.LastFrameAt < cutoff)
            {
                _streams.TryRemove(kvp.Key, out _);
                _frames.TryRemove(kvp.Key, out _);
            }
        }
    }

    private class StreamFrame
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public long FrameNumber { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class StreamInfo
    {
        public string StreamId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string ChannelId { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public DateTime LastFrameAt { get; set; }
        public long FrameCount { get; set; }
        public long TotalBytes { get; set; }
    }
}

public class StartStreamRequest
{
    public string ChannelId { get; set; } = "";
    public string Username { get; set; } = "";
}

public class StartStreamResponse
{
    public string StreamId { get; set; } = "";
    public string UploadUrl { get; set; } = "";
    public string ViewUrl { get; set; } = "";
}
