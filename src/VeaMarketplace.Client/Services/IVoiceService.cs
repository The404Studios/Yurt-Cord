using Concentus.Enums;
using Concentus.Structs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
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

// Video device information
public class VideoDeviceInfo
{
    public int DeviceIndex { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DefaultWidth { get; set; } = 640;
    public int DefaultHeight { get; set; } = 480;
    public int[] SupportedFrameRates { get; set; } = [15, 24, 30];

    public override string ToString() => Name;
}

// Video capture settings
public class VideoCaptureSettings
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int FrameRate { get; set; } = 30;
    public int Quality { get; set; } = 75; // JPEG quality 0-100

    public static VideoCaptureSettings Default => new();
    public static VideoCaptureSettings LowBandwidth => new() { Width = 320, Height = 240, FrameRate = 15, Quality = 60 };
    public static VideoCaptureSettings HighQuality => new() { Width = 1280, Height = 720, FrameRate = 30, Quality = 85 };
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

    // Master volume control (0.0 to 2.0, 1.0 = 100%)
    float MasterVolume { get; set; }

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
    event Action<byte[], int, int>? OnLocalScreenFrameReady;
    event Action<ScreenShareStats>? OnScreenShareStatsUpdated;
    Task StartScreenShareAsync();
    Task StartScreenShareAsync(DisplayInfo display);
    Task StartScreenShareAsync(DisplayInfo display, ScreenShareQuality quality);
    Task StartScreenShareAsync(DisplayInfo display, ScreenShareSettings settings);
    Task StopScreenShareAsync();

    // Video capture
    bool IsVideoEnabled { get; }
    VideoDeviceInfo? SelectedVideoDevice { get; set; }
    event Action<byte[], int, int>? OnLocalVideoFrameReady;
    event Action<string, byte[], int, int>? OnRemoteVideoFrameReceived;
    event Action<string, bool>? OnUserVideoStateChanged;
    List<VideoDeviceInfo> GetVideoDevices();
    Task StartVideoAsync();
    Task StartVideoAsync(VideoDeviceInfo device);
    Task StartVideoAsync(VideoDeviceInfo device, VideoCaptureSettings settings);
    Task StopVideoAsync();

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
    event Action<bool, string>? OnConnectionStateChanged;  // (isConnected, message)

    // WebRTC signaling events (legacy - kept for compatibility)
    event Action<string, string>? OnReceiveOffer; // senderConnectionId, offer
    event Action<string, string>? OnReceiveAnswer; // senderConnectionId, answer
    event Action<string, string>? OnReceiveIceCandidate; // senderConnectionId, candidate

    // WebRTC P2P signaling events (new)
    event Action<string, string, string>? OnP2PConnectionRequested; // connectionId, userId, username
    event Action<string, string, string, string>? OnWebRTCOfferReceived; // connectionId, userId, username, sdpOffer
    event Action<string, string, string, string>? OnWebRTCAnswerReceived; // connectionId, userId, username, sdpAnswer
    event Action<string, string, string, int>? OnICECandidateReceived; // connectionId, candidate, sdpMid, sdpMLineIndex
    event Action<string>? OnP2PConnected; // connectionId
    event Action<string, string>? OnP2PFailed; // connectionId, reason

    // Error events
    event Action<string>? OnVoiceError;
    event Action<string>? OnCallError;

    // Screen sharing quality events
    event Action<string, string>? OnQualityChangeRequested; // viewerConnectionId, qualityPreset
    event Action<string, string>? OnJoinedScreenShare; // sharerConnectionId, sharerUsername
    event Action<List<ActiveScreenShareDto>>? OnActiveScreenShares;
    event Action<string, string>? OnQualityRequested; // viewerConnectionId, quality

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
    Task InviteToChannelAsync(string userId, string channelId);
    Task MuteUserAsync(string userId, string channelId);

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

    // Nudge system
    event Action<NudgeDto>? OnNudgeReceived;
    event Action<NudgeDto>? OnNudgeSent;
    event Action<string>? OnNudgeError;
    Task SendNudgeAsync(string targetUserId, string? message = null);

    // Group call functionality
    bool IsInGroupCall { get; }
    string? CurrentGroupCallId { get; }
    event Action<GroupCallDto>? OnGroupCallStarted;
    event Action<GroupCallInviteDto>? OnGroupCallInvite;
    event Action<GroupCallDto>? OnGroupCallUpdated;
    event Action<string, GroupCallParticipantDto>? OnGroupCallParticipantJoined;
    event Action<string, string>? OnGroupCallParticipantLeft;
    event Action<string, string>? OnGroupCallEnded;
    event Action<string>? OnGroupCallError;
    event Action<string, string, bool, double>? OnGroupCallUserSpeaking; // callId, userId, isSpeaking, audioLevel
    event Action<string, string>? OnGroupCallInviteDeclined; // callId, userId

    Task StartGroupCallAsync(string name, List<string> invitedUserIds);
    Task JoinGroupCallAsync(string callId);
    Task LeaveGroupCallAsync(string callId);
    Task InviteToGroupCallAsync(string callId, string userId);
    Task DeclineGroupCallAsync(string callId);

    // Voice rooms (public rooms browser)
    event Action<List<VoiceRoomDto>, int, int, int>? OnPublicVoiceRoomsReceived;
    event Action<VoiceRoomDto>? OnVoiceRoomCreated;
    event Action<VoiceRoomDto>? OnVoiceRoomJoined;
    event Action<VoiceRoomDto>? OnVoiceRoomUpdated;
    event Action<VoiceRoomDto>? OnVoiceRoomAdded;
    event Action<string>? OnVoiceRoomRemoved;
    event Action<string>? OnVoiceRoomError;
    event Action<VoiceRoomParticipantDto>? OnVoiceRoomParticipantJoined;
    event Action<VoiceRoomParticipantDto>? OnVoiceRoomParticipantLeft;
    event Action<VoiceRoomParticipantDto>? OnVoiceRoomHostChanged;
    event Action<string, string>? OnVoiceRoomClosed; // roomId, reason
    event Action<string, string>? OnVoiceRoomKicked; // roomId, reason
    event Action<VoiceRoomParticipantDto>? OnVoiceRoomModeratorAdded;

    // Kick-related events for voice channels
    event Action<string, string>? OnKickedFromChannel; // channelId, reason
    event Action<string, string, string>? OnUserKickedFromRoom; // roomId, userId, reason
    event Action<string>? OnKickSuccess; // userId that was kicked

    Task GetPublicVoiceRoomsAsync(VoiceRoomCategory? category = null, string? query = null, int page = 1, int pageSize = 20);
    Task CreateVoiceRoomAsync(CreateVoiceRoomDto dto);
    Task JoinVoiceRoomAsync(string roomId, string? password = null);
    Task LeaveVoiceRoomAsync(string roomId);
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
    public bool IsVideoEnabled { get; set; }
}

