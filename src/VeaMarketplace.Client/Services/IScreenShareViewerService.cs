using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for viewing screen shares from other users and self
/// </summary>
public interface IScreenShareViewerService
{
    /// <summary>
    /// Currently active screen shares in the channel
    /// </summary>
    ObservableCollection<ScreenShareDto> ActiveScreenShares { get; }

    /// <summary>
    /// The screen share we're currently viewing (if any)
    /// </summary>
    ScreenShareDto? CurrentlyViewing { get; }

    /// <summary>
    /// Our own screen share (if sharing)
    /// </summary>
    ScreenShareDto? OwnScreenShare { get; }

    /// <summary>
    /// Event when a new frame is received for any screen share
    /// </summary>
    event Action<string, ImageSource>? OnFrameReceived;

    /// <summary>
    /// Event when a screen share starts
    /// </summary>
    event Action<ScreenShareDto>? OnScreenShareStarted;

    /// <summary>
    /// Event when a screen share stops
    /// </summary>
    event Action<string>? OnScreenShareStopped;

    /// <summary>
    /// Start viewing a specific screen share
    /// </summary>
    Task StartViewingAsync(string sharerConnectionId);

    /// <summary>
    /// Stop viewing the current screen share
    /// </summary>
    Task StopViewingAsync();

    /// <summary>
    /// Process a received frame (called by VoiceService)
    /// </summary>
    void HandleFrame(string sharerConnectionId, byte[] frameData, int width, int height);

    /// <summary>
    /// Handle screen share started notification
    /// </summary>
    void HandleScreenShareStarted(string connectionId, string username, string channelId);

    /// <summary>
    /// Handle screen share stopped notification
    /// </summary>
    void HandleScreenShareStopped(string connectionId);

    /// <summary>
    /// Get the latest frame as ImageSource for a specific sharer
    /// </summary>
    ImageSource? GetLatestFrame(string sharerConnectionId);

    /// <summary>
    /// Clear all state (for disconnect)
    /// </summary>
    void Clear();
}

public class ScreenShareViewerService : IScreenShareViewerService
{
    private readonly ConcurrentDictionary<string, BitmapImage> _latestFrames = new();
    private readonly ConcurrentDictionary<string, ScreenShareDto> _screenShares = new();
    private string? _viewingConnectionId;
    private string? _ownConnectionId;

    public ObservableCollection<ScreenShareDto> ActiveScreenShares { get; } = new();
    public ScreenShareDto? CurrentlyViewing => _viewingConnectionId != null && _screenShares.TryGetValue(_viewingConnectionId, out var share) ? share : null;
    public ScreenShareDto? OwnScreenShare => _ownConnectionId != null && _screenShares.TryGetValue(_ownConnectionId, out var share) ? share : null;

    public event Action<string, ImageSource>? OnFrameReceived;
    public event Action<ScreenShareDto>? OnScreenShareStarted;
    public event Action<string>? OnScreenShareStopped;

    public void SetOwnConnectionId(string connectionId)
    {
        _ownConnectionId = connectionId;
    }

    public Task StartViewingAsync(string sharerConnectionId)
    {
        _viewingConnectionId = sharerConnectionId;
        return Task.CompletedTask;
    }

    public Task StopViewingAsync()
    {
        _viewingConnectionId = null;
        return Task.CompletedTask;
    }

    public void HandleFrame(string sharerConnectionId, byte[] frameData, int width, int height)
    {
        if (frameData == null || frameData.Length == 0) return;

        try
        {
            // Convert to BitmapImage on UI thread
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(frameData);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make thread-safe

                    _latestFrames[sharerConnectionId] = bitmap;

                    // Update screen share info
                    if (_screenShares.TryGetValue(sharerConnectionId, out var share))
                    {
                        share.Width = width;
                        share.Height = height;
                        share.Fps++;
                    }

                    OnFrameReceived?.Invoke(sharerConnectionId, bitmap);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing frame: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling frame: {ex.Message}");
        }
    }

    public void HandleScreenShareStarted(string connectionId, string username, string channelId)
    {
        var share = new ScreenShareDto
        {
            SharerConnectionId = connectionId,
            SharerUsername = username,
            ChannelId = channelId,
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };

        _screenShares[connectionId] = share;

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var existing = ActiveScreenShares.FirstOrDefault(s => s.SharerConnectionId == connectionId);
            if (existing != null)
            {
                var index = ActiveScreenShares.IndexOf(existing);
                ActiveScreenShares[index] = share;
            }
            else
            {
                ActiveScreenShares.Add(share);
            }

            OnScreenShareStarted?.Invoke(share);
        });
    }

    public void HandleScreenShareStopped(string connectionId)
    {
        _screenShares.TryRemove(connectionId, out _);
        _latestFrames.TryRemove(connectionId, out _);

        if (_viewingConnectionId == connectionId)
        {
            _viewingConnectionId = null;
        }

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var existing = ActiveScreenShares.FirstOrDefault(s => s.SharerConnectionId == connectionId);
            if (existing != null)
            {
                ActiveScreenShares.Remove(existing);
            }

            OnScreenShareStopped?.Invoke(connectionId);
        });
    }

    public ImageSource? GetLatestFrame(string sharerConnectionId)
    {
        return _latestFrames.TryGetValue(sharerConnectionId, out var frame) ? frame : null;
    }

    public void Clear()
    {
        _viewingConnectionId = null;
        _ownConnectionId = null;
        _latestFrames.Clear();
        _screenShares.Clear();

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ActiveScreenShares.Clear();
        });
    }
}
