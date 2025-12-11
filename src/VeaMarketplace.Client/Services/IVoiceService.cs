using Concentus.Enums;
using Concentus.Structs;
using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using VeaMarketplace.Shared.DTOs;

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
    IScreenSharingManager ScreenSharingManager { get; }
    event Action<string, byte[], int, int>? OnScreenFrameReceived;
    event Action<ScreenShareStats>? OnScreenShareStatsUpdated;
    Task StartScreenShareAsync();
    Task StartScreenShareAsync(DisplayInfo display);
    Task StartScreenShareAsync(DisplayInfo display, ScreenShareQuality quality);
    Task StartScreenShareAsync(DisplayInfo display, ScreenShareSettings settings);
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

    // DM Call functionality
    bool IsInCall { get; }
    string? CurrentCallId { get; }
    event Action<VoiceCallDto>? OnIncomingCall;
    event Action<VoiceCallDto>? OnCallStarted;
    event Action<VoiceCallDto>? OnCallAnswered;
    event Action<VoiceCallDto>? OnCallDeclined;
    event Action<string, string>? OnCallEnded;
    event Action<string>? OnCallFailed;
    event Action<string, bool, double>? OnCallUserSpeaking;

    Task AuthenticateForCallsAsync(string token);
    Task StartCallAsync(string recipientId);
    Task AnswerCallAsync(string callId, bool accept);
    Task EndCallAsync(string callId);
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
    private readonly ConcurrentDictionary<string, OpusDecoder> _userOpusDecoders = new();

    // Playback buffer for decoded audio (replaces complex mixing thread)

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

    // Screen sharing manager
    private readonly IScreenSharingManager _screenSharingManager;
    private DisplayInfo? _selectedDisplay;

    // Audio send queue - prevents blocking audio callback with network operations
    private readonly ConcurrentQueue<byte[]> _audioSendQueue = new();
    private CancellationTokenSource? _audioSendCts;
    private Task? _audioSendTask;

    // Opus codec for efficient audio compression
    // Reduces bandwidth from ~768kbps to ~24kbps per user
    private OpusEncoder? _opusEncoder;
    private const int OpusBitrate = 24000; // 24kbps - good for voice
    private const int OpusFrameSize = 960; // 20ms at 48kHz (48000 * 0.020 = 960 samples)

    // Mic boost - amplify input audio before sending
    private const float MicGain = 2.5f; // 2.5x boost for quiet mics

    public VoiceService()
    {
        _screenSharingManager = new ScreenSharingManager();
        _screenSharingManager.OnFrameReceived += frame =>
        {
            OnScreenFrameReceived?.Invoke(frame.SenderConnectionId, frame.Data, frame.Width, frame.Height);
        };
        _screenSharingManager.OnStatsUpdated += stats =>
        {
            OnScreenShareStatsUpdated?.Invoke(stats);
        };
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsInVoiceChannel { get; private set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking => _isSpeaking;
    public double CurrentAudioLevel => _currentAudioLevel;
    public string? CurrentChannelId { get; private set; }
    public bool IsScreenSharing => _screenSharingManager.IsSharing;
    public IScreenSharingManager ScreenSharingManager => _screenSharingManager;
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
    public event Action<ScreenShareStats>? OnScreenShareStatsUpdated;

    // Call events
    public event Action<VoiceCallDto>? OnIncomingCall;
    public event Action<VoiceCallDto>? OnCallStarted;
    public event Action<VoiceCallDto>? OnCallAnswered;
    public event Action<VoiceCallDto>? OnCallDeclined;
    public event Action<string, string>? OnCallEnded;
    public event Action<string>? OnCallFailed;
    public event Action<string, bool, double>? OnCallUserSpeaking;

    // Call state
    public bool IsInCall { get; private set; }
    public string? CurrentCallId { get; private set; }
    private VoiceCallDto? _currentCall;

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
            OnUserJoinedVoice?.Invoke(user);
        });

        _connection.On<VoiceUserState>("UserLeftVoice", user =>
        {
            _voiceUsers.TryRemove(user.ConnectionId, out _);
            _userVolumes.TryRemove(user.ConnectionId, out _);
            _mutedUsers.TryRemove(user.ConnectionId, out _);
            // Clean up user's Opus decoder
            _userOpusDecoders.TryRemove(user.ConnectionId, out _);
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
            foreach (var user in users)
            {
                _voiceUsers[user.ConnectionId] = user;
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

        // Receive audio data from other users (Opus encoded)
        // Simplified: decode and add directly to playback buffer (no complex mixing thread)
        _connection.On<string, byte[]>("ReceiveAudio", (senderConnectionId, opusData) =>
        {
            // Don't play audio if we're deafened or if user is muted locally
            if (IsDeafened || IsUserMuted(senderConnectionId)) return;
            if (opusData.Length == 0 || _bufferedWaveProvider == null) return;

            try
            {
                // Get or create per-user decoder (each user needs separate decoder state)
                var decoder = _userOpusDecoders.GetOrAdd(senderConnectionId,
                    _ => new OpusDecoder(SampleRate, Channels));

                // Decode Opus to PCM
                var pcmBuffer = new short[OpusFrameSize];
                var decodedSamples = decoder.Decode(opusData, 0, opusData.Length, pcmBuffer, 0, OpusFrameSize, false);

                if (decodedSamples > 0)
                {
                    // Apply per-user volume
                    var volume = GetUserVolume(senderConnectionId);
                    if (Math.Abs(volume - 1.0f) > 0.01f)
                    {
                        for (int i = 0; i < decodedSamples; i++)
                        {
                            pcmBuffer[i] = (short)Math.Clamp(pcmBuffer[i] * volume, short.MinValue, short.MaxValue);
                        }
                    }

                    // Convert short[] to byte[] for the buffer
                    var byteBuffer = new byte[decodedSamples * 2];
                    Buffer.BlockCopy(pcmBuffer, 0, byteBuffer, 0, byteBuffer.Length);

                    // Add directly to playback buffer - NAudio handles timing
                    try
                    {
                        _bufferedWaveProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);
                    }
                    catch
                    {
                        // Buffer overflow - DiscardOnBufferOverflow handles this
                    }
                }
            }
            catch
            {
                // Decode error - skip this frame
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
            _screenSharingManager.HandleFrameReceived(senderConnectionId, frameData, width, height);
            OnScreenFrameReceived?.Invoke(senderConnectionId, frameData, width, height);
        });

        _connection.On<string, bool>("UserScreenShareChanged", (connectionId, isSharing) =>
        {
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                user.IsScreenSharing = isSharing;
            }
            OnUserScreenShareChanged?.Invoke(connectionId, isSharing);
        });

        _connection.On<string, string, string>("ScreenShareStarted", (connectionId, username, channelId) =>
        {
            _screenSharingManager.HandleScreenShareStarted(connectionId, username);
        });

        _connection.On<string>("ScreenShareStopped", connectionId =>
        {
            _screenSharingManager.HandleScreenShareStopped(connectionId);
        });

        _connection.On<int>("ViewerCountUpdated", count =>
        {
            _screenSharingManager.HandleViewerCountUpdate(count);
        });

        // Register call handlers
        RegisterCallHandlers();
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
            // Initialize Opus encoder for sending audio
            _opusEncoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = OpusBitrate,
                Complexity = 5, // Balance between quality and CPU (0-10)
                UseVBR = true,
                SignalType = OpusSignal.OPUS_SIGNAL_VOICE
            };

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

            // Setup playback buffer - NAudio handles timing internally
            _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(200) // Smaller buffer for lower latency
            };

            // Initialize output device with low latency settings
            _waveOut = new WaveOutEvent
            {
                DeviceNumber = _outputDeviceNumber,
                DesiredLatency = 50, // Low latency playback
                NumberOfBuffers = 3
            };
            _waveOut.Init(_bufferedWaveProvider);
            _waveOut.Play();

            // Start the background audio send thread (decouples network from audio callback)
            _audioSendCts = new CancellationTokenSource();
            _audioSendTask = Task.Run(() => AudioSendLoop(_audioSendCts.Token));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio device error: {ex.Message}");
        }
    }

    /// <summary>
    /// Background loop that sends queued audio data to the server.
    /// This decouples network I/O from the audio callback thread.
    /// Encodes audio with Opus codec before sending.
    /// </summary>
    private async Task AudioSendLoop(CancellationToken cancellationToken)
    {
        // Buffer for Opus encoded output (max Opus frame is ~4000 bytes)
        var opusBuffer = new byte[4000];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_audioSendQueue.TryDequeue(out var pcmData))
                {
                    if (_connection != null && IsConnected && _opusEncoder != null)
                    {
                        // Convert byte[] PCM to short[] for Opus encoder
                        var pcmSamples = new short[pcmData.Length / 2];
                        Buffer.BlockCopy(pcmData, 0, pcmSamples, 0, pcmData.Length);

                        // Encode with Opus - dramatically reduces bandwidth
                        var encodedLength = _opusEncoder.Encode(pcmSamples, 0, OpusFrameSize, opusBuffer, 0, opusBuffer.Length);

                        if (encodedLength > 0)
                        {
                            // Create correctly sized array for sending
                            var opusData = new byte[encodedLength];
                            Buffer.BlockCopy(opusBuffer, 0, opusData, 0, encodedLength);

                            // Send the compressed audio
                            await _connection.SendAsync("SendAudio", opusData, cancellationToken).ConfigureAwait(false);
                        }
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

    private void StopAudioCapture()
    {
        // Stop background send task
        _audioSendCts?.Cancel();

        try
        {
            if (_audioSendTask != null)
            {
                _audioSendTask.Wait(TimeSpan.FromMilliseconds(500));
            }
        }
        catch { }

        _audioSendCts?.Dispose();
        _audioSendCts = null;
        _audioSendTask = null;

        // Clear queues
        while (_audioSendQueue.TryDequeue(out _)) { }
        _userOpusDecoders.Clear();

        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _bufferedWaveProvider = null;

        // Clean up Opus encoder
        _opusEncoder = null;
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

        // Send speaking state updates ONLY when state actually changes
        // This prevents flooding the server with constant updates
        if (_isSpeaking != wasSpeaking)
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
                        // Use SendAsync for fire-and-forget instead of InvokeAsync
                        await _connection.SendAsync("UpdateSpeakingState", speakingState, level).ConfigureAwait(false);
                    }
                }
                catch { }
            });
        }
        // Send periodic level updates only while actively speaking (every 500ms)
        else if (_isSpeaking && (DateTime.Now - _lastSpeakingUpdate).TotalMilliseconds > 500)
        {
            _lastSpeakingUpdate = DateTime.Now;
            var level = audioLevel;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (_connection != null && IsConnected)
                    {
                        await _connection.SendAsync("UpdateSpeakingState", true, level).ConfigureAwait(false);
                    }
                }
                catch { }
            });
        }

        // Queue audio for sending if speaking and not muted
        // The background AudioSendLoop will handle the actual network transmission
        if (_isSpeaking && !IsMuted && e.BytesRecorded > 0)
        {
            // Copy and apply mic gain boost
            var audioData = new byte[e.BytesRecorded];
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                var sample = BitConverter.ToInt16(e.Buffer, i);
                // Apply gain with clipping protection
                var boosted = (int)(sample * MicGain);
                boosted = Math.Clamp(boosted, short.MinValue, short.MaxValue);
                var bytes = BitConverter.GetBytes((short)boosted);
                audioData[i] = bytes[0];
                audioData[i + 1] = bytes[1];
            }

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

    // Screen sharing implementation - now delegates to ScreenSharingManager
    public async Task StartScreenShareAsync()
    {
        // Use primary display if none selected
        if (_selectedDisplay == null)
        {
            var displays = GetAvailableDisplays();
            _selectedDisplay = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
        }

        if (_selectedDisplay != null)
        {
            await StartScreenShareAsync(_selectedDisplay);
        }
    }

    public Task StartScreenShareAsync(DisplayInfo display)
    {
        return StartScreenShareAsync(display, ScreenShareQuality.High);
    }

    public Task StartScreenShareAsync(DisplayInfo display, ScreenShareQuality quality)
    {
        return StartScreenShareAsync(display, ScreenShareSettings.FromQuality(quality));
    }

    public async Task StartScreenShareAsync(DisplayInfo display, ScreenShareSettings settings)
    {
        if (_connection == null || !IsConnected) return;

        _selectedDisplay = display;

        // Connect the manager to our SignalR connection
        await _screenSharingManager.ConnectAsync(
            async (frameData, width, height) =>
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.SendAsync("SendScreenFrame", frameData, width, height).ConfigureAwait(false);
                }
            },
            async () =>
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.SendAsync("StartScreenShare").ConfigureAwait(false);
                }
            },
            async () =>
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.SendAsync("StopScreenShare").ConfigureAwait(false);
                }
            });

        // Start sharing
        await _screenSharingManager.StartSharingAsync(display, settings);
    }

    public async Task StopScreenShareAsync()
    {
        await _screenSharingManager.StopSharingAsync();
    }

    // === DM Call Methods ===

    public async Task AuthenticateForCallsAsync(string token)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("AuthenticateForCalls", token);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task StartCallAsync(string recipientId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("StartCall", recipientId);
        }
        catch (Exception ex)
        {
            OnCallFailed?.Invoke(ex.Message);
        }
    }

    public async Task AnswerCallAsync(string callId, bool accept)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("AnswerCall", callId, accept);

            if (accept && _currentCall != null)
            {
                IsInCall = true;
                CurrentCallId = callId;
                // Start audio for call
                StartAudioCapture();
            }
        }
        catch (Exception ex)
        {
            OnCallFailed?.Invoke(ex.Message);
        }
    }

    public async Task EndCallAsync(string callId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("EndCall", callId);
            IsInCall = false;
            CurrentCallId = null;
            _currentCall = null;
            StopAudioCapture();
        }
        catch
        {
            // Ignore errors
        }
    }

    private void RegisterCallHandlers()
    {
        if (_connection == null) return;

        _connection.On<VoiceCallDto>("IncomingCall", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _currentCall = call;
                OnIncomingCall?.Invoke(call);
            });
        });

        _connection.On<VoiceCallDto>("CallStarted", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _currentCall = call;
                CurrentCallId = call.Id;
                OnCallStarted?.Invoke(call);
            });
        });

        _connection.On<VoiceCallDto>("CallAnswered", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _currentCall = call;
                CurrentCallId = call.Id;
                IsInCall = true;
                StartAudioCapture();
                OnCallAnswered?.Invoke(call);
            });
        });

        _connection.On<VoiceCallDto>("CallDeclined", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsInCall = false;
                CurrentCallId = null;
                _currentCall = null;
                OnCallDeclined?.Invoke(call);
            });
        });

        _connection.On<string, string>("CallEnded", (callId, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsInCall = false;
                CurrentCallId = null;
                _currentCall = null;
                StopAudioCapture();
                OnCallEnded?.Invoke(callId, reason);
            });
        });

        _connection.On<string>("CallFailed", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsInCall = false;
                CurrentCallId = null;
                _currentCall = null;
                OnCallFailed?.Invoke(error);
            });
        });

        _connection.On("CallAuthSuccess", () =>
        {
            // Successfully authenticated for calls
        });

        _connection.On<string>("CallAuthFailed", error =>
        {
            OnCallFailed?.Invoke($"Call auth failed: {error}");
        });

        // Call audio receiving
        _connection.On<string, byte[]>("ReceiveCallAudio", (senderConnectionId, audioData) =>
        {
            if (IsInCall && !IsDeafened && _bufferedWaveProvider != null)
            {
                // Decode and play call audio similar to voice channel audio
                DecodeAndPlayAudio(senderConnectionId, audioData);
            }
        });

        _connection.On<string, bool, double>("CallSpeakingState", (connectionId, isSpeaking, audioLevel) =>
        {
            OnCallUserSpeaking?.Invoke(connectionId, isSpeaking, audioLevel);
        });
    }

    private void DecodeAndPlayAudio(string senderConnectionId, byte[] opusData)
    {
        if (IsUserMuted(senderConnectionId) || _bufferedWaveProvider == null) return;

        try
        {
            var decoder = _userOpusDecoders.GetOrAdd(senderConnectionId, _ => new OpusDecoder(SampleRate, Channels));
            var pcmBuffer = new short[OpusFrameSize];
            var decodedSamples = decoder.Decode(opusData, 0, opusData.Length, pcmBuffer, 0, OpusFrameSize, false);

            if (decodedSamples > 0)
            {
                var userVolume = GetUserVolume(senderConnectionId);
                var pcmBytes = new byte[decodedSamples * 2];
                for (int i = 0; i < decodedSamples; i++)
                {
                    var sample = (short)(pcmBuffer[i] * userVolume);
                    var bytes = BitConverter.GetBytes(sample);
                    pcmBytes[i * 2] = bytes[0];
                    pcmBytes[i * 2 + 1] = bytes[1];
                }
                _bufferedWaveProvider.AddSamples(pcmBytes, 0, pcmBytes.Length);
            }
        }
        catch
        {
            // Ignore decode errors
        }
    }
}
