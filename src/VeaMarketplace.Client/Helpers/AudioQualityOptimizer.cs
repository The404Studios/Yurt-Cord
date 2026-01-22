using System;
using System.Buffers;
using System.Diagnostics;
using NAudio.Wave;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Audio quality preset
/// </summary>
public enum AudioQualityPreset
{
    Low,        // 8kHz, Mono - for poor connections
    Medium,     // 16kHz, Mono - for average connections
    High,       // 24kHz, Stereo - for good connections
    VeryHigh,   // 48kHz, Stereo - for excellent connections
    Custom      // User-defined settings
}

/// <summary>
/// Audio quality settings
/// </summary>
public class AudioQualitySettings
{
    public int SampleRate { get; set; } = 48000;
    public int BitDepth { get; set; } = 16;
    public int Channels { get; set; } = 2;
    public int Bitrate { get; set; } = 64000;
    public bool NoiseSuppressionEnabled { get; set; } = true;
    public bool EchoCancellationEnabled { get; set; } = true;
    public bool AutomaticGainControlEnabled { get; set; } = true;
    public int PacketLossConcealment { get; set; } = 3; // 0-10 scale
}

/// <summary>
/// Audio statistics for quality monitoring
/// </summary>
public class AudioQualityStats
{
    public double AverageVolume { get; set; }
    public double PeakVolume { get; set; }
    public int ClippingCount { get; set; }
    public double SignalToNoiseRatio { get; set; }
    public int PacketsLost { get; set; }
    public int PacketsReceived { get; set; }
    public double Jitter { get; set; }
}

/// <summary>
/// Provides audio quality optimization and adaptive settings
/// </summary>
public class AudioQualityOptimizer
{
    private readonly AudioQualitySettings _currentSettings;
    private readonly object _lock = new();

    private double _currentBandwidth;
    private int _packetLossRate;
    private int _latency;

    public AudioQualitySettings CurrentSettings => _currentSettings;

    public AudioQualityOptimizer(AudioQualityPreset preset = AudioQualityPreset.High)
    {
        _currentSettings = GetPresetSettings(preset);
        Debug.WriteLine($"Audio quality optimizer initialized with preset: {preset}");
    }

    /// <summary>
    /// Gets preset quality settings
    /// </summary>
    public static AudioQualitySettings GetPresetSettings(AudioQualityPreset preset)
    {
        return preset switch
        {
            AudioQualityPreset.Low => new AudioQualitySettings
            {
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                Bitrate = 16000,
                NoiseSuppressionEnabled = true,
                EchoCancellationEnabled = true,
                AutomaticGainControlEnabled = true,
                PacketLossConcealment = 5
            },
            AudioQualityPreset.Medium => new AudioQualitySettings
            {
                SampleRate = 16000,
                BitDepth = 16,
                Channels = 1,
                Bitrate = 32000,
                NoiseSuppressionEnabled = true,
                EchoCancellationEnabled = true,
                AutomaticGainControlEnabled = true,
                PacketLossConcealment = 4
            },
            AudioQualityPreset.High => new AudioQualitySettings
            {
                SampleRate = 24000,
                BitDepth = 16,
                Channels = 2,
                Bitrate = 64000,
                NoiseSuppressionEnabled = true,
                EchoCancellationEnabled = true,
                AutomaticGainControlEnabled = true,
                PacketLossConcealment = 3
            },
            AudioQualityPreset.VeryHigh => new AudioQualitySettings
            {
                SampleRate = 48000,
                BitDepth = 16,
                Channels = 2,
                Bitrate = 128000,
                NoiseSuppressionEnabled = false,
                EchoCancellationEnabled = true,
                AutomaticGainControlEnabled = true,
                PacketLossConcealment = 2
            },
            _ => new AudioQualitySettings()
        };
    }

