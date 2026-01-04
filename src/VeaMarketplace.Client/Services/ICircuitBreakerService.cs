using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Circuit breaker state
/// </summary>
public enum CircuitBreakerState
{
    Closed,    // Normal operation
    Open,      // Failures detected, reject requests
    HalfOpen   // Testing if service recovered
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int SuccessThreshold { get; set; } = 2;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Circuit breaker statistics
/// </summary>
public class CircuitBreakerStats
{
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public DateTime? StateChangedAt { get; set; }
    public TimeSpan? TimeUntilRetry { get; set; }
}

public interface ICircuitBreakerService
{
    Task<T> ExecuteAsync<T>(string circuitName, Func<Task<T>> operation, CircuitBreakerConfig? config = null);
    Task ExecuteAsync(string circuitName, Func<Task> operation, CircuitBreakerConfig? config = null);
    CircuitBreakerStats GetStats(string circuitName);
    void Reset(string circuitName);
    void Trip(string circuitName);
}

public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> _circuits = new();
    private readonly CircuitBreakerConfig _defaultConfig;

    public CircuitBreakerService(CircuitBreakerConfig? defaultConfig = null)
    {
        _defaultConfig = defaultConfig ?? new CircuitBreakerConfig();
    }

    public async Task<T> ExecuteAsync<T>(string circuitName, Func<Task<T>> operation, CircuitBreakerConfig? config = null)
    {
        var circuit = _circuits.GetOrAdd(circuitName, _ => new CircuitBreaker(config ?? _defaultConfig, circuitName));
        return await circuit.ExecuteAsync(operation);
    }

    public async Task ExecuteAsync(string circuitName, Func<Task> operation, CircuitBreakerConfig? config = null)
    {
        await ExecuteAsync(circuitName, async () =>
        {
            await operation();
            return true;
        }, config);
    }

    public CircuitBreakerStats GetStats(string circuitName)
    {
        if (_circuits.TryGetValue(circuitName, out var circuit))
        {
            return circuit.GetStats();
        }

        return new CircuitBreakerStats { State = CircuitBreakerState.Closed };
    }

    public void Reset(string circuitName)
    {
        if (_circuits.TryGetValue(circuitName, out var circuit))
        {
            circuit.Reset();
            Debug.WriteLine($"Circuit breaker '{circuitName}' reset");
        }
    }

    public void Trip(string circuitName)
    {
        if (_circuits.TryGetValue(circuitName, out var circuit))
        {
            circuit.Trip();
            Debug.WriteLine($"Circuit breaker '{circuitName}' manually tripped");
        }
    }

    private class CircuitBreaker
    {
        private readonly CircuitBreakerConfig _config;
        private readonly string _name;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount;
        private int _successCount;
        private DateTime _lastFailureTime;
        private DateTime _stateChangedAt = DateTime.UtcNow;
        private readonly ConcurrentQueue<(DateTime timestamp, bool success)> _executionHistory = new();

        public CircuitBreaker(CircuitBreakerConfig config, string name)
        {
            _config = config;
            _name = name;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            await _lock.WaitAsync();
            try
            {
                var currentState = GetCurrentState();

                if (currentState == CircuitBreakerState.Open)
                {
                    var timeSinceOpen = DateTime.UtcNow - _stateChangedAt;
                    throw new CircuitBreakerOpenException(
                        $"Circuit breaker '{_name}' is open",
                        _config.OpenTimeout - timeSinceOpen
                    );
                }
            }
            finally
            {
                _lock.Release();
            }

            try
            {
                var result = await operation();
                await RecordSuccessAsync();
                return result;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);
                throw;
            }
        }