public class ActiveScreenShareDto
{
    public string SharerConnectionId { get; set; } = string.Empty;
    public string SharerUsername { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public int ViewerCount { get; set; }
    public int LastWidth { get; set; }
    public int LastHeight { get; set; }
}

/// <summary>
/// Helper class to enumerate video capture devices using DirectShow
/// Uses Windows Management Instrumentation as a fallback
/// </summary>
internal class DirectShowVideoDeviceEnumerator
{
    public List<VideoDeviceInfo> EnumerateDevices()
    {
        var devices = new List<VideoDeviceInfo>();

        try
        {
            // Try WMI first for webcam enumeration
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Image' OR PNPClass = 'Camera')");

            var index = 0;
            foreach (var device in searcher.Get())
            {
                var name = device["Caption"]?.ToString() ?? device["Name"]?.ToString();
                var deviceId = device["DeviceID"]?.ToString();

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(deviceId))
                {
                    devices.Add(new VideoDeviceInfo
                    {
                        DeviceIndex = index++,
                        DeviceId = deviceId,
                        Name = name,
                        DefaultWidth = 640,
                        DefaultHeight = 480,
                        SupportedFrameRates = [15, 24, 30]
                    });
                }
            }
        }
        catch
        {
            // WMI failed, try fallback
        }

        // Fallback: check for integrated webcam
        if (devices.Count == 0)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%camera%' OR Name LIKE '%webcam%'");

                var index = 0;
                foreach (var device in searcher.Get())
                {
                    var name = device["Caption"]?.ToString() ?? device["Name"]?.ToString();
                    var deviceId = device["DeviceID"]?.ToString();

                    if (!string.IsNullOrEmpty(name))
                    {
                        devices.Add(new VideoDeviceInfo
                        {
                            DeviceIndex = index++,
                            DeviceId = deviceId ?? $"device_{index}",
                            Name = name,
                            DefaultWidth = 640,
                            DefaultHeight = 480,
                            SupportedFrameRates = [15, 24, 30]
                        });
                    }
                }
            }
            catch
            {
                // Fallback also failed
            }
        }

        return devices;
    }
}

public class VoiceService : IVoiceService, IAsyncDisposable
{
    private HubConnection? _connection;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private static readonly string HubUrl = AppConstants.Hubs.GetVoiceUrl();
    private volatile bool _isSpeaking;  // Volatile for thread-safe reads/writes
    private DateTime _lastSpeakingUpdate = DateTime.MinValue;
    private readonly ConcurrentDictionary<string, VoiceUserState> _voiceUsers = new();

    // Per-user audio control
    private readonly ConcurrentDictionary<string, float> _userVolumes = new();
    private readonly ConcurrentDictionary<string, bool> _mutedUsers = new();
    private readonly ConcurrentDictionary<string, OpusDecoder> _userOpusDecoders = new();

    // Master volume control
    private float _masterVolume = 1.0f;

    // Playback buffer for decoded audio (replaces complex mixing thread)

    // Audio device configuration
    private int _inputDeviceNumber;
    private int _outputDeviceNumber;
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

    // HTTP-based screen streaming (reduces voice lag)
    private HttpScreenStreamService? _httpStreamService;
    private bool _useHttpStreaming = true; // Enable HTTP streaming by default
    private readonly ConcurrentDictionary<string, long> _lastFrameNumbers = new();
    private DisplayInfo? _selectedDisplay;

    // Audio send queue - prevents blocking audio callback with network operations
    private readonly ConcurrentQueue<byte[]> _audioSendQueue = new();
    private CancellationTokenSource? _audioSendCts;
    private Thread? _audioSendThread;  // Dedicated high-priority thread for audio sending

    // Opus codec for efficient audio compression
    // Reduces bandwidth from ~768kbps to ~24kbps per user
    private OpusEncoder? _opusEncoder;
    private const int OpusBitrate = 24000; // 24kbps - good for voice
    private const int OpusFrameSize = 960; // 20ms at 48kHz (48000 * 0.020 = 960 samples)

    // Audio timing constants
    private const int AudioBufferMs = 20; // Audio buffer size in milliseconds
    private const int PlaybackLatencyMs = 50; // Desired playback latency
    private const int MaxAudioQueueSize = 50; // Max audio packets in send queue (~1 second)
    private const int ThreadJoinTimeoutMs = 500; // Timeout for thread joins

    // Mic boost - amplify input audio before sending
    private const float MicGain = 2.5f; // 2.5x boost for quiet mics

    // Pre-allocated buffer to reduce GC pressure in audio callback
    private byte[]? _audioProcessBuffer;

    // Voice activity tracking for receive side
    // Ensures screen share yields when we're receiving audio too
    // Using ticks with Interlocked for thread-safe DateTime operations
    private long _lastAudioReceiveTimeTicks = DateTime.MinValue.Ticks;
    private const int AudioReceiveTimeoutMs = 200; // Consider voice inactive after 200ms of silence

    // Video capture
    private bool _isVideoEnabled;
    private VideoDeviceInfo? _selectedVideoDevice;
    private VideoCaptureSettings _videoCaptureSettings = VideoCaptureSettings.Default;
    private CancellationTokenSource? _videoCts;
    private Thread? _videoCaptureThread;
    private readonly ConcurrentQueue<byte[]> _videoSendQueue = new();

    public VoiceService()
    {
        _screenSharingManager = new ScreenSharingManager();
        _screenSharingManager.OnFrameReceived += frame =>
        {
            OnScreenFrameReceived?.Invoke(frame.SenderConnectionId, frame.Data, frame.Width, frame.Height);
        };
        _screenSharingManager.OnLocalFrameReady += (data, width, height) =>
        {
            OnLocalScreenFrameReady?.Invoke(data, width, height);
        };
        _screenSharingManager.OnStatsUpdated += stats =>
        {
            OnScreenShareStatsUpdated?.Invoke(stats);
        };

        // Initialize HTTP streaming service (extracts base URL from hub URL)
        var baseUrl = HubUrl.Replace("/hubs/voice", "");
        _httpStreamService = new HttpScreenStreamService(baseUrl);
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsInVoiceChannel { get; private set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking => _isSpeaking;
    public double CurrentAudioLevel => _currentAudioLevel;
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 2f);
    }
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
    public event Action<byte[], int, int>? OnLocalScreenFrameReady;
    public event Action<string, bool>? OnUserScreenShareChanged;
    public event Action<ScreenShareStats>? OnScreenShareStatsUpdated;
    public event Action<bool, string>? OnConnectionStateChanged;

    // Video events
    public event Action<byte[], int, int>? OnLocalVideoFrameReady;
    public event Action<string, byte[], int, int>? OnRemoteVideoFrameReceived;
    public event Action<string, bool>? OnUserVideoStateChanged;

    // Video properties
    public bool IsVideoEnabled => _isVideoEnabled;
    public VideoDeviceInfo? SelectedVideoDevice
    {
        get => _selectedVideoDevice;
        set => _selectedVideoDevice = value;
    }

    // User info for reconnection
    private string? _currentUserId;
    private string? _currentUsername;
    private string? _currentAvatarUrl;

    // Call events
    public event Action<VoiceCallDto>? OnIncomingCall;
    public event Action<VoiceCallDto>? OnCallStarted;
    public event Action<VoiceCallDto>? OnCallAnswered;
    public event Action<VoiceCallDto>? OnCallDeclined;
    public event Action<string, string>? OnCallEnded;
    public event Action<string>? OnCallFailed;
    public event Action<string, bool, double>? OnCallUserSpeaking;

    // Nudge events
    public event Action<NudgeDto>? OnNudgeReceived;
    public event Action<NudgeDto>? OnNudgeSent;
    public event Action<string>? OnNudgeError;