    /// <summary>
    /// Optimizes audio settings based on network conditions
    /// </summary>
    public AudioQualitySettings OptimizeForNetwork(double bandwidthKbps, int packetLossPercent, int latencyMs)
    {
        lock (_lock)
        {
            _currentBandwidth = bandwidthKbps;
            _packetLossRate = packetLossPercent;
            _latency = latencyMs;

            var optimized = new AudioQualitySettings();

            // Determine sample rate based on available bandwidth
            if (bandwidthKbps >= 512)
            {
                optimized.SampleRate = 48000;
                optimized.Bitrate = 128000;
                optimized.Channels = 2;
            }
            else if (bandwidthKbps >= 256)
            {
                optimized.SampleRate = 24000;
                optimized.Bitrate = 64000;
                optimized.Channels = 2;
            }
            else if (bandwidthKbps >= 128)
            {
                optimized.SampleRate = 16000;
                optimized.Bitrate = 32000;
                optimized.Channels = 1;
            }
            else
            {
                optimized.SampleRate = 8000;
                optimized.Bitrate = 16000;
                optimized.Channels = 1;
            }

            // Adjust for packet loss
            if (packetLossPercent > 10)
            {
                optimized.PacketLossConcealment = 8;
                optimized.SampleRate = Math.Min(optimized.SampleRate, 16000);
            }
            else if (packetLossPercent > 5)
            {
                optimized.PacketLossConcealment = 5;
            }
            else
            {
                optimized.PacketLossConcealment = 2;
            }

            // Adjust for latency
            if (latencyMs > 200)
            {
                optimized.EchoCancellationEnabled = true;
                optimized.NoiseSuppressionEnabled = true;
            }

            optimized.AutomaticGainControlEnabled = true;
            optimized.BitDepth = 16; // Standard

            Debug.WriteLine($"Audio optimized for network: {bandwidthKbps}kbps, {packetLossPercent}% loss, {latencyMs}ms latency");
            Debug.WriteLine($"  Settings: {optimized.SampleRate}Hz, {optimized.Channels}ch, {optimized.Bitrate}bps");

            // Copy to current settings
            CopySettings(optimized, _currentSettings);

            return optimized;
        }
    }

    /// <summary>
    /// Applies noise gate to reduce background noise (in-place, no allocation)
    /// </summary>
    public void ApplyNoiseGateInPlace(byte[] audioData, float threshold = -40.0f)
    {
        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            // Convert bytes to 16-bit sample
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);

            // Calculate amplitude in dB
            float amplitude = 20 * (float)Math.Log10(Math.Abs(sample) / 32768.0f + float.Epsilon);

