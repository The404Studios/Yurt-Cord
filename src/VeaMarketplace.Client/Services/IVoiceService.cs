using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.Windows.Input;

namespace VeaMarketplace.Client.Services;

public interface IVoiceService
{
    bool IsConnected { get; }
    bool IsInVoiceChannel { get; }
    bool IsMuted { get; set; }
    bool IsDeafened { get; set; }
    bool IsSpeaking { get; }
    double CurrentAudioLevel { get; }
    string? CurrentChannelId { get; }

    // Push-to-talk
    bool PushToTalkEnabled { get; set; }
    Key PushToTalkKey { get; set; }
    bool IsPushToTalkActive { get; }

    // Per-user volume control
    void SetUserVolume(string connectionId, float volume);
    float GetUserVolume(string connectionId);
    void SetUserMuted(string connectionId, bool muted);
    bool IsUserMuted(string connectionId);

    // Screen sharing
    bool IsScreenSharing { get; }
    event Action<string, byte[], int, int>? OnScreenFrameReceived;
    Task StartScreenShareAsync();
    Task StopScreenShareAsync();

    event Action<VoiceUserState>? OnUserJoinedVoice;
    event Action<VoiceUserState>? OnUserLeftVoice;
    event Action<string, string, bool, double>? OnUserSpeaking;
    event Action<List<VoiceUserState>>? OnVoiceChannelUsers;
    event Action<double>? OnLocalAudioLevel;
    event Action<bool>? OnSpeakingChanged;
    event Action<string>? OnUserDisconnectedByAdmin;
    event Action<string, string>? OnUserMovedToChannel;
    event Action<string, bool>? OnUserScreenShareChanged;

    Task ConnectAsync();
    Task DisconnectAsync();
    Task JoinVoiceChannelAsync(string channelId, string userId, string username, string avatarUrl);
    Task LeaveVoiceChannelAsync();

    // Audio device management
    void SetInputDevice(int deviceNumber);
    void SetOutputDevice(int deviceNumber);
    void SetVoiceActivityThreshold(double threshold);

    // Push-to-talk control
    void SetPushToTalkActive(bool active);

    // Admin controls
    Task DisconnectUserAsync(string connectionId);
    Task MoveUserToChannelAsync(string connectionId, string targetChannelId);
    Task KickUserAsync(string userId, string reason);
    Task BanUserAsync(string userId, string reason, TimeSpan? duration);
}

public class VoiceUserState
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public double AudioLevel { get; set; }
}

public class VoiceService : IVoiceService, IAsyncDisposable
{
    private HubConnection? _connection;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private const string HubUrl = "http://162.248.94.23:5000/hubs/voice";
    private bool _isSpeaking;
    private DateTime _lastSpeakingUpdate = DateTime.MinValue;
    private readonly ConcurrentDictionary<string, VoiceUserState> _voiceUsers = new();

    // Per-user audio control
    private readonly ConcurrentDictionary<string, float> _userVolumes = new();
    private readonly ConcurrentDictionary<string, bool> _mutedUsers = new();
    private readonly ConcurrentDictionary<string, BufferedWaveProvider> _userAudioBuffers = new();
    private MixingWaveProvider32? _mixer;
    private float _masterVolume = 1.0f;

    // Audio device configuration
    private int _inputDeviceNumber = 0;
    private int _outputDeviceNumber = 0;
    private double _voiceActivityThreshold = 0.02;
    private double _currentAudioLevel;

    // Push-to-talk
    private bool _pushToTalkEnabled;
    private bool _isPushToTalkActive;
    private Key _pushToTalkKey = Key.V;

    // Audio quality settings - FIXED for smoother audio
    private const int SampleRate = 48000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int CaptureBufferMs = 40; // Larger capture buffer (was 20)
    private const int PlaybackLatencyMs = 150; // Higher latency for smoother playback
    private const int JitterBufferMs = 100; // Jitter buffer to handle network variation

