# System Audio Capture Implementation Guide

**Status:** 🔴 NOT IMPLEMENTED YET
**Priority:** HIGH (Critical missing feature for screen sharing)
**Complexity:** MEDIUM-HIGH (Requires audio mixing and encoder management)

---

## Problem Statement

**Current State:**
Screen sharing only captures VIDEO. When users share their screen, viewers cannot hear:
- Game audio
- Music/media playback
- Application sounds
- System notifications
- Any desktop audio

**User Impact:**
This makes screen sharing significantly less useful than competitors (Discord, Zoom, OBS) which all support system audio capture.

---

## Solution Overview

Implement WASAPI (Windows Audio Session API) loopback capture to record desktop audio and send it alongside screen share video frames.

### Key Components:

1. **WASAPI Loopback Capture** - Captures all system audio (what you hear)
2. **Audio Mixing** - Optionally combine desktop audio + microphone
3. **Opus Encoding** - Compress audio before network transmission
4. **SignalR Transmission** - Send audio packets to server
5. **Client Playback** - Decode and play desktop audio for viewers

---

## Architecture Design

### Current Audio Flow (Microphone Only):
```
Microphone → WaveInEvent → Voice Activity Detection → Opus Encode → SignalR → Server
```

### Proposed Audio Flow (Desktop + Mic):
```
Desktop Audio → WasapiLoopbackCapture ──┐
                                         ├→ Audio Mixer → Opus Encode → SignalR → Server
Microphone → WaveInEvent ───────────────┘
```

---

## Implementation Steps

### Step 1: Add WASAPI Loopback Capture

**File:** `ScreenSharingManager.cs`

**Add Fields:**
```csharp
// System audio capture (WASAPI loopback)
private WasapiLoopbackCapture? _wasapiCapture;
private BufferedWaveProvider? _desktopAudioBuffer;
private OpusEncoder? _desktopAudioEncoder;
private readonly ConcurrentQueue<byte[]> _desktopAudioQueue = new();
private Thread? _desktopAudioSendThread;
private CancellationTokenSource? _desktopAudioCts;
```