        public CircuitBreakerStats GetStats()
        {
            var currentState = GetCurrentState();
            TimeSpan? timeUntilRetry = null;

            if (currentState == CircuitBreakerState.Open)
            {
                var timeSinceOpen = DateTime.UtcNow - _stateChangedAt;
                timeUntilRetry = _config.OpenTimeout - timeSinceOpen;
                if (timeUntilRetry < TimeSpan.Zero)
                    timeUntilRetry = TimeSpan.Zero;
            }

            return new CircuitBreakerStats
            {
                State = currentState,
                FailureCount = _failureCount,
                SuccessCount = _successCount,
                LastFailureTime = _lastFailureTime != default ? _lastFailureTime : null,
                StateChangedAt = _stateChangedAt,
                TimeUntilRetry = timeUntilRetry
            };
        }

        public void Reset()
        {
            _lock.Wait();
            try
            {
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
                _successCount = 0;
                _stateChangedAt = DateTime.UtcNow;
                _executionHistory.Clear();

                Debug.WriteLine($"Circuit breaker '{_name}' reset to Closed state");
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Trip()
        {
            _lock.Wait();
            try
            {
                TransitionToOpen();
            }
            finally
            {
                _lock.Release();
            }
        }

        private CircuitBreakerState GetCurrentState()
        {
            _lock.Wait();
            try
            {
                CleanupHistory();

                if (_state == CircuitBreakerState.Open)
                {
                    var timeSinceOpen = DateTime.UtcNow - _stateChangedAt;
                    if (timeSinceOpen >= _config.OpenTimeout)
                    {
                        // Transition to half-open
                        _state = CircuitBreakerState.HalfOpen;
                        _stateChangedAt = DateTime.UtcNow;
                        _successCount = 0;
                        Debug.WriteLine($"Circuit breaker '{_name}' transitioned to HalfOpen state");
                    }
                }

                return _state;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task RecordSuccessAsync()
        {
            await Task.CompletedTask;

            _lock.Wait();
            try
            {
                _executionHistory.Enqueue((DateTime.UtcNow, true));
                _successCount++;

                if (_state == CircuitBreakerState.HalfOpen)
                {
                    if (_successCount >= _config.SuccessThreshold)
                    {
                        // Transition back to closed
                        _state = CircuitBreakerState.Closed;
                        _stateChangedAt = DateTime.UtcNow;
                        _failureCount = 0;
                        _successCount = 0;
                        Debug.WriteLine($"Circuit breaker '{_name}' transitioned to Closed state");
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task RecordFailureAsync(Exception ex)
        {
            await Task.CompletedTask;

            _lock.Wait();
            try
            {
                _executionHistory.Enqueue((DateTime.UtcNow, false));
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                Debug.WriteLine($"Circuit breaker '{_name}' recorded failure ({_failureCount}/{_config.FailureThreshold}): {ex.Message}");

                if (_state == CircuitBreakerState.HalfOpen)
                {
                    // Any failure in half-open state trips the breaker
                    TransitionToOpen();
                }
                else if (_state == CircuitBreakerState.Closed && _failureCount >= _config.FailureThreshold)
                {
                    TransitionToOpen();
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private void TransitionToOpen()
        {
            _state = CircuitBreakerState.Open;
            _stateChangedAt = DateTime.UtcNow;
            _successCount = 0;

            Debug.WriteLine($"Circuit breaker '{_name}' transitioned to Open state (failures: {_failureCount})");
        }

        private void CleanupHistory()
        {
            var cutoff = DateTime.UtcNow - _config.SamplingDuration;

            while (_executionHistory.TryPeek(out var entry) && entry.timestamp < cutoff)
            {
                _executionHistory.TryDequeue(out _);
            }

            // Recalculate counts based on recent history
            int recentFailures = 0;
            int recentSuccesses = 0;

            foreach (var (_, success) in _executionHistory)
            {
                if (success)
                    recentSuccesses++;
                else
                    recentFailures++;
            }

            _failureCount = recentFailures;
            _successCount = recentSuccesses;
        }
    }
}

public class CircuitBreakerOpenException : Exception
{
    public TimeSpan RetryAfter { get; }

    public CircuitBreakerOpenException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }
}
