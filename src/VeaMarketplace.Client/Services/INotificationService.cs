using System.IO;
using System.Media;
using System.Reflection;
using System.Windows.Media;

namespace VeaMarketplace.Client.Services;

public interface INotificationService
{
    void PlayUserJoinSound();
    void PlayUserLeaveSound();
    void PlayMessageSound();
    void PlayCallSound();
    void PlayMentionSound();
    void PlayFriendRequestSound();
    void StopAllSounds();
}

public class NotificationService : INotificationService
{
    private MediaPlayer? _mediaPlayer;
    private readonly string _soundsPath;

    public NotificationService()
    {
        _soundsPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
            "Sounds");

        // Create sounds directory if it doesn't exist
        if (!Directory.Exists(_soundsPath))
        {
            Directory.CreateDirectory(_soundsPath);
        }

        // Generate default sounds
        GenerateDefaultSounds();
    }

    public void PlayUserJoinSound()
    {
        PlaySound("join.wav");
    }

    public void PlayUserLeaveSound()
    {
        PlaySound("leave.wav");
    }

    public void PlayMessageSound()
    {
        PlaySound("message.wav");
    }

    public void PlayCallSound()
    {
        PlaySound("call.wav");
    }

    public void PlayMentionSound()
    {
        PlaySound("mention.wav");
    }

    public void PlayFriendRequestSound()
    {
        PlaySound("friend_request.wav");
    }

    public void StopAllSounds()
    {
        _mediaPlayer?.Stop();
    }

    private void PlaySound(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_soundsPath, fileName);
            if (!File.Exists(filePath))
            {
                // Try playing system sound as fallback
                SystemSounds.Asterisk.Play();
                return;
            }

            // Use MediaPlayer for better audio control
            _mediaPlayer?.Stop();
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Open(new Uri(filePath));
            _mediaPlayer.Volume = 0.5;
            _mediaPlayer.Play();
        }
        catch
        {
            // Silently fail if sound can't be played
        }
    }

    private void GenerateDefaultSounds()
    {
        // Create simple WAV files programmatically for the chime sounds
        // These are short sine wave tones

        var joinPath = Path.Combine(_soundsPath, "join.wav");
        if (!File.Exists(joinPath))
        {
            // Chinese-style chime - ascending two notes
            CreateChimeWav(joinPath, new[] { 523.25, 659.25 }, 0.15); // C5 to E5
        }

        var leavePath = Path.Combine(_soundsPath, "leave.wav");
        if (!File.Exists(leavePath))
        {
            // Descending two notes
            CreateChimeWav(leavePath, new[] { 659.25, 523.25 }, 0.15); // E5 to C5
        }

        var messagePath = Path.Combine(_soundsPath, "message.wav");
        if (!File.Exists(messagePath))
        {
            // Quick single note
            CreateChimeWav(messagePath, new[] { 880.0 }, 0.1); // A5
        }

        var callPath = Path.Combine(_soundsPath, "call.wav");
        if (!File.Exists(callPath))
        {
            // Ringtone-like pattern
            CreateChimeWav(callPath, new[] { 440.0, 554.37, 659.25, 554.37 }, 0.2); // A4-C#5-E5-C#5
        }

        var mentionPath = Path.Combine(_soundsPath, "mention.wav");
        if (!File.Exists(mentionPath))
        {
            // Higher pitched alert - two quick notes
            CreateChimeWav(mentionPath, new[] { 987.77, 1174.66 }, 0.08); // B5 to D6
        }

        var friendRequestPath = Path.Combine(_soundsPath, "friend_request.wav");
        if (!File.Exists(friendRequestPath))
        {
            // Friendly ascending pattern
            CreateChimeWav(friendRequestPath, new[] { 523.25, 659.25, 783.99 }, 0.12); // C5-E5-G5
        }
    }

    private static void CreateChimeWav(string filePath, double[] frequencies, double noteDuration)
    {
        const int sampleRate = 44100;
        const int bitsPerSample = 16;
        const int channels = 1;

        var totalSamples = (int)(sampleRate * noteDuration * frequencies.Length);
        var samplesPerNote = (int)(sampleRate * noteDuration);

        using var stream = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        // WAV header
        writer.Write(new char[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + totalSamples * 2); // File size
        writer.Write(new char[] { 'W', 'A', 'V', 'E' });

        // Format chunk
        writer.Write(new char[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // Chunk size
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8); // Byte rate
        writer.Write((short)(channels * bitsPerSample / 8)); // Block align
        writer.Write((short)bitsPerSample);

        // Data chunk
        writer.Write(new char[] { 'd', 'a', 't', 'a' });
        writer.Write(totalSamples * 2);

        // Generate samples
        for (int noteIndex = 0; noteIndex < frequencies.Length; noteIndex++)
        {
            var frequency = frequencies[noteIndex];
            for (int i = 0; i < samplesPerNote; i++)
            {
                var t = (double)i / sampleRate;

                // Sine wave with envelope (fade in/out)
                var envelope = 1.0;
                var fadeLength = samplesPerNote / 10;
                if (i < fadeLength)
                    envelope = (double)i / fadeLength;
                else if (i > samplesPerNote - fadeLength)
                    envelope = (double)(samplesPerNote - i) / fadeLength;

                var sample = Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.5;
                var intSample = (short)(sample * short.MaxValue);
                writer.Write(intSample);
            }
        }
    }
}