    // Group call events
    public event Action<GroupCallDto>? OnGroupCallStarted;
    public event Action<GroupCallInviteDto>? OnGroupCallInvite;
    public event Action<GroupCallDto>? OnGroupCallUpdated;
    public event Action<string, GroupCallParticipantDto>? OnGroupCallParticipantJoined;
    public event Action<string, string>? OnGroupCallParticipantLeft;
    public event Action<string, string>? OnGroupCallEnded;
    public event Action<string>? OnGroupCallError;
    public event Action<string, string, bool, double>? OnGroupCallUserSpeaking;
    public event Action<string, string>? OnGroupCallInviteDeclined;

    // Voice room events
    public event Action<List<VoiceRoomDto>, int, int, int>? OnPublicVoiceRoomsReceived;
    public event Action<VoiceRoomDto>? OnVoiceRoomCreated;
    public event Action<VoiceRoomDto>? OnVoiceRoomJoined;
    public event Action<VoiceRoomDto>? OnVoiceRoomUpdated;
    public event Action<VoiceRoomDto>? OnVoiceRoomAdded;
    public event Action<string>? OnVoiceRoomRemoved;
    public event Action<string>? OnVoiceRoomError;
    public event Action<VoiceRoomParticipantDto>? OnVoiceRoomParticipantJoined;
    public event Action<VoiceRoomParticipantDto>? OnVoiceRoomParticipantLeft;
    public event Action<VoiceRoomParticipantDto>? OnVoiceRoomHostChanged;
    public event Action<string, string>? OnVoiceRoomClosed;
    public event Action<string, string>? OnVoiceRoomKicked;
    public event Action<VoiceRoomParticipantDto>? OnVoiceRoomModeratorAdded;
    public event Action<string, string>? OnKickedFromChannel;
    public event Action<string, string, string>? OnUserKickedFromRoom;
    public event Action<string>? OnKickSuccess;

    // WebRTC signaling events (legacy)
    public event Action<string, string>? OnReceiveOffer;
    public event Action<string, string>? OnReceiveAnswer;
    public event Action<string, string>? OnReceiveIceCandidate;

    // WebRTC P2P signaling events
    public event Action<string, string, string>? OnP2PConnectionRequested;
    public event Action<string, string, string, string>? OnWebRTCOfferReceived;
    public event Action<string, string, string, string>? OnWebRTCAnswerReceived;
    public event Action<string, string, string, int>? OnICECandidateReceived;
    public event Action<string>? OnP2PConnected;
    public event Action<string, string>? OnP2PFailed;

    // Error events
    public event Action<string>? OnVoiceError;
    public event Action<string>? OnCallError;

    // Screen sharing quality events
    public event Action<string, string>? OnQualityChangeRequested;
    public event Action<string, string>? OnJoinedScreenShare;
    public event Action<List<ActiveScreenShareDto>>? OnActiveScreenShares;
    public event Action<string, string>? OnQualityRequested;

    // Call state
    public bool IsInCall { get; private set; }
    public string? CurrentCallId { get; private set; }
    private VoiceCallDto? _currentCall;

    // Group call state
    public bool IsInGroupCall { get; private set; }
    public string? CurrentGroupCallId { get; private set; }
    private GroupCallDto? _currentGroupCall;

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

