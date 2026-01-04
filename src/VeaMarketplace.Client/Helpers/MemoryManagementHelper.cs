using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides memory management utilities and monitoring
/// </summary>
public static class MemoryManagementHelper
{
    private static Timer? _monitoringTimer;
    private static long _lastWorkingSet;
    private static long _peakWorkingSet;

    public static event Action<MemoryStats>? OnMemoryStatsChanged;

    /// <summary>
    /// Current memory statistics
    /// </summary>
    public class MemoryStats
    {
        public long WorkingSetBytes { get; set; }
        public long PrivateMemoryBytes { get; set; }
        public long ManagedMemoryBytes { get; set; }
        public long PeakWorkingSetBytes { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double MemoryPressure { get; set; }
    }

    /// <summary>
    /// Gets current memory statistics
    /// </summary>
    public static MemoryStats GetMemoryStats()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var workingSet = process.WorkingSet64;
        _peakWorkingSet = Math.Max(_peakWorkingSet, workingSet);

        var gcMemory = GC.GetTotalMemory(false);

        return new MemoryStats
        {
            WorkingSetBytes = workingSet,
            PrivateMemoryBytes = process.PrivateMemorySize64,
            ManagedMemoryBytes = gcMemory,
            PeakWorkingSetBytes = _peakWorkingSet,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            MemoryPressure = CalculateMemoryPressure(workingSet)
        };
    }

    /// <summary>
    /// Forces garbage collection (use sparingly!)
    /// </summary>
    public static void ForceGarbageCollection()
    {
        Debug.WriteLine("Forcing garbage collection...");

        var before = GC.GetTotalMemory(false);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var after = GC.GetTotalMemory(false);
        var freed = before - after;

        Debug.WriteLine($"GC completed. Freed: {FormatBytes(freed)}");
    }

    /// <summary>
    /// Attempts to reduce memory footprint
    /// </summary>
    public static void OptimizeMemory()
    {
        Debug.WriteLine("Optimizing memory usage...");

        // Compact the Large Object Heap
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

        // Force GC
        ForceGarbageCollection();

        // Clear unused memory
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                NativeMethods.EmptyWorkingSet(process.Handle);
                Debug.WriteLine("Working set emptied");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to empty working set: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Starts monitoring memory usage
    /// </summary>
    public static void StartMonitoring(TimeSpan interval)
    {
        StopMonitoring();

        _monitoringTimer = new Timer(
            _ =>
            {
                var stats = GetMemoryStats();
                var delta = stats.WorkingSetBytes - _lastWorkingSet;
                _lastWorkingSet = stats.WorkingSetBytes;

                Debug.WriteLine($"Memory: {FormatBytes(stats.WorkingSetBytes)} " +
                              $"(Managed: {FormatBytes(stats.ManagedMemoryBytes)}) " +
                              $"Delta: {(delta >= 0 ? "+" : "")}{FormatBytes(delta)} " +
                              $"Pressure: {stats.MemoryPressure:F1}%");

                OnMemoryStatsChanged?.Invoke(stats);

                // Auto-optimize if memory pressure is high
                if (stats.MemoryPressure > 80)
                {
                    Debug.WriteLine("High memory pressure detected, optimizing...");
                    OptimizeMemory();
                }
            },
            null,
            TimeSpan.Zero,
            interval
        );

        Debug.WriteLine($"Memory monitoring started (interval: {interval.TotalSeconds}s)");
    }

    /// <summary>
    /// Stops memory monitoring
    /// </summary>
    public static void StopMonitoring()
    {
        _monitoringTimer?.Dispose();
        _monitoringTimer = null;
        Debug.WriteLine("Memory monitoring stopped");
    }

    /// <summary>
    /// Calculates memory pressure (0-100%)
    /// </summary>
    private static double CalculateMemoryPressure(long workingSetBytes)
    {
        try
        {
            using var performanceCounter = new PerformanceCounter("Memory", "Available MBytes");
            var availableMB = performanceCounter.NextValue();
            var usedMB = workingSetBytes / (1024.0 * 1024.0);
            var totalMB = usedMB + availableMB;

            if (totalMB == 0)
                return 0;

            return (usedMB / totalMB) * 100;
        }
        catch
        {
            // Fallback if performance counter fails
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (totalMemory == 0)
                return 0;

            return ((double)workingSetBytes / totalMemory) * 100;
        }
    }

    /// <summary>
    /// Formats byte count for display
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = Math.Abs(bytes);
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        var sign = bytes < 0 ? "-" : "";
        return $"{sign}{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Checks if the application is approaching memory limits
    /// </summary>
    public static bool IsMemoryConstrained()
    {
        var stats = GetMemoryStats();
        return stats.MemoryPressure > 75;
    }

    /// <summary>
    /// Provides recommendations based on current memory usage
    /// </summary>
    public static string GetMemoryRecommendation()
    {
        var stats = GetMemoryStats();

        if (stats.MemoryPressure < 50)
            return "Memory usage is healthy";

        if (stats.MemoryPressure < 70)
            return "Memory usage is moderate";

        if (stats.MemoryPressure < 85)
            return "Memory usage is high. Consider closing unused features.";

        return "Memory usage is critical. Optimize or restart the application.";
    }

    /// <summary>
    /// Native methods for Windows-specific memory management
    /// </summary>
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);
    }
}
