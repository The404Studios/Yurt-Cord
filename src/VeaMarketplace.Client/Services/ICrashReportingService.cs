using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Crash report severity
/// </summary>
public enum CrashSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Crash report data
/// </summary>
public class CrashReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CrashSeverity Severity { get; set; }
    public string? ExceptionType { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public string? InnerExceptions { get; set; }
    public Dictionary<string, string>? SystemInfo { get; set; }
    public Dictionary<string, string>? CustomData { get; set; }
    public string? UserDescription { get; set; }
    public bool IsHandled { get; set; }
}

public interface ICrashReportingService : IDisposable
{
    void Initialize();
    void ReportCrash(Exception exception, CrashSeverity severity = CrashSeverity.High, Dictionary<string, string>? customData = null);
    void ReportError(string message, Exception? exception = null, Dictionary<string, string>? customData = null);
    Task<bool> SaveCrashReportAsync(CrashReport report);
    Task<List<CrashReport>> GetCrashReportsAsync();
    Task<bool> ClearCrashReportsAsync();
    event Action<CrashReport>? OnCrashReported;
}

public class CrashReportingService : ICrashReportingService
{
    private readonly string _crashReportsPath;
    private bool _isInitialized;
    private bool _disposed = false;

    public event Action<CrashReport>? OnCrashReported;

    public CrashReportingService()
    {
        // Use XDG-compliant state directory for crash reports (cross-platform support)
        var appDataPath = Path.Combine(Helpers.XdgDirectories.StateHome, "CrashReports");

        Directory.CreateDirectory(appDataPath);
        _crashReportsPath = appDataPath;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        // Hook into unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _isInitialized = true;

        Debug.WriteLine("Crash reporting service initialized");
    }

    public void ReportCrash(Exception exception, CrashSeverity severity = CrashSeverity.High, Dictionary<string, string>? customData = null)
    {
        try
        {
            var report = CreateCrashReport(exception, severity, customData, isHandled: true);

            _ = SaveCrashReportAsync(report);

            Debug.WriteLine($"Crash reported: {exception.GetType().Name} - {exception.Message}");

            OnCrashReported?.Invoke(report);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to report crash: {ex.Message}");
        }
    }

    public void ReportError(string message, Exception? exception = null, Dictionary<string, string>? customData = null)
    {
        try
        {
            var report = new CrashReport
            {
                Timestamp = DateTime.UtcNow,
                Severity = CrashSeverity.Medium,
                Message = message,
                ExceptionType = exception?.GetType().Name,
                StackTrace = exception?.StackTrace,
                InnerExceptions = GetInnerExceptionMessages(exception),
                SystemInfo = CollectSystemInfo(),
                CustomData = customData,
                IsHandled = true
            };

            _ = SaveCrashReportAsync(report);

            Debug.WriteLine($"Error reported: {message}");

            OnCrashReported?.Invoke(report);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to report error: {ex.Message}");
        }
    }