            if (amplitude < threshold)
            {
                // Below threshold, mute
                audioData[i] = 0;
                audioData[i + 1] = 0;
            }
        }
    }

    /// <summary>
    /// Applies noise gate to reduce background noise.
    /// IMPORTANT: Caller must return the result array to ArrayPool when done.
    /// </summary>
    public byte[] ApplyNoiseGate(byte[] audioData, float threshold = -40.0f)
    {
        var result = ArrayPool<byte>.Shared.Rent(audioData.Length);

        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            // Convert bytes to 16-bit sample
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);

            // Calculate amplitude in dB
            float amplitude = 20 * (float)Math.Log10(Math.Abs(sample) / 32768.0f + float.Epsilon);

            if (amplitude < threshold)
            {
                // Below threshold, mute
                result[i] = 0;
                result[i + 1] = 0;
            }
            else
            {
                // Above threshold, pass through
                result[i] = audioData[i];
                result[i + 1] = audioData[i + 1];
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a pooled audio buffer. Call this when done with buffers from ApplyNoiseGate/ApplyAutomaticGainControl.
    /// </summary>
    public static void ReturnBuffer(byte[] buffer)
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }

    /// <summary>
    /// Applies automatic gain control to normalize volume (in-place, no allocation)
    /// </summary>
    public void ApplyAutomaticGainControlInPlace(byte[] audioData, float targetLevel = -20.0f)
    {
        // Calculate current RMS level
        double sum = 0;
        int sampleCount = audioData.Length / 2;

        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
            sum += sample * sample;
        }

        if (sampleCount == 0) return;

        double rms = Math.Sqrt(sum / sampleCount);
        if (rms < 1.0) return; // Avoid log of zero

        double currentDb = 20 * Math.Log10(rms / 32768.0);

        // Calculate gain needed
        double gainDb = targetLevel - currentDb;
        double gain = Math.Pow(10, gainDb / 20.0);

        // Limit gain to reasonable range
        gain = Math.Max(0.1, Math.Min(10.0, gain));

        // Apply gain in-place
        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
            int adjusted = (int)(sample * gain);

            // Prevent clipping
            adjusted = Math.Clamp(adjusted, -32768, 32767);

            audioData[i] = (byte)(adjusted & 0xFF);
            audioData[i + 1] = (byte)((adjusted >> 8) & 0xFF);
        }
    }

    /// <summary>
    /// Applies automatic gain control to normalize volume.
    /// IMPORTANT: Caller must return the result array to ArrayPool when done via ReturnBuffer().
    /// </summary>
    public byte[] ApplyAutomaticGainControl(byte[] audioData, float targetLevel = -20.0f)
    {
        var result = ArrayPool<byte>.Shared.Rent(audioData.Length);

        // Calculate current RMS level
        double sum = 0;
        int sampleCount = audioData.Length / 2;

        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
            sum += sample * sample;
        }

        if (sampleCount == 0)
        {
            Buffer.BlockCopy(audioData, 0, result, 0, audioData.Length);
            return result;
        }

        double rms = Math.Sqrt(sum / sampleCount);
        if (rms < 1.0)
        {
            Buffer.BlockCopy(audioData, 0, result, 0, audioData.Length);
            return result;
        }

        double currentDb = 20 * Math.Log10(rms / 32768.0);

        // Calculate gain needed
        double gainDb = targetLevel - currentDb;
        double gain = Math.Pow(10, gainDb / 20.0);

        // Limit gain to reasonable range
        gain = Math.Max(0.1, Math.Min(10.0, gain));

        // Apply gain
        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
            int adjusted = (int)(sample * gain);

            // Prevent clipping
            adjusted = Math.Clamp(adjusted, -32768, 32767);

            result[i] = (byte)(adjusted & 0xFF);
            result[i + 1] = (byte)((adjusted >> 8) & 0xFF);
        }

        return result;
    }

    /// <summary>
    /// Analyzes audio quality
    /// </summary>
    public AudioQualityStats AnalyzeAudio(byte[] audioData)
    {
        var stats = new AudioQualityStats();

        if (audioData.Length < 2)
            return stats;

        double sum = 0;
        double sumSquares = 0;
        int peakSample = 0;
        int clippingCount = 0;
        int sampleCount = 0;

        for (int i = 0; i < audioData.Length; i += 2)
        {
            if (i + 1 < audioData.Length)
            {
                short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                int absSample = Math.Abs(sample);

                sum += absSample;
                sumSquares += sample * sample;
                peakSample = Math.Max(peakSample, absSample);

                // Count clipping (near max value)
                if (absSample > 32000)
                {
                    clippingCount++;
                }

                sampleCount++;
            }
        }

        if (sampleCount > 0)
        {
            double avgSample = sum / sampleCount;
            double rms = Math.Sqrt(sumSquares / sampleCount);

            stats.AverageVolume = 20 * Math.Log10(avgSample / 32768.0);
            stats.PeakVolume = 20 * Math.Log10(peakSample / 32768.0);
            stats.ClippingCount = clippingCount;

            // Simple SNR estimation
            double noise = avgSample * 0.01; // Assume 1% is noise
            stats.SignalToNoiseRatio = 20 * Math.Log10(rms / Math.Max(noise, 1.0));
        }

        return stats;
    }

    /// <summary>
    /// Converts audio format. Uses pooled buffer internally.
    /// </summary>
    public byte[]? ConvertFormat(byte[] audioData, WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        byte[]? buffer = null;
        try
        {
            using var sourceStream = new RawSourceWaveStream(audioData, 0, audioData.Length, sourceFormat);
            using var resampler = new MediaFoundationResampler(sourceStream, targetFormat);

            // Use pooled buffer for reading
            var bufferSize = resampler.WaveFormat.AverageBytesPerSecond;
            buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            using var outputStream = new System.IO.MemoryStream();

            int bytesRead;
            while ((bytesRead = resampler.Read(buffer, 0, bufferSize)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }

            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to convert audio format: {ex.Message}");
            return null;
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static void CopySettings(AudioQualitySettings source, AudioQualitySettings target)
    {
        target.SampleRate = source.SampleRate;
        target.BitDepth = source.BitDepth;
        target.Channels = source.Channels;
        target.Bitrate = source.Bitrate;
        target.NoiseSuppressionEnabled = source.NoiseSuppressionEnabled;
        target.EchoCancellationEnabled = source.EchoCancellationEnabled;
        target.AutomaticGainControlEnabled = source.AutomaticGainControlEnabled;
        target.PacketLossConcealment = source.PacketLossConcealment;
    }
}
