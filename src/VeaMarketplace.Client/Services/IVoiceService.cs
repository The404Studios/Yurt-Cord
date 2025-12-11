using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace VeaMarketplace.Client.Services;

// Display/Screen information
public class DisplayInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public int Index { get; set; }

    public override string ToString() => FriendlyName;
}

// Audio device information
public class AudioDeviceInfo
{
    public int DeviceNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsInput { get; set; }

    public override string ToString() => Name;
}

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
    DisplayInfo? SelectedDisplay { get; set; }
    event Action<string, byte[], int, int>? OnScreenFrameReceived;
    Task StartScreenShareAsync();
    Task StartScreenShareAsync(DisplayInfo display);
    Task StopScreenShareAsync();

    // Device enumeration
    List<DisplayInfo> GetAvailableDisplays();
    List<AudioDeviceInfo> GetInputDevices();
    List<AudioDeviceInfo> GetOutputDevices();

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
    public bool IsScreenSharing { get; set; }
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

    // Audio device configuration
    private int _inputDeviceNumber = 0;
    private int _outputDeviceNumber = 0;
    private double _voiceActivityThreshold = 0.02;
    private double _currentAudioLevel;

    // Push-to-talk
    private bool _pushToTalkEnabled;
    private bool _isPushToTalkActive;
    private Key _pushToTalkKey = Key.V;

    // Audio quality settings
    private const int SampleRate = 48000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    // Screen sharing
    private bool _isScreenSharing;
    private CancellationTokenSource? _screenShareCts;
    private Task? _screenShareTask;
    private DisplayInfo? _selectedDisplay;

    // Audio send queue - prevents blocking audio callback with network operations
    private readonly ConcurrentQueue<byte[]> _audioSendQueue = new();
    private CancellationTokenSource? _audioSendCts;
    private Task? _audioSendTask;

    // Jitter buffer for smooth playback
    private readonly ConcurrentQueue<byte[]> _jitterBuffer = new();
    private const int JitterBufferTargetMs = 60; // Target 60ms of buffered audio
    private const int JitterBufferMaxMs = 200; // Max buffer before discarding
    private DateTime _lastPlaybackTime = DateTime.MinValue;
    private Task? _playbackTask;
    private CancellationTokenSource? _playbackCts;

    // Thread synchronization
    private readonly object _audioLock = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsInVoiceChannel { get; private set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking => _isSpeaking;
    public double CurrentAudioLevel => _currentAudioLevel;
    public string? CurrentChannelId { get; private set; }
    public bool IsScreenSharing => _isScreenSharing;
    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set => _selectedDisplay = value;
    }

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

    // Device enumeration
    public List<DisplayInfo> GetAvailableDisplays()
    {
        var displays = new List<DisplayInfo>();
        var screens = System.Windows.Forms.Screen.AllScreens;

        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            displays.Add(new DisplayInfo
            {
                DeviceName = screen.DeviceName,
                FriendlyName = screen.Primary ? $"Display {i + 1} (Primary)" : $"Display {i + 1}",
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                IsPrimary = screen.Primary,
                Index = i
            });
        }

        return displays;
    }

    public List<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo
            {
                DeviceNumber = i,
                Name = caps.ProductName,
                IsInput = true
            });
        }
        return devices;
    }

    public List<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo
            {
                DeviceNumber = i,
                Name = caps.ProductName,
                IsInput = false
            });
        }
        return devices;
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

            if (audioData.Length > 0)
            {
                // Apply per-user volume if needed
                var volume = GetUserVolume(senderConnectionId);
                byte[] processedData;

                if (Math.Abs(volume - 1.0f) > 0.01f)
                {
                    // Apply per-user volume on a separate array
                    processedData = ApplyVolume(audioData, volume);
                }
                else
                {
                    processedData = audioData;
                }

                // Add to jitter buffer instead of directly to playback
                // This smooths out network timing variations
                // Limit jitter buffer size to prevent excessive delay
                if (_jitterBuffer.Count < 25) // ~500ms max buffered audio
                {
                    _jitterBuffer.Enqueue(processedData);
                }
                else
                {
                    // Buffer is full, discard oldest and add new (keeps audio current)
                    _jitterBuffer.TryDequeue(out _);
                    _jitterBuffer.Enqueue(processedData);
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
            // Configure audio capture with small buffer for low latency
            _waveIn = new WaveInEvent
            {
                DeviceNumber = _inputDeviceNumber,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 20, // 20ms buffers for low latency
                NumberOfBuffers = 3
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();

            // Setup playback buffer with proper size for jitter compensation
            _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(500) // 500ms buffer capacity
            };

            // Initialize output device with low latency settings
            _waveOut = new WaveOutEvent
            {
                DeviceNumber = _outputDeviceNumber,
                DesiredLatency = 50, // Lower latency for better responsiveness
                NumberOfBuffers = 3  // More buffers for smoother playback
            };
            _waveOut.Init(_bufferedWaveProvider);
            _waveOut.Play();

            // Start the background audio send thread (decouples network from audio callback)
            _audioSendCts = new CancellationTokenSource();
            _audioSendTask = Task.Run(() => AudioSendLoop(_audioSendCts.Token));

            // Start jitter buffer playback thread
            _playbackCts = new CancellationTokenSource();
            _playbackTask = Task.Run(() => JitterBufferPlaybackLoop(_playbackCts.Token));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio device error: {ex.Message}");
        }
    }

    /// <summary>
    /// Background loop that sends queued audio data to the server.
    /// This decouples network I/O from the audio callback thread.
    /// </summary>
    private async Task AudioSendLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_audioSendQueue.TryDequeue(out var audioData))
                {
                    if (_connection != null && IsConnected)
                    {
                        // Use ConfigureAwait(false) to avoid context switching overhead
                        await _connection.InvokeAsync("SendAudio", audioData, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    // No data to send, wait a bit to avoid busy-spinning
                    await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore send errors - network issues shouldn't crash audio
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Jitter buffer playback loop - smooths out network timing variations
    /// </summary>
    private async Task JitterBufferPlaybackLoop(CancellationToken cancellationToken)
    {
        const int playbackIntervalMs = 20; // Play audio every 20ms
        var bytesPerInterval = (SampleRate * Channels * (BitsPerSample / 8) * playbackIntervalMs) / 1000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Calculate how much data should be in the buffer
                var bufferCount = _jitterBuffer.Count;

                // If we have enough buffered data, play it
                if (_jitterBuffer.TryDequeue(out var audioData) && _bufferedWaveProvider != null)
                {
                    lock (_audioLock)
                    {
                        // Only add if buffer isn't too full
                        if (_bufferedWaveProvider.BufferedBytes < _bufferedWaveProvider.BufferLength * 0.8)
                        {
                            _bufferedWaveProvider.AddSamples(audioData, 0, audioData.Length);
                        }
                    }
                }

                // Maintain consistent timing
                await Task.Delay(playbackIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void StopAudioCapture()
    {
        // Stop background tasks first
        _audioSendCts?.Cancel();
        _playbackCts?.Cancel();

        try
        {
            // Wait for tasks to complete with timeout
            if (_audioSendTask != null)
            {
                _audioSendTask.Wait(TimeSpan.FromMilliseconds(500));
            }
            if (_playbackTask != null)
            {
                _playbackTask.Wait(TimeSpan.FromMilliseconds(500));
            }
        }
        catch { }

        _audioSendCts?.Dispose();
        _audioSendCts = null;
        _audioSendTask = null;

        _playbackCts?.Dispose();
        _playbackCts = null;
        _playbackTask = null;

        // Clear queues
        while (_audioSendQueue.TryDequeue(out _)) { }
        while (_jitterBuffer.TryDequeue(out _)) { }

        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _bufferedWaveProvider = null;
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        // CRITICAL: This callback runs on the audio thread and must NOT block!
        // Any blocking here causes crackling/stuttering.

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

        // Fire events on thread pool to avoid blocking audio thread
        ThreadPool.QueueUserWorkItem(_ => OnLocalAudioLevel?.Invoke(audioLevel));

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

        // Notify if speaking state changed (on thread pool)
        if (_isSpeaking != wasSpeaking)
        {
            ThreadPool.QueueUserWorkItem(_ => OnSpeakingChanged?.Invoke(_isSpeaking));
        }

        // Send speaking state updates (fire-and-forget on thread pool)
        if (_isSpeaking != wasSpeaking || (DateTime.Now - _lastSpeakingUpdate).TotalMilliseconds > 100)
        {
            _lastSpeakingUpdate = DateTime.Now;
            var speakingState = _isSpeaking;
            var level = audioLevel;

            // Fire and forget - don't block audio thread
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_connection != null && IsConnected)
                    {
                        await _connection.InvokeAsync("UpdateSpeakingState", speakingState, level).ConfigureAwait(false);
                    }
                }
                catch { }
            });
        }

        // Queue audio for sending if speaking and not muted
        // The background AudioSendLoop will handle the actual network transmission
        if (_isSpeaking && !IsMuted && e.BytesRecorded > 0)
        {
            // Copy the audio buffer (must copy since original buffer will be reused)
            var audioData = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);

            // Add to send queue - background thread will send it
            // Limit queue size to prevent memory buildup if network is slow
            if (_audioSendQueue.Count < 50) // ~1 second of audio max
            {
                _audioSendQueue.Enqueue(audioData);
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
        // Use primary display if none selected
        if (_selectedDisplay == null)
        {
            var displays = GetAvailableDisplays();
            _selectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
        }
        await StartScreenShareInternal();
    }

    public async Task StartScreenShareAsync(DisplayInfo display)
    {
        _selectedDisplay = display;
        await StartScreenShareInternal();
    }

    private async Task StartScreenShareInternal()
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
            // Get screen bounds from selected display or primary
            int screenLeft, screenTop, screenWidth, screenHeight;

            if (_selectedDisplay != null)
            {
                screenLeft = _selectedDisplay.Left;
                screenTop = _selectedDisplay.Top;
                screenWidth = _selectedDisplay.Width;
                screenHeight = _selectedDisplay.Height;
            }
            else
            {
                // Fallback to primary screen
                screenLeft = 0;
                screenTop = 0;
                screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
            }

            using var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);

            // Capture the selected screen region
            graphics.CopyFromScreen(screenLeft, screenTop, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));

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
