using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for generating a unique Hardware ID (HWID) for the current machine.
/// The HWID is a SHA256 hash of various hardware identifiers combined.
/// </summary>
public class HwidService
{
    private static string? _cachedHwid;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the unique Hardware ID for this machine.
    /// The ID is cached after first generation.
    /// </summary>
    public string GetHwid()
    {
        if (_cachedHwid != null)
            return _cachedHwid;

        lock (_lock)
        {
            if (_cachedHwid != null)
                return _cachedHwid;

            _cachedHwid = GenerateHwid();
            return _cachedHwid;
        }
    }

    /// <summary>
    /// Generates a unique hardware fingerprint based on multiple hardware identifiers.
    /// </summary>
    private string GenerateHwid()
    {
        var components = new StringBuilder();

        // CPU ID
        components.Append(GetWmiValue("Win32_Processor", "ProcessorId"));
        components.Append("|");

        // Motherboard Serial
        components.Append(GetWmiValue("Win32_BaseBoard", "SerialNumber"));
        components.Append("|");

        // BIOS Serial
        components.Append(GetWmiValue("Win32_BIOS", "SerialNumber"));
        components.Append("|");

        // Primary disk serial (C: drive)
        components.Append(GetDiskSerial());
        components.Append("|");

        // Machine GUID from registry (Windows installation specific)
        components.Append(GetMachineGuid());

        // Hash the combined components
        var rawHwid = components.ToString();
        return ComputeHash(rawHwid);
    }

    /// <summary>
    /// Gets a value from WMI
    /// </summary>
    private string GetWmiValue(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                var value = obj[property]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value) && value != "To Be Filled By O.E.M.")
                    return value;
            }
        }
        catch
        {
            // WMI query failed, continue with other identifiers
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets the serial number of the primary disk
    /// </summary>
    private string GetDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0");
            foreach (var obj in searcher.Get())
            {
                var serial = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(serial))
                    return serial;
            }
        }
        catch
        {
            // Disk query failed
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets the Windows Machine GUID from registry
    /// </summary>
    private string GetMachineGuid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid")?.ToString();
            return guid ?? string.Empty;
        }
        catch
        {
            // Registry access failed
        }
        return string.Empty;
    }

    /// <summary>
    /// Computes SHA256 hash of the input string
    /// </summary>
    private string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        // Convert to hex string, take first 32 characters for a shorter ID
        var fullHash = Convert.ToHexString(hash);
        return fullHash[..32]; // 32 hex chars = 128 bits, still very unique
    }

    /// <summary>
    /// Gets a display-friendly version of the HWID (first 8 chars with dashes)
    /// </summary>
    public string GetDisplayHwid()
    {
        var hwid = GetHwid();
        if (hwid.Length >= 16)
        {
            return $"{hwid[..4]}-{hwid[4..8]}-{hwid[8..12]}-{hwid[12..16]}";
        }
        return hwid;
    }
}