**Start Capture Method:**
```csharp
private void StartDesktopAudioCapture()
{
    if (!_settings.ShareAudio) return;

    try
    {
        // Initialize WASAPI loopback (captures all desktop audio)
        _wasapiCapture = new WasapiLoopbackCapture();

        // Initialize Opus encoder for desktop audio
        _desktopAudioEncoder = new OpusEncoder(
            _wasapiCapture.WaveFormat.SampleRate,
            _wasapiCapture.WaveFormat.Channels,
            OpusApplication.OPUS_APPLICATION_AUDIO // Use AUDIO mode, not VOIP
        )
        {
            Bitrate = 96000, // 96 kbps for good desktop audio quality
            Complexity = 8,  // Higher complexity for better quality
            UseVBR = true
        };

        // Buffer for desktop audio
        _desktopAudioBuffer = new BufferedWaveProvider(_wasapiCapture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(200)
        };

        // Subscribe to audio data events
        _wasapiCapture.DataAvailable += OnDesktopAudioDataAvailable;
        _wasapiCapture.RecordingStopped += OnDesktopAudioRecordingStopped;

        // Start recording desktop audio
        _wasapiCapture.StartRecording();

        // Start send thread for desktop audio
        _desktopAudioCts = new CancellationTokenSource();
        _desktopAudioSendThread = new Thread(() => DesktopAudioSendLoop(_desktopAudioCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal, // Lower than voice mic (which is Highest)
            Name = "DesktopAudioSendThread"
        };
        _desktopAudioSendThread.Start();

        Debug.WriteLine("Desktop audio capture started successfully");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Failed to start desktop audio capture: {ex.Message}");
        CleanupDesktopAudioCapture();
    }
}

private void OnDesktopAudioDataAvailable(object? sender, WaveInEventArgs e)
{
    if (e.BytesRecorded == 0) return;

    // Queue desktop audio for encoding and sending
    var audioData = new byte[e.BytesRecorded];
    Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);
    _desktopAudioQueue.Enqueue(audioData);
}

private void OnDesktopAudioRecordingStopped(object? sender, StoppedEventArgs e)
{
    if (e.Exception != null)
    {
        Debug.WriteLine($"Desktop audio recording stopped with error: {e.Exception.Message}");
    }
}

private void DesktopAudioSendLoop(CancellationToken cancellationToken)
{
    var opusBuffer = new byte[4000];
    var spinWait = new SpinWait();

    while (!cancellationToken.IsCancellationRequested && _isSharing)
    {
        try
        {
            if (_desktopAudioQueue.TryDequeue(out var pcmData))
            {
                var encoder = _desktopAudioEncoder;
                if (encoder != null && pcmData.Length >= 2)
                {
                    // Convert to shorts for Opus
                    var pcmSamples = new short[pcmData.Length / 2];
                    Buffer.BlockCopy(pcmData, 0, pcmSamples, 0, pcmData.Length);

                    // Encode with Opus
                    var encodedLength = encoder.Encode(pcmSamples, 0, 960, opusBuffer, 0, opusBuffer.Length);

                    if (encodedLength > 0)
                    {
                        var opusData = new byte[encodedLength];
                        Buffer.BlockCopy(opusBuffer, 0, opusData, 0, encodedLength);

                        // Send via SignalR (need to add server method)
                        // TODO: Implement SendDesktopAudio hub method
                        // _ = _connection?.SendAsync("SendDesktopAudio", opusData, cancellationToken);
                    }
                }
            }
            else
            {
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Desktop audio send error: {ex.Message}");
        }
    }
}

private void CleanupDesktopAudioCapture()
{
    // Stop send thread
    _desktopAudioCts?.Cancel();
    try { _desktopAudioSendThread?.Join(1000); } catch { }
    _desktopAudioCts?.Dispose();
    _desktopAudioCts = null;
    _desktopAudioSendThread = null;

    // Clear queue
    while (_desktopAudioQueue.TryDequeue(out _)) { }

    // Stop WASAPI capture
    try { _wasapiCapture?.StopRecording(); } catch { }
    try { _wasapiCapture?.Dispose(); } catch { }
    _wasapiCapture = null;

    // Cleanup encoder
    _desktopAudioEncoder?.Dispose();
    _desktopAudioEncoder = null;

    _desktopAudioBuffer = null;
}
```

**Call from StartSharingAsync:**
```csharp
public async Task StartSharingAsync(DisplayInfo display, ScreenShareSettings? settings = null)
{
    // ... existing code ...

    // Start desktop audio capture if enabled
    if (_settings.ShareAudio)
    {
        StartDesktopAudioCapture();
    }

    // ... rest of existing code ...
}
```

**Call from StopSharingAsync:**
```csharp
public async Task StopSharingAsync()
{
    // ... existing code ...

    // Stop desktop audio capture
    CleanupDesktopAudioCapture();

    // ... rest of existing code ...
}
```

---

### Step 2: Add Server-Side Hub Methods

**File:** `VoiceHub.cs`

**Add Method:**
```csharp
public async Task SendDesktopAudio(byte[] opusData)
{
    var connectionId = Context.ConnectionId;

    // Broadcast desktop audio to all viewers in the same channel
    // Use a different method name to distinguish from mic audio
    await Clients.Others.SendAsync("ReceiveDesktopAudio", connectionId, opusData);
}
```

---

### Step 3: Add Client-Side Audio Playback

**File:** `IScreenShareViewerService.cs` (or new `ScreenShareAudioService`)

**Add Fields:**
```csharp
private readonly ConcurrentDictionary<string, OpusDecoder> _desktopAudioDecoders = new();
private readonly ConcurrentDictionary<string, BufferedWaveProvider> _desktopAudioBuffers = new();
private WaveOutEvent? _desktopAudioPlayer;
```