    // Screen sharing
    private bool _isScreenSharing;
    private CancellationTokenSource? _screenShareCts;
    private Task? _screenShareTask;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsInVoiceChannel { get; private set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking => _isSpeaking;
    public double CurrentAudioLevel => _currentAudioLevel;
    public string? CurrentChannelId { get; private set; }
    public bool IsScreenSharing => _isScreenSharing;

    // Push-to-talk properties
    public bool PushToTalkEnabled
    {
        get => _pushToTalkEnabled;
        set => _pushToTalkEnabled = value;
    }

    public Key PushToTalkKey
    {
        get => _pushToTalkKey;
        set => _pushToTalkKey = value;
    }

    public bool IsPushToTalkActive => _isPushToTalkActive;

    public event Action<VoiceUserState>? OnUserJoinedVoice;
    public event Action<VoiceUserState>? OnUserLeftVoice;
    public event Action<string, string, bool, double>? OnUserSpeaking;
    public event Action<List<VoiceUserState>>? OnVoiceChannelUsers;
    public event Action<double>? OnLocalAudioLevel;
    public event Action<bool>? OnSpeakingChanged;
    public event Action<string>? OnUserDisconnectedByAdmin;
    public event Action<string, string>? OnUserMovedToChannel;
    public event Action<string, byte[], int, int>? OnScreenFrameReceived;
    public event Action<string, bool>? OnUserScreenShareChanged;

    // Per-user volume control
    public void SetUserVolume(string connectionId, float volume)
    {
        _userVolumes[connectionId] = Math.Clamp(volume, 0f, 2f);
    }

    public float GetUserVolume(string connectionId)
    {
        return _userVolumes.TryGetValue(connectionId, out var vol) ? vol : 1f;
    }

    public void SetUserMuted(string connectionId, bool muted)
    {
        _mutedUsers[connectionId] = muted;
    }

    public bool IsUserMuted(string connectionId)
    {
        return _mutedUsers.TryGetValue(connectionId, out var muted) && muted;
    }

    public void SetInputDevice(int deviceNumber)
    {
        _inputDeviceNumber = deviceNumber;
        if (IsInVoiceChannel)
        {
            // Restart audio capture with new device
            StopAudioCapture();
            StartAudioCapture();
        }
    }

    public void SetOutputDevice(int deviceNumber)
    {
        _outputDeviceNumber = deviceNumber;
        if (IsInVoiceChannel)
        {
            // Restart audio output with new device
            StopAudioCapture();
            StartAudioCapture();
        }
    }

    public void SetVoiceActivityThreshold(double threshold)
    {
        _voiceActivityThreshold = Math.Clamp(threshold, 0.001, 0.5);
    }

    public void SetPushToTalkActive(bool active)
    {
        _isPushToTalkActive = active;
    }

    public async Task ConnectAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers();
        await _connection.StartAsync();
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        _connection.On<VoiceUserState>("UserJoinedVoice", user =>
        {
            _voiceUsers[user.ConnectionId] = user;
            // Create audio buffer for this user
            var buffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(1)
            };
            _userAudioBuffers[user.ConnectionId] = buffer;
            OnUserJoinedVoice?.Invoke(user);
        });

        _connection.On<VoiceUserState>("UserLeftVoice", user =>
        {
            _voiceUsers.TryRemove(user.ConnectionId, out _);
            _userAudioBuffers.TryRemove(user.ConnectionId, out _);
            _userVolumes.TryRemove(user.ConnectionId, out _);
            _mutedUsers.TryRemove(user.ConnectionId, out _);
            OnUserLeftVoice?.Invoke(user);
        });

