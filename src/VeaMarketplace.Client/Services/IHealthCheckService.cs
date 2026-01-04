using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Health check status
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public Exception? Exception { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Overall health report
/// </summary>
public class HealthReport
{
    public HealthStatus OverallStatus { get; set; }
    public Dictionary<string, HealthCheckResult> Entries { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health check interface
/// </summary>
public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public interface IHealthCheckService
{
    void RegisterHealthCheck(IHealthCheck healthCheck);
    void UnregisterHealthCheck(string name);
    Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task<HealthCheckResult?> CheckHealthAsync(string name, CancellationToken cancellationToken = default);
    Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default);
    void StopMonitoring();
    event Action<HealthReport>? OnHealthReportGenerated;
}

public class HealthCheckService : IHealthCheckService
{
    private readonly Dictionary<string, IHealthCheck> _healthChecks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;

    public event Action<HealthReport>? OnHealthReportGenerated;

    public void RegisterHealthCheck(IHealthCheck healthCheck)
    {
        _lock.Wait();
        try
        {
            _healthChecks[healthCheck.Name] = healthCheck;
            Debug.WriteLine($"Health check registered: {healthCheck.Name}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void UnregisterHealthCheck(string name)
    {
        _lock.Wait();
        try
        {
            if (_healthChecks.Remove(name))
            {
                Debug.WriteLine($"Health check unregistered: {name}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var report = new HealthReport();

        IHealthCheck[] healthChecks;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            healthChecks = _healthChecks.Values.ToArray();
        }
        finally
        {
            _lock.Release();
        }

        var tasks = healthChecks.Select(async check =>
        {
            try
            {
                var result = await check.CheckHealthAsync(cancellationToken);
                return (check.Name, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Health check '{check.Name}' failed with exception: {ex.Message}");

                return (check.Name, new HealthCheckResult
                {
                    Name = check.Name,
                    Status = HealthStatus.Unhealthy,
                    Description = "Health check threw an exception",
                    Exception = ex,
                    CheckedAt = DateTime.UtcNow
                });
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (name, result) in results)
        {
            report.Entries[name] = result;
        }

        overallStopwatch.Stop();
        report.TotalDuration = overallStopwatch.Elapsed;
        report.OverallStatus = CalculateOverallStatus(report.Entries.Values);

        OnHealthReportGenerated?.Invoke(report);

        return report;
    }

    public async Task<HealthCheckResult?> CheckHealthAsync(string name, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        IHealthCheck? healthCheck;
        try
        {
            _healthChecks.TryGetValue(name, out healthCheck);
        }
        finally
        {
            _lock.Release();
        }

        if (healthCheck == null)
        {
            return null;
        }

        try
        {
            return await healthCheck.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Name = name,
                Status = HealthStatus.Unhealthy,
                Description = "Health check threw an exception",
                Exception = ex,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    public async Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        StopMonitoring();

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = MonitorHealthAsync(interval, _monitoringCts.Token);

        await Task.CompletedTask;
    }

    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private async Task MonitorHealthAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"Health monitoring started (interval: {interval.TotalSeconds}s)");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var report = await CheckHealthAsync(cancellationToken);

                Debug.WriteLine($"Health check completed: {report.OverallStatus} ({report.Entries.Count} checks, {report.TotalDuration.TotalMilliseconds:F0}ms)");

                if (report.OverallStatus != HealthStatus.Healthy)
                {
                    var unhealthyChecks = report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy).ToList();
                    foreach (var entry in unhealthyChecks)
                    {
                        Debug.WriteLine($"  {entry.Key}: {entry.Value.Status} - {entry.Value.Description}");
                    }
                }

                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during health monitoring: {ex.Message}");
                await Task.Delay(interval, cancellationToken);
            }
        }

        Debug.WriteLine("Health monitoring stopped");
    }

    private static HealthStatus CalculateOverallStatus(IEnumerable<HealthCheckResult> results)
    {
        var resultsList = results.ToList();

        if (!resultsList.Any())
            return HealthStatus.Healthy;

        if (resultsList.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (resultsList.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }
}

/// <summary>
/// Built-in health checks
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _maxMemoryBytes;

    public string Name => "Memory";

    public MemoryHealthCheck(long maxMemoryBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _maxMemoryBytes = maxMemoryBytes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var stopwatch = Stopwatch.StartNew();

        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var workingSet = process.WorkingSet64;
        var usagePercent = (workingSet / (double)_maxMemoryBytes) * 100;

        stopwatch.Stop();

        var status = usagePercent switch
        {
            < 70 => HealthStatus.Healthy,
            < 85 => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy
        };

        return new HealthCheckResult
        {
            Name = Name,
            Status = status,
            Description = $"Memory usage: {workingSet / (1024 * 1024)}MB ({usagePercent:F1}%)",
            Duration = stopwatch.Elapsed,
            Data = new Dictionary<string, object>
            {
                ["workingSetBytes"] = workingSet,
                ["maxMemoryBytes"] = _maxMemoryBytes,
                ["usagePercent"] = usagePercent
            }
        };
    }
}

public class NetworkHealthCheck : IHealthCheck
{
    private readonly string _serverHost;

    public string Name => "Network";

    public NetworkHealthCheck(string serverHost)
    {
        _serverHost = serverHost;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(_serverHost, 5000);

            stopwatch.Stop();

            var status = reply.Status == System.Net.NetworkInformation.IPStatus.Success
                ? (reply.RoundtripTime < 200 ? HealthStatus.Healthy : HealthStatus.Degraded)
                : HealthStatus.Unhealthy;

            return new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Description = $"Ping to {_serverHost}: {reply.RoundtripTime}ms",
                Duration = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    ["host"] = _serverHost,
                    ["roundtripTime"] = reply.RoundtripTime,
                    ["status"] = reply.Status.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthCheckResult
            {
                Name = Name,
                Status = HealthStatus.Unhealthy,
                Description = $"Failed to ping {_serverHost}: {ex.Message}",
                Duration = stopwatch.Elapsed,
                Exception = ex
            };
        }
    }
}

public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly string _drivePath;
    private readonly long _minFreeBytes;

    public string Name => "DiskSpace";

    public DiskSpaceHealthCheck(string drivePath, long minFreeBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _drivePath = drivePath;
        _minFreeBytes = minFreeBytes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var drive = new System.IO.DriveInfo(_drivePath);
            var freeSpace = drive.AvailableFreeSpace;
            var totalSpace = drive.TotalSize;
            var usedPercent = ((totalSpace - freeSpace) / (double)totalSpace) * 100;

            stopwatch.Stop();

            var status = freeSpace >= _minFreeBytes
                ? (usedPercent < 85 ? HealthStatus.Healthy : HealthStatus.Degraded)
                : HealthStatus.Unhealthy;

            return new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Description = $"Free space on {_drivePath}: {freeSpace / (1024 * 1024 * 1024)}GB ({100 - usedPercent:F1}% free)",
                Duration = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    ["drivePath"] = _drivePath,
                    ["freeSpaceBytes"] = freeSpace,
                    ["totalSpaceBytes"] = totalSpace,
                    ["usedPercent"] = usedPercent
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthCheckResult
            {
                Name = Name,
                Status = HealthStatus.Unhealthy,
                Description = $"Failed to check disk space on {_drivePath}: {ex.Message}",
                Duration = stopwatch.Elapsed,
                Exception = ex
            };
        }
    }
}