**Add Method:**
```csharp
public void HandleDesktopAudio(string sharerConnectionId, byte[] opusData)
{
    try
    {
        // Get or create decoder for this sharer
        var decoder = _desktopAudioDecoders.GetOrAdd(sharerConnectionId, _ =>
        {
            return new OpusDecoder(48000, 2); // Stereo desktop audio
        });

        // Get or create audio buffer
        var buffer = _desktopAudioBuffers.GetOrAdd(sharerConnectionId, _ =>
        {
            var waveFormat = new WaveFormat(48000, 16, 2);
            return new BufferedWaveProvider(waveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(300)
            };
        });

        // Decode Opus to PCM
        var pcmBuffer = new short[5760]; // Max Opus frame size
        var decodedLength = decoder.Decode(opusData, 0, opusData.Length, pcmBuffer, 0, 960);

        if (decodedLength > 0)
        {
            // Convert to bytes
            var pcmBytes = new byte[decodedLength * 2];
            Buffer.BlockCopy(pcmBuffer, 0, pcmBytes, 0, pcmBytes.Length);

            // Add to playback buffer
            buffer.AddSamples(pcmBytes, 0, pcmBytes.Length);

            // Start player if not already playing
            if (_desktopAudioPlayer == null || _desktopAudioPlayer.PlaybackState != PlaybackState.Playing)
            {
                EnsureDesktopAudioPlayerStarted(buffer);
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Desktop audio playback error: {ex.Message}");
    }
}

private void EnsureDesktopAudioPlayerStarted(BufferedWaveProvider buffer)
{
    if (_desktopAudioPlayer == null)
    {
        _desktopAudioPlayer = new WaveOutEvent
        {
            DesiredLatency = 200,
            NumberOfBuffers = 3
        };
        _desktopAudioPlayer.Init(buffer);
        _desktopAudioPlayer.Play();
    }
}
```

---

### Step 4: Wire Up SignalR Events

**File:** `VoiceService.cs` (in SetupHubEventHandlers method)

**Add Handler:**
```csharp
_connection.On<string, byte[]>("ReceiveDesktopAudio", (sharerConnectionId, opusData) =>
{
    // Forward to screen share viewer service
    _screenShareViewerService?.HandleDesktopAudio(sharerConnectionId, opusData);
});
```

---

## Advanced: Audio Mixing (Optional)

If you want to mix microphone + desktop audio into a single stream:

```csharp
public class AudioMixer
{
    public byte[] MixAudio(byte[] micAudio, byte[] desktopAudio, float micVolume = 0.7f, float desktopVolume = 0.5f)
    {
        var maxLength = Math.Max(micAudio.Length, desktopAudio.Length);
        var mixed = new byte[maxLength];

        for (int i = 0; i + 1 < maxLength; i += 2)
        {
            // Convert to 16-bit samples
            short micSample = 0;
            if (i + 1 < micAudio.Length)
            {
                micSample = (short)((micAudio[i + 1] << 8) | micAudio[i]);
            }

            short desktopSample = 0;
            if (i + 1 < desktopAudio.Length)
            {
                desktopSample = (short)((desktopAudio[i + 1] << 8) | desktopAudio[i]);
            }

            // Mix with volume control
            int mixedSample = (int)(micSample * micVolume + desktopSample * desktopVolume);

            // Prevent clipping
            mixedSample = Math.Max(-32768, Math.Min(32767, mixedSample));

            // Convert back to bytes
            mixed[i] = (byte)(mixedSample & 0xFF);
            mixed[i + 1] = (byte)((mixedSample >> 8) & 0xFF);
        }

        return mixed;
    }
}
```

---

## Testing Plan

### Test 1: Basic Desktop Audio Capture
1. Start screen sharing with ShareAudio = true
2. Play music/video on desktop
3. Verify viewer hears desktop audio

