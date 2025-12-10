using NAudio.Wave;

namespace VeaMarketplace.Client.Services;

public interface IAudioDeviceService
{
    List<AudioDevice> GetInputDevices();
    List<AudioDevice> GetOutputDevices();
    AudioDevice? GetDefaultInputDevice();
    AudioDevice? GetDefaultOutputDevice();
    event Action? OnDevicesChanged;
}

public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int DeviceNumber { get; set; }
}

public class AudioDeviceService : IAudioDeviceService
{
    public event Action? OnDevicesChanged;

    public List<AudioDevice> GetInputDevices()
    {
        var devices = new List<AudioDevice>();

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var capabilities = WaveInEvent.GetCapabilities(i);
            devices.Add(new AudioDevice
            {
                Id = $"input_{i}",
                Name = capabilities.ProductName,
                IsDefault = i == 0,
                DeviceNumber = i
            });
        }

        return devices;
    }

    public List<AudioDevice> GetOutputDevices()
    {
        var devices = new List<AudioDevice>();

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDevice
            {
                Id = $"output_{i}",
                Name = capabilities.ProductName,
                IsDefault = i == 0,
                DeviceNumber = i
            });
        }

        return devices;
    }

    public AudioDevice? GetDefaultInputDevice()
    {
        var devices = GetInputDevices();
        return devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
    }

    public AudioDevice? GetDefaultOutputDevice()
    {
        var devices = GetOutputDevices();
        return devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
    }

    public void RefreshDevices()
    {
        OnDevicesChanged?.Invoke();
    }
}
