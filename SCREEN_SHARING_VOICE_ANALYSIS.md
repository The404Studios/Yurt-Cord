# Screen Sharing & Voice/Video Analysis and Fixes

**Date:** 2026-01-04

## Executive Summary

Analysis of the screen sharing and voice/video systems revealed three critical issues that prevent optimal screen sharing functionality:

1. **NO SYSTEM AUDIO CAPTURE**: Screen sharing only captures video - desktop/application audio is NOT captured
2. **60 FPS SUPPORT EXISTS BUT NOT OPTIMAL**: Infrastructure exists but needs optimization for stable 60 FPS streaming
3. **IMAGE QUALITY TOO LOW**: Default JPEG quality (45) is too low for good visual fidelity

---

## Critical Issues Found

### 1. ✅ Screen Sharing Audio Capture - CRITICAL BUG

**Problem:** Screen sharing does NOT capture system audio (desktop audio/application sounds)

**Evidence:**
- `ScreenShareSettings.ShareAudio` exists but is **set to false by default** (IScreenSharingManager.cs:33)
- NO implementation of system audio capture exists anywhere in the codebase
- Only microphone audio is captured via `WaveInEvent` in VoiceService.cs
- Screen sharing only captures video frames via `Graphics.CopyFromScreen()`

**Root Cause:**
The application needs WASAPI loopback capture to capture desktop/system audio, but this was never implemented. When users share their screen, viewers cannot hear application audio, music, game sounds, etc.

**Impact:** HIGH
- Users cannot share screen audio (major feature gap vs Discord/Zoom)
- Screen sharing is video-only, making it less useful for gaming, media playback, presentations

---

### 2. ✅ 60 FPS Support - Needs Optimization

**Current State:**
- Default FPS: 30 (ScreenShareSettings:26)
- FullHD preset: 1080p @ 60 FPS (~16 Mbps)
- QHD60 preset: 1440p @ 60 FPS (~30 Mbps)
- Infrastructure exists: frame timing, encoding, network transmission

**Problems:**
1. Default presets don't emphasize 60 FPS enough
2. Frame timing could be more precise for consistent 60 FPS
3. H.264 encoding may need optimization for 60 FPS stability

**Recommended Changes:**
- Add dedicated "High" preset: 720p @ 60 FPS (~6 Mbps) for smooth gaming/motion
- Improve frame timing precision in `CaptureLoop()`
- Ensure H.264 encoder handles 60 FPS efficiently
- Update default from 30 FPS to 60 FPS for high-quality preset

---

### 3. ✅ Image Quality - TOO LOW

**Current Quality Settings:**
- Default JPEG quality: **45** (very low, causes visible artifacts)
- HighQuality preset: 85 quality (good)
- Low preset: 30 quality (for poor connections)

**Problems:**
```csharp
// IScreenSharingManager.cs:29
public int JpegQuality { get; set; } = 45; // Lower quality = less CPU for encoding
```

This is far too low for modern screen sharing. For comparison:
- JPEG quality 45: Visible blocking artifacts, poor text readability
- JPEG quality 75-85: Good balance of quality/size
- JPEG quality 90+: Near-lossless but large file sizes

**Impact:**
- Text appears blurry and hard to read
- Images have visible compression artifacts
- Screen details are lost

**Solution:**
- Increase default JPEG quality to **75** (industry standard)
- Update High preset to use quality **85**
- Ensure H.264 encoding uses proper bitrate for quality

---

### 4. ✅ Stability Issues

**Frame Dropping:**
- Current logic drops frames too aggressively when queue backs up
- Voice priority is good, but video frame skipping is too extreme

**Adaptive Quality:**
- Reduces JPEG quality and resolution under network pressure ✓ (good)
- Never reduces FPS ✓ (good - maintains smooth motion)

**Recommendations:**
- Improve frame dropping heuristics
- Better network congestion detection
- Smoother quality transitions

---

## System Architecture Analysis

### Current Screen Sharing Flow:

1. **Capture** (`CaptureLoop` - AboveNormal priority thread):
   ```
   Graphics.CopyFromScreen() → Resize → Queue for encoding
   ```

2. **Encode** (`EncodeLoop` - Normal priority thread):
   ```
   Dequeue bitmap → Try adaptive streaming → Try H.264 hardware → Fallback to JPEG → Queue for sending
   ```

3. **Send** (`SendLoop` - async task):
   ```
   Dequeue encoded frame → Yield to voice if active → Send via SignalR → Update stats
   ```

### Current Voice/Audio Flow:

1. **Microphone Capture** (`WaveInEvent`):
   ```
   Mic audio → Voice activity detection → Opus encode → Queue → Send via SignalR
   ```

2. **Audio Receive**:
   ```
   Receive Opus → Decode to PCM → Apply volume → Add to playback buffer → Play via WaveOut
   ```

### Missing: System Audio Capture

**What's needed:**
```
WASAPI Loopback → Capture desktop audio → Mix with mic (optional) → Opus encode → Send with video
```

---

## Solutions

### Solution 1: Add System Audio Capture for Screen Sharing

**Implementation:**
1. Add `WasapiLoopbackCapture` for desktop audio
2. Create audio mixer to combine desktop audio + microphone
3. Add separate audio send queue for screen share audio
4. Wire audio capture to screen sharing lifecycle

**Files to modify:**
- `VoiceService.cs` - Add WASAPI loopback capture
- `ScreenSharingManager.cs` - Add audio capture lifecycle
- `ScreenShareSettings.cs` - Enable ShareAudio by default
- `VoiceHub.cs` - Handle screen share audio packets