### Test 2: Audio/Video Sync
1. Play video with audio
2. Verify audio and video are synchronized (< 100ms latency)

### Test 3: Audio Quality
1. Play high-quality music
2. Verify no crackling, stuttering, or dropouts
3. Verify reasonable audio quality (not overly compressed)

### Test 4: Microphone + Desktop Mix
1. Enable both mic and desktop audio
2. Verify both are audible
3. Verify proper volume balance

### Test 5: Performance
1. Monitor CPU usage with desktop audio enabled
2. Should be < 5% additional CPU
3. Verify no memory leaks over 30 minutes

---

## Known Challenges

### Challenge 1: Audio/Video Sync
**Problem:** Audio and video may drift out of sync over time
**Solution:** Add timestamps to both audio and video packets, implement jitter buffer

### Challenge 2: Sample Rate Mismatch
**Problem:** WASAPI loopback may use different sample rates (44.1kHz, 48kHz, etc.)
**Solution:** Use NAudio's MediaFoundationResampler to convert to 48kHz

### Challenge 3: Audio Latency
**Problem:** Desktop audio may have higher latency than voice mic
**Solution:** Use smaller buffer sizes (50-100ms) for desktop audio

### Challenge 4: No Desktop Audio Available
**Problem:** Some systems may not have audio output devices
**Solution:** Gracefully disable desktop audio capture, show warning to user

---

## Performance Considerations

### CPU Usage:
- WASAPI capture: ~1-2% CPU
- Opus encoding (96kbps stereo): ~2-3% CPU
- Total additional overhead: ~3-5% CPU

### Bandwidth Usage:
- Desktop audio (96kbps): ~12 KB/s
- Total with video: Video bandwidth + 12 KB/s

### Memory Usage:
- Audio buffers: ~100 KB per active screen share
- Minimal additional memory overhead

---

## Alternative Approaches

### Approach 1: Use FFmpeg for Audio Capture
**Pros:** More flexible, supports multiple audio sources
**Cons:** Requires FFmpeg installation, more complex setup

### Approach 2: DirectShow/Windows CoreAudio
**Pros:** Native Windows API, no dependencies
**Cons:** More complex P/Invoke code, harder to maintain

### Approach 3: Virtual Audio Cable
**Pros:** User can route any audio
**Cons:** Requires third-party software, not user-friendly

**Recommendation:** Use WASAPI loopback (proposed approach) - best balance of simplicity and functionality

---

## Dependencies

Required NuGet packages (already installed):
- ✅ NAudio (for WasapiLoopbackCapture)
- ✅ Concentus (for Opus encoding/decoding)
- ✅ SignalR (for network transmission)

No additional dependencies needed.

---

## Deployment Notes

### Windows Compatibility:
- WASAPI requires Windows Vista or later ✅
- Works on Windows 7, 8, 10, 11 ✅

### Permissions:
- No special permissions required
- Works in user mode

### Configuration:
- Users can enable/disable via ShareAudio setting
- Default: enabled (ShareAudio = true)

---

## Future Enhancements

1. **Audio Source Selection:** Allow users to select specific applications to capture
2. **Audio Filters:** Add noise suppression, echo cancellation for desktop audio
3. **Multi-Channel Audio:** Support 5.1/7.1 surround sound
4. **Audio Ducking:** Automatically lower desktop audio when speaking
5. **Audio Recording:** Save desktop audio alongside screen recording

---

## Conclusion

Implementing system audio capture is **critical** for feature parity with Discord, Zoom, and other screen sharing platforms. The proposed WASAPI loopback approach is the most straightforward and performant solution.

**Estimated Implementation Time:** 4-6 hours for basic implementation, 8-12 hours with audio mixing and polish.

**Priority:** HIGH - This should be implemented as soon as possible to improve screen sharing functionality.