    public async Task<bool> SaveCrashReportAsync(CrashReport report)
    {
        try
        {
            var fileName = $"crash_{report.Timestamp:yyyyMMdd_HHmmss}_{report.Id}.json";
            var filePath = Path.Combine(_crashReportsPath, fileName);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);

            Debug.WriteLine($"Crash report saved: {filePath}");

            // Also save a human-readable version
            var readableFilePath = Path.Combine(_crashReportsPath, $"crash_{report.Timestamp:yyyyMMdd_HHmmss}_{report.Id}.txt");
            await File.WriteAllTextAsync(readableFilePath, FormatCrashReport(report));

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save crash report: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CrashReport>> GetCrashReportsAsync()
    {
        var reports = new List<CrashReport>();

        try
        {
            var files = Directory.GetFiles(_crashReportsPath, "crash_*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<CrashReport>(json);

                    if (report != null)
                    {
                        reports.Add(report);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load crash report {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get crash reports: {ex.Message}");
        }

        return reports;
    }

    public async Task<bool> ClearCrashReportsAsync()
    {
        try
        {
            var files = Directory.GetFiles(_crashReportsPath, "crash_*.*");

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete crash report {file}: {ex.Message}");
                }
            }

            Debug.WriteLine($"Cleared {files.Length} crash reports");

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear crash reports: {ex.Message}");
            return false;
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            var report = CreateCrashReport(exception, CrashSeverity.Critical, null, isHandled: false);
            _ = SaveCrashReportAsync(report);

            Debug.WriteLine($"Unhandled exception: {exception.Message}");

            OnCrashReported?.Invoke(report);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var report = CreateCrashReport(e.Exception, CrashSeverity.Critical, null, isHandled: false);
        _ = SaveCrashReportAsync(report);

        Debug.WriteLine($"Dispatcher unhandled exception: {e.Exception.Message}");

        OnCrashReported?.Invoke(report);

        // Mark as handled to prevent app crash
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var report = CreateCrashReport(e.Exception, CrashSeverity.High, null, isHandled: false);
        _ = SaveCrashReportAsync(report);

        Debug.WriteLine($"Unobserved task exception: {e.Exception.Message}");

        OnCrashReported?.Invoke(report);

        // Mark as observed to prevent app crash
        e.SetObserved();
    }

    private CrashReport CreateCrashReport(Exception exception, CrashSeverity severity, Dictionary<string, string>? customData, bool isHandled)
    {
        return new CrashReport
        {
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            InnerExceptions = GetInnerExceptionMessages(exception),
            SystemInfo = CollectSystemInfo(),
            CustomData = customData,
            IsHandled = isHandled
        };
    }

    private static Dictionary<string, string> CollectSystemInfo()
    {
        var info = new Dictionary<string, string>();

        try
        {
            info["OS"] = Environment.OSVersion.ToString();
            info["OSVersion"] = Environment.OSVersion.VersionString;
            info["64BitOS"] = Environment.Is64BitOperatingSystem.ToString();
            info["64BitProcess"] = Environment.Is64BitProcess.ToString();
            info["ProcessorCount"] = Environment.ProcessorCount.ToString();
            info["MachineName"] = Environment.MachineName;
            info["UserName"] = Environment.UserName;
            info["CLRVersion"] = Environment.Version.ToString();
            info["WorkingSet"] = $"{Environment.WorkingSet / (1024 * 1024)} MB";

            var process = Process.GetCurrentProcess();
            info["ProcessMemory"] = $"{process.WorkingSet64 / (1024 * 1024)} MB";
            info["ThreadCount"] = process.Threads.Count.ToString();

            var assembly = Assembly.GetExecutingAssembly();
            info["AppVersion"] = assembly.GetName().Version?.ToString() ?? "Unknown";
            info["AppName"] = assembly.GetName().Name ?? "Unknown";
        }
        catch (Exception ex)
        {
            info["SystemInfoError"] = ex.Message;
        }

        return info;
    }

    private static string GetInnerExceptionMessages(Exception? exception)
    {
        if (exception == null)
            return string.Empty;

        var sb = new StringBuilder();
        var current = exception.InnerException;
        int depth = 1;

        while (current != null && depth <= 5) // Limit depth to prevent excessive data
        {
            sb.AppendLine($"Inner Exception {depth}: {current.GetType().Name}");
            sb.AppendLine($"  Message: {current.Message}");
            sb.AppendLine($"  StackTrace: {current.StackTrace}");
            sb.AppendLine();

            current = current.InnerException;
            depth++;
        }

        return sb.ToString();
    }

    private static string FormatCrashReport(CrashReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("               YURT CORD CRASH REPORT                  ");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine();

        sb.AppendLine($"Report ID: {report.Id}");
        sb.AppendLine($"Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"Severity: {report.Severity}");
        sb.AppendLine($"Handled: {report.IsHandled}");
        sb.AppendLine();

        sb.AppendLine("─── Exception Information ───────────────────────────────");
        sb.AppendLine($"Type: {report.ExceptionType}");
        sb.AppendLine($"Message: {report.Message}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(report.StackTrace))
        {
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(report.StackTrace);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(report.InnerExceptions))
        {
            sb.AppendLine("─── Inner Exceptions ────────────────────────────────────");
            sb.AppendLine(report.InnerExceptions);
        }

        if (report.SystemInfo != null && report.SystemInfo.Count > 0)
        {
            sb.AppendLine("─── System Information ──────────────────────────────────");
            foreach (var kvp in report.SystemInfo)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        if (report.CustomData != null && report.CustomData.Count > 0)
        {
            sb.AppendLine("─── Custom Data ─────────────────────────────────────────");
            foreach (var kvp in report.CustomData)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(report.UserDescription))
        {
            sb.AppendLine("─── User Description ────────────────────────────────────");
            sb.AppendLine(report.UserDescription);
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════");

        return sb.ToString();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Unsubscribe from events to prevent memory leaks
                if (_isInitialized)
                {
                    try
                    {
                        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                        System.Windows.Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
                        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error unsubscribing from events: {ex.Message}");
                    }
                }

                Debug.WriteLine("Crash reporting service disposed");
            }

            _disposed = true;
        }
    }
}