**Code Pattern:**
```csharp
// Add to VoiceService.cs
private WasapiLoopbackCapture? _wasapiCapture;
private bool _captureSystemAudio;

private void StartSystemAudioCapture()
{
    _wasapiCapture = new WasapiLoopbackCapture();
    _wasapiCapture.DataAvailable += OnSystemAudioDataAvailable;
    _wasapiCapture.StartRecording();
}

private void OnSystemAudioDataAvailable(object sender, WaveInEventArgs e)
{
    // Mix with microphone audio or send separately
    // Opus encode and send via SignalR
}
```

---

### Solution 2: Optimize 60 FPS Support

**Changes:**
1. Update quality presets:
   ```csharp
   ScreenShareQuality.High => 720p @ 60 FPS, quality 75, 6 Mbps
   ScreenShareQuality.FullHD => 1080p @ 60 FPS, quality 80, 16 Mbps
   ```

2. Improve frame timing in `CaptureLoop()`:
   ```csharp
   // Use high-precision timer for 60 FPS
   var frameIntervalMs = 1000.0 / 60.0; // 16.67ms per frame
   var lastFrameTime = Stopwatch.GetTimestamp();

   while (!cancellationToken.IsCancellationRequested)
   {
       var now = Stopwatch.GetTimestamp();
       var elapsed = (now - lastFrameTime) * 1000.0 / Stopwatch.Frequency;

       if (elapsed >= frameIntervalMs - 0.5) // 0.5ms tolerance
       {
           lastFrameTime = now;
           CaptureFrame();
       }
   }
   ```

3. Ensure H.264 encoding supports 60 FPS properly

---

### Solution 3: Improve Image Quality

**Changes:**
1. Update default quality:
   ```csharp
   public int JpegQuality { get; set; } = 75; // Much better quality
   ```

2. Update presets:
   ```csharp
   ScreenShareQuality.Medium => quality 70
   ScreenShareQuality.High => quality 80
   ScreenShareQuality.FullHD => quality 85
   ```

3. Ensure H.264 uses proper bitrate (currently: BitrateKbps setting)

---

### Solution 4: Improve Stability

**Changes:**
1. Better frame dropping logic:
   ```csharp
   // Drop oldest frames only when queue > 10, not 3
   if (_frameQueue.Count > 10)
   {
       // Skip only 30% of backlog, not 50%
       var skipCount = _frameQueue.Count / 3;
   }
   ```

2. Smoother adaptive quality transitions
3. Better network congestion detection using StreamingOrchestrator

---

## Testing Plan

### Test 1: System Audio Capture
- [ ] Start screen share with desktop audio playing
- [ ] Verify viewer hears desktop audio
- [ ] Test audio mixing (mic + desktop)
- [ ] Test audio-only screen share
- [ ] Verify no audio echo/feedback

### Test 2: 60 FPS Performance
- [ ] Share screen at 720p @ 60 FPS
- [ ] Verify consistent 60 FPS (check stats)
- [ ] Test with motion (scroll, video, gaming)
- [ ] Monitor CPU usage
- [ ] Verify no frame stuttering

### Test 3: Image Quality
- [ ] Share screen with text (code editor)
- [ ] Verify text is sharp and readable
- [ ] Share screen with images
- [ ] Compare quality at different presets
- [ ] Verify no blocking artifacts

### Test 4: Stability
- [ ] Run screen share for 30+ minutes
- [ ] Test with poor network conditions
- [ ] Verify adaptive quality works smoothly
- [ ] Test with multiple concurrent screen shares
- [ ] Monitor memory usage (no leaks)

---

## Implementation Priority

1. **CRITICAL**: Add system audio capture (major missing feature)
2. **HIGH**: Improve image quality (immediate user experience)
3. **MEDIUM**: Optimize 60 FPS (performance improvement)
4. **LOW**: Improve stability (incremental improvements)

---

## Files to Modify

### Primary Changes:
1. **VoiceService.cs** - Add WASAPI loopback capture, audio mixing
2. **IScreenSharingManager.cs** - Update quality presets, default settings
3. **ScreenSharingManager.cs** - Wire audio capture to screen share lifecycle
4. **VoiceHub.cs** - Handle screen share audio packets (if separate from video)

### Configuration Changes:
1. **ScreenShareSettings** - ShareAudio default to true, JpegQuality to 75
2. **Quality Presets** - Add 60 FPS presets, update quality values

---

## Deployment Notes

### Breaking Changes
None - these are enhancements and bug fixes

### Performance Impact
- System audio capture: Minimal CPU impact (<2%)
- Higher JPEG quality: Slightly more CPU (+5-10%) but better image
- 60 FPS: ~2x CPU usage for capture/encode (acceptable on modern hardware)

### Bandwidth Requirements
- 720p @ 60 FPS, quality 75: ~6 Mbps upload
- 1080p @ 60 FPS, quality 80: ~16 Mbps upload
- System audio: +24-64 kbps (Opus compressed)

---

## Conclusion

Screen sharing currently has a critical missing feature (system audio) and suboptimal defaults (low quality, 30 FPS). The implementation below will:

✅ Add system audio capture (WASAPI loopback)
✅ Optimize for smooth 60 FPS streaming
✅ Improve image quality to industry standards
✅ Enhance stability and user experience

All infrastructure exists - we just need to wire it up correctly and adjust defaults.
