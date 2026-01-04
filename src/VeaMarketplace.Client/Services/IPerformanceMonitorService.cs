using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VeaMarketplace.Client.Services;

public interface IPerformanceMonitorService
{
    void RecordMetric(string metricName, double value);
    void StartTimer(string operationName);
    void StopTimer(string operationName);
    PerformanceStats GetStats(string metricName);
    Dictionary<string, PerformanceStats> GetAllStats();
    void Reset();
}

public class PerformanceStats
{
    public double Average { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public int Count { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
}

public class PerformanceMonitorService : IPerformanceMonitorService
{
    private readonly Dictionary<string, List<double>> _metrics = new();
    private readonly Dictionary<string, Stopwatch> _timers = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public void RecordMetric(string metricName, double value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_metrics.ContainsKey(metricName))
            {
                _metrics[metricName] = new List<double>();
            }

            _metrics[metricName].Add(value);

            // Keep only last 1000 values to prevent memory bloat
            if (_metrics[metricName].Count > 1000)
            {
                _metrics[metricName].RemoveAt(0);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void StartTimer(string operationName)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_timers.ContainsKey(operationName))
            {
                _timers[operationName] = new Stopwatch();
            }

            _timers[operationName].Restart();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void StopTimer(string operationName)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_timers.ContainsKey(operationName))
            {
                _lock.EnterWriteLock();
                try
                {
                    _timers[operationName].Stop();
                    RecordMetric(operationName, _timers[operationName].Elapsed.TotalMilliseconds);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public PerformanceStats GetStats(string metricName)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_metrics.ContainsKey(metricName) || _metrics[metricName].Count == 0)
            {
                return new PerformanceStats();
            }

            var values = _metrics[metricName].ToList();
            var sorted = values.OrderBy(v => v).ToList();

            return new PerformanceStats
            {
                Average = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                Count = values.Count,
                P50 = GetPercentile(sorted, 0.50),
                P95 = GetPercentile(sorted, 0.95),
                P99 = GetPercentile(sorted, 0.99)
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Dictionary<string, PerformanceStats> GetAllStats()
    {
        _lock.EnterReadLock();
        try
        {
            var result = new Dictionary<string, PerformanceStats>();

            foreach (var metric in _metrics.Keys)
            {
                result[metric] = GetStats(metric);
            }

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Reset()
    {
        _lock.EnterWriteLock();
        try
        {
            _metrics.Clear();
            _timers.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));

        return sortedValues[index];
    }
}