        _connection.On<string, string, bool, double>("UserSpeaking", (connectionId, username, isSpeaking, audioLevel) =>
        {
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                user.IsSpeaking = isSpeaking;
                user.AudioLevel = audioLevel;
            }
            OnUserSpeaking?.Invoke(connectionId, username, isSpeaking, audioLevel);
        });

        _connection.On<List<VoiceUserState>>("VoiceChannelUsers", users =>
        {
            _voiceUsers.Clear();
            _userAudioBuffers.Clear();
            foreach (var user in users)
            {
                _voiceUsers[user.ConnectionId] = user;
                // Create audio buffer for each user
                var buffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(1)
                };
                _userAudioBuffers[user.ConnectionId] = buffer;
            }
            OnVoiceChannelUsers?.Invoke(users);
        });

        _connection.On<string, bool, bool>("VoiceStateUpdated", (connectionId, isMuted, isDeafened) =>
        {
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                user.IsMuted = isMuted;
                user.IsDeafened = isDeafened;
            }
        });

        // Receive audio data from other users
        _connection.On<string, byte[]>("ReceiveAudio", (senderConnectionId, audioData) =>
        {
            // Don't play audio if we're deafened or if user is muted locally
            if (IsDeafened || IsUserMuted(senderConnectionId)) return;

            // Add audio to the playback buffer with user-specific volume
            if (_bufferedWaveProvider != null && audioData.Length > 0)
            {
                var volume = GetUserVolume(senderConnectionId);
                if (volume != 1.0f)
                {
                    // Apply per-user volume
                    var adjustedData = ApplyVolume(audioData, volume);
                    _bufferedWaveProvider.AddSamples(adjustedData, 0, adjustedData.Length);
                }
                else
                {
                    _bufferedWaveProvider.AddSamples(audioData, 0, audioData.Length);
                }
            }
        });

        // Handle being disconnected by admin
        _connection.On<string>("DisconnectedByAdmin", reason =>
        {
            OnUserDisconnectedByAdmin?.Invoke(reason);
            _ = LeaveVoiceChannelAsync();
        });

        // Handle being moved to another channel
        _connection.On<string, string>("MovedToChannel", (newChannelId, movedBy) =>
        {
            CurrentChannelId = newChannelId;
            OnUserMovedToChannel?.Invoke(newChannelId, movedBy);
        });

        // Screen sharing handlers
        _connection.On<string, byte[], int, int>("ReceiveScreenFrame", (senderConnectionId, frameData, width, height) =>
        {
            OnScreenFrameReceived?.Invoke(senderConnectionId, frameData, width, height);
        });

        _connection.On<string, bool>("UserScreenShareChanged", (connectionId, isSharing) =>
        {
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                // Could add IsScreenSharing to VoiceUserState
            }
            OnUserScreenShareChanged?.Invoke(connectionId, isSharing);
        });
    }

    private byte[] ApplyVolume(byte[] audioData, float volume)
    {
        var result = new byte[audioData.Length];
        for (var i = 0; i < audioData.Length; i += 2)
        {
            var sample = BitConverter.ToInt16(audioData, i);
            sample = (short)Math.Clamp(sample * volume, short.MinValue, short.MaxValue);
            var bytes = BitConverter.GetBytes(sample);
            result[i] = bytes[0];
            result[i + 1] = bytes[1];
        }
        return result;
    }

    public async Task JoinVoiceChannelAsync(string channelId, string userId, string username, string avatarUrl)
    {
        if (_connection == null || !IsConnected) return;

        await _connection.InvokeAsync("JoinVoiceChannel", channelId, userId, username, avatarUrl);
        IsInVoiceChannel = true;
        CurrentChannelId = channelId;

        StartAudioCapture();
    }

    public async Task LeaveVoiceChannelAsync()
    {
        StopAudioCapture();

        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("LeaveVoiceChannel");
        }

        IsInVoiceChannel = false;
        CurrentChannelId = null;
        _voiceUsers.Clear();
    }

    private void StartAudioCapture()
    {
        try
        {
            // Configure audio capture with larger buffer for stability
            _waveIn = new WaveInEvent
            {
                DeviceNumber = _inputDeviceNumber,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = CaptureBufferMs, // Larger buffer (40ms) for more stable capture
                NumberOfBuffers = 3 // Multiple buffers to reduce underruns
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();

            // Setup playback with jitter buffer for network variation
            _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(500), // Larger buffer capacity
                ReadFully = false // Don't wait for full buffer - reduces latency
            };

            // Pre-fill buffer with silence to create jitter buffer
            var silenceBytes = new byte[SampleRate * (BitsPerSample / 8) * Channels * JitterBufferMs / 1000];
            _bufferedWaveProvider.AddSamples(silenceBytes, 0, silenceBytes.Length);

            _waveOut = new WaveOutEvent
            {
                DeviceNumber = _outputDeviceNumber,
                DesiredLatency = PlaybackLatencyMs, // Higher latency for smoother playback
                NumberOfBuffers = 3 // Multiple buffers to reduce clicks
            };
            _waveOut.Init(_bufferedWaveProvider);
            _waveOut.Play();
        }
        catch
        {
            // Audio device not available
        }
    }

    private void StopAudioCapture()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _bufferedWaveProvider = null;
    }

    private async void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_connection == null || !IsConnected) return;

        // Calculate audio level (RMS for better accuracy)
        var sum = 0.0;
        var sampleCount = e.BytesRecorded / 2;
        for (var i = 0; i < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768.0;
            sum += sample * sample;
        }
        var rms = Math.Sqrt(sum / sampleCount);
        var audioLevel = Math.Min(1.0, rms * 3); // Scale for better visualization

        _currentAudioLevel = audioLevel;
        OnLocalAudioLevel?.Invoke(audioLevel);

        // Determine if speaking based on mode
        var wasSpeaking = _isSpeaking;
        if (_pushToTalkEnabled)
        {
            // Push-to-talk mode: only speak when PTT key is held
            _isSpeaking = _isPushToTalkActive && !IsMuted && audioLevel > _voiceActivityThreshold;
        }
        else
        {
            // Open mic mode: voice activity detection
            _isSpeaking = !IsMuted && audioLevel > _voiceActivityThreshold;
        }

        // Notify if speaking state changed
        if (_isSpeaking != wasSpeaking)
        {
            OnSpeakingChanged?.Invoke(_isSpeaking);
        }

        // Send speaking state updates
        if (_isSpeaking != wasSpeaking || (DateTime.Now - _lastSpeakingUpdate).TotalMilliseconds > 100)
        {
            _lastSpeakingUpdate = DateTime.Now;
            try
            {
                await _connection.InvokeAsync("UpdateSpeakingState", _isSpeaking, audioLevel);
            }
            catch
            {
                // Ignore send errors
            }
        }

        // ACTUALLY SEND THE AUDIO DATA if speaking and not muted
        if (_isSpeaking && !IsMuted && e.BytesRecorded > 0)
        {
            try
            {
                // Copy the audio buffer to send
                var audioData = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, audioData, e.BytesRecorded);

                // Send audio to the server for broadcasting to other users
                await _connection.InvokeAsync("SendAudio", audioData);
            }
            catch
            {
                // Ignore send errors - network issues shouldn't crash audio
            }
        }
    }

    public async Task DisconnectAsync()
    {
        await LeaveVoiceChannelAsync();

        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }

    // Admin control methods
    public async Task DisconnectUserAsync(string connectionId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("DisconnectUser", connectionId);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task MoveUserToChannelAsync(string connectionId, string targetChannelId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("MoveUserToChannel", connectionId, targetChannelId);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task KickUserAsync(string userId, string reason)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("KickUser", userId, reason);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task BanUserAsync(string userId, string reason, TimeSpan? duration)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("BanUser", userId, reason, duration?.TotalMinutes);
        }
        catch
        {
            // Ignore errors
        }
    }

    // Screen sharing implementation
    public async Task StartScreenShareAsync()
    {
        if (_connection == null || !IsConnected || _isScreenSharing) return;

        _isScreenSharing = true;
        _screenShareCts = new CancellationTokenSource();

        // Notify others that we started screen sharing
        try
        {
            await _connection.InvokeAsync("StartScreenShare");
        }
        catch
        {
            _isScreenSharing = false;
            return;
        }

        // Start capture task
        _screenShareTask = Task.Run(() => CaptureScreenLoop(_screenShareCts.Token), _screenShareCts.Token);
    }

    public async Task StopScreenShareAsync()
    {
        if (!_isScreenSharing) return;

        _screenShareCts?.Cancel();
        _isScreenSharing = false;

        if (_screenShareTask != null)
        {
            try
            {
                await _screenShareTask;
            }
            catch (OperationCanceledException) { }
        }

        _screenShareTask = null;
        _screenShareCts?.Dispose();
        _screenShareCts = null;

        // Notify others that we stopped screen sharing
        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.InvokeAsync("StopScreenShare");
            }
            catch { }
        }
    }

    private async Task CaptureScreenLoop(CancellationToken cancellationToken)
    {
        const int targetFps = 60;
        const int frameDelayMs = 1000 / targetFps;
        const int targetWidth = 1920;
        const int targetHeight = 1080;

        while (!cancellationToken.IsCancellationRequested && _connection != null && IsConnected)
        {
            try
            {
                var frameStart = DateTime.UtcNow;

                // Capture screen using GDI+ (cross-compatible approach)
                var frameData = CaptureScreen(targetWidth, targetHeight);

                if (frameData != null && frameData.Length > 0)
                {
                    // Send frame in chunks if needed (SignalR has message size limits)
                    const int chunkSize = 64 * 1024; // 64KB chunks

                    if (frameData.Length <= chunkSize)
                    {
                        await _connection.InvokeAsync("SendScreenFrame", frameData, targetWidth, targetHeight);
                    }
                    else
                    {
                        // For large frames, compress more aggressively or skip
                        // This is a simplified version - production would use proper streaming
                        var compressedData = CompressFrame(frameData, targetWidth, targetHeight, 30); // Lower quality for large frames
                        if (compressedData.Length <= chunkSize * 4) // Allow up to 256KB
                        {
                            await _connection.InvokeAsync("SendScreenFrame", compressedData, targetWidth, targetHeight);
                        }
                    }
                }

                // Maintain target framerate
                var elapsed = (DateTime.UtcNow - frameStart).TotalMilliseconds;
                if (elapsed < frameDelayMs)
                {
                    await Task.Delay((int)(frameDelayMs - elapsed), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue on errors, but add small delay
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private byte[]? CaptureScreen(int targetWidth, int targetHeight)
    {
        try
        {
            // Get primary screen bounds
            var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

            using var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);

            // Capture the screen
            graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));

            // Resize if needed
            System.Drawing.Bitmap finalBitmap;
            if (screenWidth != targetWidth || screenHeight != targetHeight)
            {
                finalBitmap = new System.Drawing.Bitmap(targetWidth, targetHeight);
                using var resizeGraphics = System.Drawing.Graphics.FromImage(finalBitmap);
                resizeGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                resizeGraphics.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
            }
            else
            {
                finalBitmap = bitmap;
            }

            // Compress to JPEG
            using var ms = new MemoryStream();
            var encoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(e => e.MimeType == "image/jpeg");

            if (encoder != null)
            {
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, 50L); // 50% quality for balance

                finalBitmap.Save(ms, encoder, encoderParams);
            }
            else
            {
                finalBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            if (finalBitmap != bitmap)
            {
                finalBitmap.Dispose();
            }

            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private byte[] CompressFrame(byte[] frameData, int width, int height, int quality)
    {
        try
        {
            using var inputMs = new MemoryStream(frameData);
            using var image = System.Drawing.Image.FromStream(inputMs);
            using var outputMs = new MemoryStream();

            var encoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(e => e.MimeType == "image/jpeg");

            if (encoder != null)
            {
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, (long)quality);

                image.Save(outputMs, encoder, encoderParams);
            }
            else
            {
                image.Save(outputMs, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            return outputMs.ToArray();
        }
        catch
        {
            return frameData;
        }
    }
}
