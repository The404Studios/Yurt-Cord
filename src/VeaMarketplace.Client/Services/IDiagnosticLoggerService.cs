using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public interface IDiagnosticLoggerService
{
    LogLevel MinimumLevel { get; set; }
    void Log(LogLevel level, string category, string message, Exception? exception = null, Dictionary<string, object>? properties = null);
    void Trace(string category, string message);
    void Debug(string category, string message);
    void Info(string category, string message);
    void Warning(string category, string message, Exception? exception = null);
    void Error(string category, string message, Exception? exception = null);
    void Critical(string category, string message, Exception exception);
    Task<List<LogEntry>> GetRecentLogsAsync(int count = 100);
    Task<bool> ExportLogsAsync(string filePath);
    Task ClearLogsAsync();
}

public class DiagnosticLoggerService : IDiagnosticLoggerService
{
    private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _logFilePath;

    private const int MaxBufferSize = 10000;
    private const int FlushThreshold = 100;

    private int _logCount;

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public DiagnosticLoggerService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YurtCord",
            "Logs"
        );

        Directory.CreateDirectory(appDataPath);

        var logFileName = $"diagnostic_{DateTime.Now:yyyy-MM-dd}.log";
        _logFilePath = Path.Combine(appDataPath, logFileName);

        // Start background flushing
        _ = StartBackgroundFlushAsync();
    }

    public void Log(LogLevel level, string category, string message, Exception? exception = null, Dictionary<string, object>? properties = null)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Exception = exception,
            Properties = properties
        };

        _logBuffer.Enqueue(entry);
        Interlocked.Increment(ref _logCount);

        // Trim buffer if too large
        while (_logBuffer.Count > MaxBufferSize)
        {
            _logBuffer.TryDequeue(out _);
        }

        // Write to Debug output immediately
        var debugMessage = FormatLogEntry(entry);
        System.Diagnostics.Debug.WriteLine(debugMessage);

        // Trigger flush if threshold reached
        if (_logCount >= FlushThreshold)
        {
            _ = FlushLogsAsync();
        }
    }

    public void Trace(string category, string message)
    {
        Log(LogLevel.Trace, category, message);
    }

    public void Debug(string category, string message)
    {
        Log(LogLevel.Debug, category, message);
    }

    public void Info(string category, string message)
    {
        Log(LogLevel.Info, category, message);
    }

    public void Warning(string category, string message, Exception? exception = null)
    {
        Log(LogLevel.Warning, category, message, exception);
    }

    public void Error(string category, string message, Exception? exception = null)
    {
        Log(LogLevel.Error, category, message, exception);
    }

    public void Critical(string category, string message, Exception exception)
    {
        Log(LogLevel.Critical, category, message, exception);
    }

    public async Task<List<LogEntry>> GetRecentLogsAsync(int count = 100)
    {
        await Task.CompletedTask;

        return _logBuffer
            .Reverse()
            .Take(count)
            .Reverse()
            .ToList();
    }

    public async Task<bool> ExportLogsAsync(string filePath)
    {
        try
        {
            var logs = await GetRecentLogsAsync(_logBuffer.Count);
            var sb = new StringBuilder();

            sb.AppendLine("=== Yurt Cord Diagnostic Logs ===");
            sb.AppendLine($"Exported: {DateTime.Now}");
            sb.AppendLine($"Total Entries: {logs.Count}");
            sb.AppendLine();

            foreach (var entry in logs)
            {
                sb.AppendLine(FormatLogEntry(entry, includeDetails: true));
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());

            System.Diagnostics.Debug.WriteLine($"Logs exported to: {filePath}");

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to export logs: {ex.Message}");
            return false;
        }
    }

    public async Task ClearLogsAsync()
    {
        _logBuffer.Clear();
        Interlocked.Exchange(ref _logCount, 0);

        await Task.CompletedTask;

        System.Diagnostics.Debug.WriteLine("Diagnostic logs cleared");
    }

    private async Task FlushLogsAsync()
    {
        if (_logBuffer.IsEmpty)
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            var logsToFlush = new List<LogEntry>();

            while (_logBuffer.TryDequeue(out var entry))
            {
                logsToFlush.Add(entry);
            }

            if (logsToFlush.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();

            foreach (var entry in logsToFlush)
            {
                sb.AppendLine(FormatLogEntry(entry, includeDetails: true));
            }

            await File.AppendAllTextAsync(_logFilePath, sb.ToString());

            Interlocked.Exchange(ref _logCount, 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to flush logs: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task StartBackgroundFlushAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                await FlushLogsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background flush error: {ex.Message}");
            }
        }
    }

    private static string FormatLogEntry(LogEntry entry, bool includeDetails = false)
    {
        var sb = new StringBuilder();

        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{entry.Level,-8}] ");
        sb.Append($"[{entry.Category}] ");
        sb.Append(entry.Message);

        if (includeDetails)
        {
            if (entry.Properties != null && entry.Properties.Count > 0)
            {
                sb.AppendLine();
                sb.Append("  Properties: ");
                sb.Append(string.Join(", ", entry.Properties.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append("  Exception: ");
                sb.Append(entry.Exception.ToString());
            }
        }

        return sb.ToString();
    }
}