    public List<VideoDeviceInfo> GetVideoDevices()
    {
        var devices = new List<VideoDeviceInfo>();
        try
        {
            // Use DirectShow to enumerate video devices
            // This is a simplified implementation - in production you'd use DirectShow COM interop
            // For now, we'll detect common webcam devices
            var filterGraph = new DirectShowVideoDeviceEnumerator();
            var videoDevices = filterGraph.EnumerateDevices();

            foreach (var device in videoDevices)
            {
                devices.Add(device);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate video devices: {ex.Message}");
        }

        // Add a placeholder if no devices found
        if (devices.Count == 0)
        {
            devices.Add(new VideoDeviceInfo
            {
                DeviceIndex = 0,
                DeviceId = "default",
                Name = "Default Camera",
                DefaultWidth = 640,
                DefaultHeight = 480
            });
        }

        return devices;
    }

    public async Task ConnectAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                // Match server's JSON serialization for proper enum handling
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // Handle connection state changes
        _connection.Reconnecting += error =>
        {
            Debug.WriteLine($"Voice connection reconnecting: {error?.Message}");
            // Notify UI that connection is temporarily lost
            OnConnectionStateChanged?.Invoke(false, "Reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async connectionId =>
        {
            try
            {
                Debug.WriteLine($"Voice connection reconnected: {connectionId}");
                OnConnectionStateChanged?.Invoke(true, "Reconnected");

                // Re-join voice channel if we were in one
                if (IsInVoiceChannel && !string.IsNullOrEmpty(CurrentChannelId) && !string.IsNullOrEmpty(_currentUserId))
                {
                    await _connection.InvokeAsync("JoinVoiceChannel", CurrentChannelId, _currentUserId, _currentUsername, _currentAvatarUrl);
                    Debug.WriteLine($"Re-joined voice channel {CurrentChannelId} after reconnect");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VoiceService: Error during reconnection: {ex.Message}");
            }
        };

        _connection.Closed += error =>
        {
            Debug.WriteLine($"Voice connection closed: {error?.Message}");
            OnConnectionStateChanged?.Invoke(false, error?.Message ?? "Disconnected");

            // Clean up local state
            IsInVoiceChannel = false;
            _voiceUsers.Clear();
            return Task.CompletedTask;
        };

        RegisterHandlers();
        await _connection.StartAsync();
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Connection handshake from server
        _connection.On<JsonElement>("ConnectionHandshake", handshake =>
        {
            if (handshake.TryGetProperty("ConnectionId", out var connId))
            {
                Debug.WriteLine($"VoiceService: Handshake received. ConnectionId: {connId.GetString()}");
            }
        });

        // Heartbeat response
        _connection.On<JsonElement>("Pong", pong =>
        {
            Debug.WriteLine("VoiceService: Pong received");
        });

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

            // Update screen share voice priority using unified method
            UpdateVoiceActivity();
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
        // Uses pooled buffers to reduce GC pressure during high-frequency audio processing
        _connection.On<string, byte[]>("ReceiveAudio", (senderConnectionId, opusData) =>
        {
            // Don't play audio if we're deafened or if user is muted locally
            if (IsDeafened || IsUserMuted(senderConnectionId)) return;
            if (opusData.Length == 0 || _bufferedWaveProvider == null) return;

            // VOICE PRIORITY: Signal to orchestrator that we're receiving audio
            StreamingOrchestrator.Instance.SignalAudioReceive();
            Interlocked.Exchange(ref _lastAudioReceiveTimeTicks, DateTime.UtcNow.Ticks);
            UpdateVoiceActivityFromReceive();

            try
            {
                // Get or create per-user decoder (each user needs separate decoder state)
#pragma warning disable CS0618 // Type or member is obsolete
                var decoder = _userOpusDecoders.GetOrAdd(senderConnectionId,
                    _ => new OpusDecoder(SampleRate, Channels));

                // Decode Opus to PCM - use pooled buffer to reduce GC pressure
                var pcmBuffer = StreamingOrchestrator.Instance.RentSmallBuffer();
                var pcmShortBuffer = new short[OpusFrameSize]; // Reuse for each decode
                var decodedSamples = decoder.Decode(opusData, 0, opusData.Length, pcmShortBuffer, 0, OpusFrameSize, false);
#pragma warning restore CS0618

                if (decodedSamples > 0)
                {
                    // Apply combined volume (per-user * master) with SIMD-friendly loop
                    var userVolume = GetUserVolume(senderConnectionId);
                    var combinedVolume = userVolume * _masterVolume;
                    var applyVolume = Math.Abs(combinedVolume - 1.0f) > 0.01f;

                    // Convert short[] to byte[] inline (avoids extra array allocation)
                    var byteCount = decodedSamples * 2;
                    for (int i = 0; i < decodedSamples; i++)
                    {
                        var sample = applyVolume
                            ? (short)Math.Clamp(pcmShortBuffer[i] * combinedVolume, short.MinValue, short.MaxValue)
                            : pcmShortBuffer[i];
                        pcmBuffer[i * 2] = (byte)sample;
                        pcmBuffer[i * 2 + 1] = (byte)(sample >> 8);
                    }

                    // Add directly to playback buffer - NAudio handles timing
                    try
                    {
                        _bufferedWaveProvider.AddSamples(pcmBuffer, 0, byteCount);
                    }
                    catch
                    {
                        // Buffer overflow - DiscardOnBufferOverflow handles this
                    }
                }

                // Return pooled buffer
                StreamingOrchestrator.Instance.ReturnSmallBuffer(pcmBuffer);
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
        // Note: OnScreenFrameReceived is already fired through _screenSharingManager.OnFrameReceived subscription
        _connection.On<string, byte[], int, int>("ReceiveScreenFrame", (senderConnectionId, frameData, width, height) =>
        {
            _screenSharingManager.HandleFrameReceived(senderConnectionId, frameData, width, height);
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

        // Video streaming handlers
        _connection.On<string, string, string>("UserVideoStarted", (connectionId, userId, username) =>
        {
            Debug.WriteLine($"User {username} started video");
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                user.IsVideoEnabled = true;
            }
            OnUserVideoStateChanged?.Invoke(userId, true);
        });

        _connection.On<string, string>("UserVideoStopped", (connectionId, userId) =>
        {
            Debug.WriteLine($"User {userId} stopped video");
            if (_voiceUsers.TryGetValue(connectionId, out var user))
            {
                user.IsVideoEnabled = false;
            }
            OnUserVideoStateChanged?.Invoke(userId, false);
        });

        _connection.On<string, byte[], int, int>("ReceiveVideoFrame", (connectionId, frameData, width, height) =>
        {
            OnRemoteVideoFrameReceived?.Invoke(connectionId, frameData, width, height);
        });

        _connection.On<int>("ViewerCountUpdated", count =>
        {
            _screenSharingManager.HandleViewerCountUpdate(count);
        });

        // HTTP streaming: lightweight notification that a new frame is available
        // Viewers fetch the frame via HTTP instead of receiving it through SignalR
        _connection.On<string, long, int, int>("ScreenFrameAvailable", async (streamId, frameNumber, width, height) =>
        {
            // Only fetch if we don't have this frame yet
            if (_lastFrameNumbers.TryGetValue(streamId, out var lastFrame) && frameNumber <= lastFrame)
                return;

            _lastFrameNumbers[streamId] = frameNumber;

            // Fetch frame via HTTP
            if (_httpStreamService != null)
            {
                var (data, w, h, fn) = await _httpStreamService.GetFrameAsync(streamId, lastFrame);
                if (data != null && data.Length > 0)
                {
                    // Fire the frame received event
                    OnScreenFrameReceived?.Invoke(streamId, data, w, h);
                }
            }
        });

        // Register call handlers
        RegisterCallHandlers();

        // Register WebRTC signaling handlers for P2P connections
        RegisterWebRTCHandlers();
    }

    /// <summary>
    /// Register handlers for WebRTC signaling messages.
    /// These enable P2P connections for lower latency audio/video.
    /// </summary>
    private void RegisterWebRTCHandlers()
    {
        if (_connection == null) return;

        // P2P connection request
        _connection.On<string, string, string>("P2PConnectionRequested", (fromConnectionId, fromUserId, fromUsername) =>
        {
            Debug.WriteLine($"VoiceService: P2P connection requested from {fromUsername} ({fromConnectionId})");
            OnP2PConnectionRequested?.Invoke(fromConnectionId, fromUserId, fromUsername);
        });

        // WebRTC SDP offer received
        _connection.On<string, string, string, string>("WebRTCOfferReceived", (fromConnectionId, fromUserId, fromUsername, sdpOffer) =>
        {
            Debug.WriteLine($"VoiceService: WebRTC offer received from {fromUsername}");
            OnWebRTCOfferReceived?.Invoke(fromConnectionId, fromUserId, fromUsername, sdpOffer);
        });

        // WebRTC SDP answer received
        _connection.On<string, string, string, string>("WebRTCAnswerReceived", (fromConnectionId, fromUserId, fromUsername, sdpAnswer) =>
        {
            Debug.WriteLine($"VoiceService: WebRTC answer received from {fromUsername}");
            OnWebRTCAnswerReceived?.Invoke(fromConnectionId, fromUserId, fromUsername, sdpAnswer);
        });

        // ICE candidate received
        _connection.On<string, string, string, int>("ICECandidateReceived", (fromConnectionId, candidate, sdpMid, sdpMLineIndex) =>
        {
            Debug.WriteLine($"VoiceService: ICE candidate received from {fromConnectionId}");
            OnICECandidateReceived?.Invoke(fromConnectionId, candidate, sdpMid, sdpMLineIndex);
        });

        // P2P connection established
        _connection.On<string>("P2PConnected", connectionId =>
        {
            Debug.WriteLine($"VoiceService: P2P connection established with {connectionId}");
            OnP2PConnected?.Invoke(connectionId);
        });

        // P2P connection failed - fall back to server relay
        _connection.On<string, string>("P2PFailed", (connectionId, reason) =>
        {
            Debug.WriteLine($"VoiceService: P2P connection failed with {connectionId}: {reason}");
            OnP2PFailed?.Invoke(connectionId, reason);
        });

        // WebRTC error
        _connection.On<string>("WebRTCError", error =>
        {
            Debug.WriteLine($"VoiceService: WebRTC error: {error}");
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

        // Store user info for reconnection
        _currentUserId = userId;
        _currentUsername = username;
        _currentAvatarUrl = avatarUrl;

        await _connection.InvokeAsync("JoinVoiceChannel", channelId, userId, username, avatarUrl);
        IsInVoiceChannel = true;
        CurrentChannelId = channelId;

        StartAudioCapture();
    }

    public async Task LeaveVoiceChannelAsync()
    {
        // Stop screen share FIRST before leaving channel to ensure proper cleanup
        if (_screenSharingManager.IsSharing)
        {
            try
            {
                await StopScreenShareAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        StopAudioCapture();

        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.InvokeAsync("LeaveVoiceChannel").ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during cleanup
            }
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
#pragma warning disable CS0618 // Type or member is obsolete
            _opusEncoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = OpusBitrate,
                Complexity = 5, // Balance between quality and CPU (0-10)
                UseVBR = true,
                SignalType = OpusSignal.OPUS_SIGNAL_VOICE
            };
#pragma warning restore CS0618

            // Configure audio capture with small buffer for low latency
            _waveIn = new WaveInEvent
            {
                DeviceNumber = _inputDeviceNumber,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = AudioBufferMs, // Low latency buffers
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
                DesiredLatency = PlaybackLatencyMs, // Low latency playback
                NumberOfBuffers = 3
            };
            _waveOut.Init(_bufferedWaveProvider);
            _waveOut.Play();

            // Start the dedicated audio send thread (decouples network from audio callback)
            // Uses HIGHEST priority - audio is more time-sensitive than video
            _audioSendCts = new CancellationTokenSource();
            _audioSendThread = new Thread(() => AudioSendLoopThreaded(_audioSendCts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "AudioSendThread"
            };
            _audioSendThread.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio device error: {ex.Message}");

            // Clean up any partially initialized resources
            try { _waveIn?.StopRecording(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: WaveIn stop failed: {cleanupEx.Message}"); }
            try { _waveIn?.Dispose(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: WaveIn dispose failed: {cleanupEx.Message}"); }
            _waveIn = null;

            try { _waveOut?.Stop(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: WaveOut stop failed: {cleanupEx.Message}"); }
            try { _waveOut?.Dispose(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: WaveOut dispose failed: {cleanupEx.Message}"); }
            _waveOut = null;

            _bufferedWaveProvider = null;
            _opusEncoder = null;

            try { _audioSendCts?.Cancel(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: AudioSendCts cancel failed: {cleanupEx.Message}"); }
            try { _audioSendCts?.Dispose(); } catch (Exception cleanupEx) { Debug.WriteLine($"Cleanup: AudioSendCts dispose failed: {cleanupEx.Message}"); }
            _audioSendCts = null;
        }
    }

    /// <summary>
    /// Dedicated thread loop that sends queued audio data to the server.
    /// This runs on a high-priority thread to ensure audio gets sent before screen share frames.
    /// Encodes audio with Opus codec before sending.
    /// PRIORITY: Audio is sent immediately without delays to prevent choppiness during screen share.
    /// </summary>
    private void AudioSendLoopThreaded(CancellationToken cancellationToken)
    {
        // Buffer for Opus encoded output (max Opus frame is ~4000 bytes)
        var opusBuffer = new byte[4000];
        var spinWait = new SpinWait();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_audioSendQueue.TryDequeue(out var pcmData))
                {
                    // Capture references locally to avoid race conditions
                    var encoder = _opusEncoder;
                    if (_connection != null && IsConnected && encoder != null && pcmData.Length >= 2)
                    {
                        // Convert byte[] PCM to short[] for Opus encoder
                        var pcmSamples = new short[pcmData.Length / 2];
                        Buffer.BlockCopy(pcmData, 0, pcmSamples, 0, pcmData.Length);

                        // Encode with Opus - dramatically reduces bandwidth
#pragma warning disable CS0618 // Type or member is obsolete
                        var encodedLength = encoder.Encode(pcmSamples, 0, OpusFrameSize, opusBuffer, 0, opusBuffer.Length);
#pragma warning restore CS0618

                        if (encodedLength > 0)
                        {
                            // Create correctly sized array for sending
                            var opusData = new byte[encodedLength];
                            Buffer.BlockCopy(opusBuffer, 0, opusData, 0, encodedLength);

                            // Fire-and-forget send - NEVER block audio thread!
                            // Blocking causes mic lag/choppiness
                            var conn = _connection;
                            if (conn != null)
                            {
                                // Queue the send without waiting - audio packets are tiny (~50-100 bytes)
                                // and SignalR will handle queuing internally
                                _ = conn.SendAsync("SendAudio", opusData, cancellationToken)
                                    .ContinueWith(t =>
                                    {
                                        if (t.IsCompletedSuccessfully)
                                        {
                                            StreamingOrchestrator.Instance.SignalAudioSend();
                                        }
                                    }, TaskContinuationOptions.ExecuteSynchronously);
                            }
                        }
                    }
                }
                else
                {
                    // No data to send - short spin then yield
                    // SpinWait is more efficient than Thread.Sleep for short waits
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield)
                    {
                        Thread.Sleep(1);
                        spinWait.Reset();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore send errors - network issues shouldn't crash audio
            }
        }
    }

    private void StopAudioCapture()
    {
        // Stop background send thread
        _audioSendCts?.Cancel();

        try
        {
            if (_audioSendThread != null)
            {
                _audioSendThread.Join(ThreadJoinTimeoutMs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Audio thread join failed: {ex.Message}");
        }

        _audioSendCts?.Dispose();
        _audioSendCts = null;
        _audioSendThread = null;

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

        // Calculate audio level (optimized RMS - subsample for speed)
        // Check every 8th sample instead of every sample - still accurate enough for VAD
        var sum = 0L;
        var sampleCount = 0;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 16) // Every 8th sample (16 bytes = 8 samples * 2 bytes)
        {
            var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            sum += sample * sample;
            sampleCount++;
        }
        var rms = sampleCount > 0 ? Math.Sqrt((double)sum / sampleCount) / 32768.0 : 0;
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

            // Update voice activity (considers both sending and receiving)
            UpdateVoiceActivity();
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to send speaking state: {ex.Message}");
                }
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to send periodic speaking state: {ex.Message}");
                }
            });
        }

        // Queue audio for sending if speaking and not muted
        // The background AudioSendLoop will handle the actual network transmission
        if (_isSpeaking && !IsMuted && e.BytesRecorded > 0)
        {
            // Ensure buffer is allocated (reuse to reduce GC pressure)
            if (_audioProcessBuffer == null || _audioProcessBuffer.Length < e.BytesRecorded)
            {
                _audioProcessBuffer = new byte[e.BytesRecorded];
            }

            // Copy and apply mic gain boost - optimized with unsafe for speed
            var audioData = new byte[e.BytesRecorded];
            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                // Apply gain with clipping protection
                var boosted = (int)(sample * MicGain);
                if (boosted > short.MaxValue) boosted = short.MaxValue;
                else if (boosted < short.MinValue) boosted = short.MinValue;
                audioData[i] = (byte)boosted;
                audioData[i + 1] = (byte)(boosted >> 8);
            }

            // Add to send queue - background thread will send it
            // Limit queue size to prevent memory buildup if network is slow
            if (_audioSendQueue.Count < MaxAudioQueueSize) // ~1 second of audio max
            {
                _audioSendQueue.Enqueue(audioData);
            }
        }
    }

    /// <summary>
    /// Updates voice activity state considering both sending and receiving audio.
    /// Called when local speaking state changes.
    /// </summary>
    private void UpdateVoiceActivity()
    {
        // Voice is active if we're speaking OR we've received audio recently
        // Use Interlocked.Read for thread-safe access to ticks
        var lastReceiveTicks = Interlocked.Read(ref _lastAudioReceiveTimeTicks);
        var isReceivingAudio = (DateTime.UtcNow.Ticks - lastReceiveTicks) / TimeSpan.TicksPerMillisecond < AudioReceiveTimeoutMs;
        var anyoneSpeaking = _voiceUsers.Values.Any(u => u.IsSpeaking) || _isSpeaking || isReceivingAudio;
        _screenSharingManager.SetVoiceActive(anyoneSpeaking);
    }

    /// <summary>
    /// Updates voice activity when receiving audio from others.
    /// Called from ReceiveAudio handler to ensure screen share yields for incoming audio.
    /// </summary>
    private void UpdateVoiceActivityFromReceive()
    {
        // Signal voice activity when receiving audio
        // This ensures screen share yields even if UserSpeaking event hasn't arrived yet
        _screenSharingManager.SetVoiceActive(true);
    }

    public async Task DisconnectAsync()
    {
        // LeaveVoiceChannelAsync now stops screen share first
        await LeaveVoiceChannelAsync().ConfigureAwait(false);

        // Disconnect the screen sharing manager
        _screenSharingManager.Disconnect();

        // Cleanup HTTP streaming service
        _httpStreamService?.Dispose();
        _lastFrameNumbers.Clear();

        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during disconnect
            }

            try
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during dispose
            }

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

    public async Task InviteToChannelAsync(string userId, string channelId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("InviteToChannel", userId, channelId);
        }
        catch
        {
            // Ignore errors - server may not support this
        }
    }

    public async Task MuteUserAsync(string userId, string channelId)
    {
        if (_connection == null || !IsConnected) return;
        try
        {
            await _connection.InvokeAsync("MuteUser", userId, channelId);
        }
        catch
        {
            // Ignore errors - server may not support this
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

        // Use HTTP streaming if available (reduces voice lag)
        string? httpStreamId = null;
        if (_useHttpStreaming && _httpStreamService != null && CurrentChannelId != null)
        {
            httpStreamId = await _httpStreamService.StartStreamAsync(CurrentChannelId, _currentUsername ?? "Unknown");
        }

        // Connect the manager with appropriate frame sender
        await _screenSharingManager.ConnectAsync(
            async (frameData, width, height) =>
            {
                if (_useHttpStreaming && httpStreamId != null && _httpStreamService != null)
                {
                    // Send via HTTP (doesn't block voice SignalR)
                    await _httpStreamService.UploadFrameAsync(frameData, width, height).ConfigureAwait(false);
                }
                else if (_connection != null && IsConnected)
                {
                    // Fallback to SignalR
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
                // Stop HTTP stream
                if (_httpStreamService != null)
                {
                    await _httpStreamService.StopStreamAsync().ConfigureAwait(false);
                }
            });

        // Start sharing
        await _screenSharingManager.StartSharingAsync(display, settings);
    }

    public async Task StopScreenShareAsync()
    {
        // Notify server FIRST while connection is still active
        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.SendAsync("StopScreenShare").ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors - connection might already be closing
            }
        }

        // Then stop local capture/streaming
        await _screenSharingManager.StopSharingAsync().ConfigureAwait(false);
    }

    // === Video Capture Methods ===

    public async Task StartVideoAsync()
    {
        // Use first available device if none selected
        if (_selectedVideoDevice == null)
        {
            var devices = GetVideoDevices();
            _selectedVideoDevice = devices.FirstOrDefault();
        }

        if (_selectedVideoDevice != null)
        {
            await StartVideoAsync(_selectedVideoDevice);
        }
    }

    public Task StartVideoAsync(VideoDeviceInfo device)
    {
        return StartVideoAsync(device, VideoCaptureSettings.Default);
    }

    public async Task StartVideoAsync(VideoDeviceInfo device, VideoCaptureSettings settings)
    {
        if (_isVideoEnabled || _connection == null || !IsConnected) return;

        _selectedVideoDevice = device;
        _videoCaptureSettings = settings;
        _isVideoEnabled = true;

        // Notify server that we're starting video
        try
        {
            await _connection.SendAsync("StartVideo").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to notify server of video start: {ex.Message}");
        }

        // Start video capture thread
        _videoCts = new CancellationTokenSource();
        _videoCaptureThread = new Thread(() => VideoCaptureLoop(_videoCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal,
            Name = "VideoCaptureThread"
        };
        _videoCaptureThread.Start();

        OnUserVideoStateChanged?.Invoke(_currentUserId ?? "", true);
    }

    public async Task StopVideoAsync()
    {
        if (!_isVideoEnabled) return;

        _isVideoEnabled = false;

        // Stop capture thread
        _videoCts?.Cancel();
        try
        {
            _videoCaptureThread?.Join(ThreadJoinTimeoutMs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Video thread join failed: {ex.Message}");
        }
        _videoCts?.Dispose();
        _videoCts = null;
        _videoCaptureThread = null;

        // Clear queue
        while (_videoSendQueue.TryDequeue(out _)) { }

        // Notify server
        if (_connection != null && IsConnected)
        {
            try
            {
                await _connection.SendAsync("StopVideo").ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors
            }
        }

        OnUserVideoStateChanged?.Invoke(_currentUserId ?? "", false);
    }

    private void VideoCaptureLoop(CancellationToken cancellationToken)
    {
        // Video capture implementation using GDI+ for webcam
        // In a production app, this would use DirectShow, MediaFoundation, or OpenCV
        var frameRate = Math.Max(1, _videoCaptureSettings.FrameRate); // Ensure at least 1 FPS to prevent division by zero
        var frameInterval = 1000 / frameRate;
        var lastFrameTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested && _isVideoEnabled)
        {
            try
            {
                var elapsed = (DateTime.UtcNow - lastFrameTime).TotalMilliseconds;
                if (elapsed < frameInterval)
                {
                    Thread.Sleep(Math.Max(1, (int)(frameInterval - elapsed)));
                    continue;
                }

                lastFrameTime = DateTime.UtcNow;

                // Capture frame (placeholder - real implementation would use DirectShow/MediaFoundation)
                // For now, create a test pattern or blank frame to demonstrate the pipeline
                var width = _videoCaptureSettings.Width;
                var height = _videoCaptureSettings.Height;

                using var bitmap = new System.Drawing.Bitmap(width, height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);

                // Draw a gradient test pattern to show video is working
                var tick = Environment.TickCount;
                var hue = (tick / 50) % 360;
                graphics.Clear(ColorFromHSV(hue, 0.5, 0.8));

                // Add text to show it's a test pattern
                using var font = new System.Drawing.Font("Arial", 16);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                graphics.DrawString("Video Preview", font, brush, 10, 10);
                graphics.DrawString($"Camera: {_selectedVideoDevice?.Name}", font, brush, 10, 40);
                graphics.DrawString($"{width}x{height} @ {_videoCaptureSettings.FrameRate}fps", font, brush, 10, 70);

                // Encode as JPEG
                using var ms = new MemoryStream();
                var encoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(e => e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, (long)_videoCaptureSettings.Quality);

                if (encoder != null)
                {
                    bitmap.Save(ms, encoder, encoderParams);
                }
                else
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                var frameData = ms.ToArray();

                // Fire local preview event
                OnLocalVideoFrameReady?.Invoke(frameData, width, height);

                // Send to server if connected (fire and forget)
                var conn = _connection;
                if (conn != null && IsConnected)
                {
                    _ = conn.SendAsync("SendVideoFrame", frameData, width, height, cancellationToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                Debug.WriteLine($"Video frame send failed: {t.Exception?.InnerException?.Message}");
                            }
                        }, TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Video capture error: {ex.Message}");
                Thread.Sleep(100); // Back off on error
            }
        }
    }

    private static System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
    {
        var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        var f = hue / 60 - Math.Floor(hue / 60);
        value *= 255;
        var v = Convert.ToInt32(value);
        var p = Convert.ToInt32(value * (1 - saturation));
        var q = Convert.ToInt32(value * (1 - f * saturation));
        var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => System.Drawing.Color.FromArgb(255, v, t, p),
            1 => System.Drawing.Color.FromArgb(255, q, v, p),
            2 => System.Drawing.Color.FromArgb(255, p, v, t),
            3 => System.Drawing.Color.FromArgb(255, p, q, v),
            4 => System.Drawing.Color.FromArgb(255, t, p, v),
            _ => System.Drawing.Color.FromArgb(255, v, p, q)
        };
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

        _connection.On<string, bool, double>("CallUserSpeaking", (userId, isSpeaking, audioLevel) =>
        {
            OnCallUserSpeaking?.Invoke(userId, isSpeaking, audioLevel);
        });

        // Nudge handlers
        _connection.On<NudgeDto>("NudgeReceived", nudge =>
        {
            OnNudgeReceived?.Invoke(nudge);
        });

        _connection.On<NudgeDto>("NudgeSent", nudge =>
        {
            OnNudgeSent?.Invoke(nudge);
        });

        _connection.On<string>("NudgeError", error =>
        {
            OnNudgeError?.Invoke(error);
        });

        // Group call handlers
        RegisterGroupCallHandlers();
    }

    private void RegisterGroupCallHandlers()
    {
        if (_connection == null) return;

        _connection.On<GroupCallDto>("GroupCallStarted", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _currentGroupCall = call;
                CurrentGroupCallId = call.Id;
                IsInGroupCall = true;
                StartAudioCapture();
                OnGroupCallStarted?.Invoke(call);
            });
        });

        _connection.On<GroupCallInviteDto>("GroupCallInvite", invite =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnGroupCallInvite?.Invoke(invite);
            });
        });

        _connection.On<GroupCallDto>("GroupCallUpdated", call =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _currentGroupCall = call;
                OnGroupCallUpdated?.Invoke(call);
            });
        });

        _connection.On<string, GroupCallParticipantDto>("GroupCallParticipantJoined", (callId, participant) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnGroupCallParticipantJoined?.Invoke(callId, participant);
            });
        });

        _connection.On<string, string>("GroupCallParticipantLeft", (callId, userId) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnGroupCallParticipantLeft?.Invoke(callId, userId);
            });
        });

        _connection.On<string, string>("GroupCallEnded", (callId, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (CurrentGroupCallId == callId)
                {
                    IsInGroupCall = false;
                    CurrentGroupCallId = null;
                    _currentGroupCall = null;
                    StopAudioCapture();
                }
                OnGroupCallEnded?.Invoke(callId, reason);
            });
        });

        _connection.On<string>("GroupCallError", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnGroupCallError?.Invoke(error);
            });
        });

        // Group call audio receiving
        _connection.On<string, byte[]>("GroupCallAudioReceived", (userId, audioData) =>
        {
            if (IsInGroupCall && !IsDeafened && _bufferedWaveProvider != null)
            {
                DecodeAndPlayAudio(userId, audioData);
            }
        });

        // Group call user speaking indicator
        _connection.On<string, string, bool, double>("GroupCallUserSpeaking", (callId, userId, isSpeaking, audioLevel) =>
        {
            OnGroupCallUserSpeaking?.Invoke(callId, userId, isSpeaking, audioLevel);
        });

        // Group call invite declined notification (for host)
        _connection.On<string, string>("GroupCallInviteDeclined", (callId, userId) =>
        {
            OnGroupCallInviteDeclined?.Invoke(callId, userId);
        });

        // Voice room handlers
        _connection.On<List<VoiceRoomDto>, int, int, int>("PublicVoiceRooms", (rooms, totalCount, page, pageSize) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPublicVoiceRoomsReceived?.Invoke(rooms, totalCount, page, pageSize);
            });
        });

        _connection.On<VoiceRoomDto>("VoiceRoomCreated", room =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomCreated?.Invoke(room);
            });
        });

        _connection.On<VoiceRoomDto>("VoiceRoomJoined", room =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomJoined?.Invoke(room);
            });
        });

        _connection.On<VoiceRoomDto>("VoiceRoomUpdated", room =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomUpdated?.Invoke(room);
            });
        });

        _connection.On<VoiceRoomDto>("VoiceRoomAdded", room =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomAdded?.Invoke(room);
            });
        });

        _connection.On<string>("VoiceRoomRemoved", roomId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomRemoved?.Invoke(roomId);
            });
        });

        _connection.On<string>("VoiceRoomError", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomError?.Invoke(error);
            });
        });

        _connection.On<VoiceRoomParticipantDto>("VoiceRoomParticipantJoined", participant =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomParticipantJoined?.Invoke(participant);
            });
        });

        _connection.On<VoiceRoomParticipantDto>("VoiceRoomParticipantLeft", participant =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomParticipantLeft?.Invoke(participant);
            });
        });

        _connection.On<VoiceRoomParticipantDto>("VoiceRoomHostChanged", participant =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomHostChanged?.Invoke(participant);
            });
        });

        _connection.On<string, string>("VoiceRoomClosed", (roomId, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomClosed?.Invoke(roomId, reason);
            });
        });

        _connection.On<string, string>("VoiceRoomKicked", (roomId, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomKicked?.Invoke(roomId, reason);
            });
        });

        _connection.On<VoiceRoomParticipantDto>("VoiceRoomModeratorAdded", participant =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomModeratorAdded?.Invoke(participant);
            });
        });

        // Kick-related handlers
        _connection.On<JsonElement>("KickedFromRoom", data =>
        {
            var roomId = data.TryGetProperty("RoomId", out var rid) ? rid.GetString() ?? "" : "";
            var reason = data.TryGetProperty("Reason", out var r) ? r.GetString() ?? "Kicked" : "Kicked";
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceRoomKicked?.Invoke(roomId, reason);
            });
            _ = LeaveVoiceChannelAsync();
        });

        _connection.On<JsonElement>("UserKicked", data =>
        {
            var roomId = data.TryGetProperty("RoomId", out var rid) ? rid.GetString() ?? "" : "";
            var userId = data.TryGetProperty("UserId", out var uid) ? uid.GetString() ?? "" : "";
            var reason = data.TryGetProperty("Reason", out var r) ? r.GetString() ?? "" : "";
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnUserKickedFromRoom?.Invoke(roomId, userId, reason);
            });
        });

        _connection.On<string>("KickSuccess", userId =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnKickSuccess?.Invoke(userId);
            });
        });

        _connection.On<JsonElement>("KickedFromChannel", data =>
        {
            var channelId = data.TryGetProperty("ChannelId", out var cid) ? cid.GetString() ?? "" : "";
            var reason = data.TryGetProperty("Reason", out var r) ? r.GetString() ?? "Kicked" : "Kicked";
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnKickedFromChannel?.Invoke(channelId, reason);
            });
            _ = LeaveVoiceChannelAsync();
        });

        // WebRTC signaling handlers
        _connection.On<string, string>("ReceiveOffer", (senderConnectionId, offer) =>
        {
            OnReceiveOffer?.Invoke(senderConnectionId, offer);
        });

        _connection.On<string, string>("ReceiveAnswer", (senderConnectionId, answer) =>
        {
            OnReceiveAnswer?.Invoke(senderConnectionId, answer);
        });

        _connection.On<string, string>("ReceiveIceCandidate", (senderConnectionId, candidate) =>
        {
            OnReceiveIceCandidate?.Invoke(senderConnectionId, candidate);
        });

        // Error handlers
        _connection.On<string>("VoiceError", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnVoiceError?.Invoke(error);
            });
        });

        _connection.On<string>("CallError", error =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnCallError?.Invoke(error);
            });
        });

        // Screen sharing quality handlers
        _connection.On<string, string>("QualityChangeRequested", (viewerConnectionId, qualityPreset) =>
        {
            OnQualityChangeRequested?.Invoke(viewerConnectionId, qualityPreset);
        });

        _connection.On<string, string>("JoinedScreenShare", (sharerConnectionId, sharerUsername) =>
        {
            OnJoinedScreenShare?.Invoke(sharerConnectionId, sharerUsername);
        });

        _connection.On<List<ActiveScreenShareDto>>("ActiveScreenShares", activeShares =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnActiveScreenShares?.Invoke(activeShares);
            });
        });

        _connection.On<string, string>("QualityRequested", (viewerConnectionId, quality) =>
        {
            OnQualityRequested?.Invoke(viewerConnectionId, quality);
        });
    }

    public async Task SendNudgeAsync(string targetUserId, string? message = null)
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        try
        {
            await _connection.InvokeAsync("SendNudge", targetUserId, message);
        }
        catch (Exception ex)
        {
            OnNudgeError?.Invoke($"Failed to send nudge: {ex.Message}");
        }
    }

    // === Group Call Methods ===

    public async Task StartGroupCallAsync(string name, List<string> invitedUserIds)
    {
        if (_connection == null || !IsConnected) return;
        if (IsInGroupCall || IsInCall)
        {
            OnGroupCallError?.Invoke("Already in a call");
            return;
        }

        try
        {
            await _connection.InvokeAsync("StartGroupCall", name, invitedUserIds);
        }
        catch (Exception ex)
        {
            OnGroupCallError?.Invoke($"Failed to start group call: {ex.Message}");
        }
    }

    public async Task JoinGroupCallAsync(string callId)
    {
        if (_connection == null || !IsConnected) return;
        if (IsInGroupCall || IsInCall)
        {
            OnGroupCallError?.Invoke("Already in a call");
            return;
        }

        try
        {
            await _connection.InvokeAsync("JoinGroupCall", callId);
            IsInGroupCall = true;
            CurrentGroupCallId = callId;
            StartAudioCapture();
        }
        catch (Exception ex)
        {
            OnGroupCallError?.Invoke($"Failed to join group call: {ex.Message}");
        }
    }

    public async Task LeaveGroupCallAsync(string callId)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("LeaveGroupCall", callId);
            IsInGroupCall = false;
            CurrentGroupCallId = null;
            _currentGroupCall = null;
            StopAudioCapture();
        }
        catch
        {
            // Ignore errors on leave
        }
    }

    public async Task InviteToGroupCallAsync(string callId, string userId)
    {
        if (_connection == null || !IsConnected) return;
        if (!IsInGroupCall || CurrentGroupCallId != callId)
        {
            OnGroupCallError?.Invoke("Not in this group call");
            return;
        }

        try
        {
            await _connection.InvokeAsync("InviteToGroupCall", callId, userId);
        }
        catch (Exception ex)
        {
            OnGroupCallError?.Invoke($"Failed to invite user: {ex.Message}");
        }
    }

    public async Task DeclineGroupCallAsync(string callId)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("DeclineGroupCall", callId);
        }
        catch
        {
            // Ignore errors
        }
    }

    // Voice Room Methods
    public async Task GetPublicVoiceRoomsAsync(VoiceRoomCategory? category = null, string? query = null, int page = 1, int pageSize = 20)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("GetPublicVoiceRooms", category, query, page, pageSize);
        }
        catch (Exception ex)
        {
            OnVoiceRoomError?.Invoke($"Failed to get rooms: {ex.Message}");
        }
    }

    public async Task CreateVoiceRoomAsync(CreateVoiceRoomDto dto)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("CreateVoiceRoom", dto, _currentUserId, _currentUsername, _currentAvatarUrl);
        }
        catch (Exception ex)
        {
            OnVoiceRoomError?.Invoke($"Failed to create room: {ex.Message}");
        }
    }

    public async Task JoinVoiceRoomAsync(string roomId, string? password = null)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("JoinVoiceRoom", roomId, _currentUserId, _currentUsername, _currentAvatarUrl, password);
        }
        catch (Exception ex)
        {
            OnVoiceRoomError?.Invoke($"Failed to join room: {ex.Message}");
        }
    }

    public async Task LeaveVoiceRoomAsync(string roomId)
    {
        if (_connection == null || !IsConnected) return;

        try
        {
            await _connection.InvokeAsync("LeaveVoiceRoom", roomId);
        }
        catch (Exception ex)
        {
            OnVoiceRoomError?.Invoke($"Failed to leave room: {ex.Message}");
        }
    }

    private void DecodeAndPlayAudio(string senderConnectionId, byte[] opusData)
    {
        if (IsUserMuted(senderConnectionId) || _bufferedWaveProvider == null) return;

        // Signal audio receive to orchestrator for voice priority
        StreamingOrchestrator.Instance.SignalAudioReceive();

        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var decoder = _userOpusDecoders.GetOrAdd(senderConnectionId, _ => new OpusDecoder(SampleRate, Channels));
            var pcmBuffer = new short[OpusFrameSize];
            var decodedSamples = decoder.Decode(opusData, 0, opusData.Length, pcmBuffer, 0, OpusFrameSize, false);
#pragma warning restore CS0618

            if (decodedSamples > 0)
            {
                // Use pooled buffer to reduce GC pressure
                var pcmBytes = StreamingOrchestrator.Instance.RentSmallBuffer();
                var userVolume = GetUserVolume(senderConnectionId);
                var combinedVolume = userVolume * _masterVolume;
                var applyVolume = Math.Abs(combinedVolume - 1.0f) > 0.01f;
                var byteCount = decodedSamples * 2;

                // Convert short[] to byte[] with volume applied inline
                for (int i = 0; i < decodedSamples; i++)
                {
                    var sample = applyVolume
                        ? (short)Math.Clamp(pcmBuffer[i] * combinedVolume, short.MinValue, short.MaxValue)
                        : pcmBuffer[i];
                    pcmBytes[i * 2] = (byte)sample;
                    pcmBytes[i * 2 + 1] = (byte)(sample >> 8);
                }

                try
                {
                    _bufferedWaveProvider.AddSamples(pcmBytes, 0, byteCount);
                }
                catch
                {
                    // Buffer overflow handled by DiscardOnBufferOverflow
                }

                // Return pooled buffer
                StreamingOrchestrator.Instance.ReturnSmallBuffer(pcmBytes);
            }
        }
        catch
        {
            // Ignore decode errors
        }
    }
}
