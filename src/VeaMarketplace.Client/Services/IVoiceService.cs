using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace VeaMarketplace.Client.Services;

public interface IVoiceService
{
    bool IsConnected { get; }
    bool IsInVoiceChannel { get; }
    bool IsMuted { get; set; }
    bool IsDeafened { get; set; }

    event Action<VoiceUserState>? OnUserJoinedVoice;
    event Action<VoiceUserState>? OnUserLeftVoice;
    event Action<string, string, bool, double>? OnUserSpeaking;
    event Action<List<VoiceUserState>>? OnVoiceChannelUsers;

    Task ConnectAsync();
    Task DisconnectAsync();
    Task JoinVoiceChannelAsync(string channelId, string userId, string username, string avatarUrl);
    Task LeaveVoiceChannelAsync();
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

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public bool IsInVoiceChannel { get; private set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }

    public event Action<VoiceUserState>? OnUserJoinedVoice;
    public event Action<VoiceUserState>? OnUserLeftVoice;
    public event Action<string, string, bool, double>? OnUserSpeaking;
    public event Action<List<VoiceUserState>>? OnVoiceChannelUsers;

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
                _voiceUsers[user.ConnectionId] = user;
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
    }

    public async Task JoinVoiceChannelAsync(string channelId, string userId, string username, string avatarUrl)
    {
        if (_connection == null || !IsConnected) return;

        await _connection.InvokeAsync("JoinVoiceChannel", channelId, userId, username, avatarUrl);
        IsInVoiceChannel = true;

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
        _voiceUsers.Clear();
    }

    private void StartAudioCapture()
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(48000, 16, 1),
                BufferMilliseconds = 20
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();

            // Setup playback
            _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                DiscardOnBufferOverflow = true
            };

            _waveOut = new WaveOutEvent();
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
        if (IsMuted || _connection == null || !IsConnected) return;

        // Calculate audio level
        var max = 0f;
        for (var i = 0; i < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
            if (sample > max) max = sample;
        }

        var audioLevel = max;
        var wasSpeaking = _isSpeaking;
        _isSpeaking = audioLevel > 0.02; // Voice activity detection threshold

        // Only send updates at reasonable intervals
        if (_isSpeaking != wasSpeaking || (DateTime.Now - _lastSpeakingUpdate).TotalMilliseconds > 100)
        {
            _lastSpeakingUpdate = DateTime.Now;
            try
            {
                await _connection.InvokeAsync("UpdateSpeakingState", _isSpeaking, (double)audioLevel);
            }
            catch
            {
                // Ignore send errors
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
    }
}
