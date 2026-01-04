using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Provides connection retry logic with exponential backoff and jitter
/// </summary>
public class ConnectionRetryHelper
{
    private const int MaxRetries = 5;
    private const int InitialDelayMs = 1000;
    private const int MaxDelayMs = 30000;
    private const double JitterFactor = 0.2;

    private static readonly Random _random = new();

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = MaxRetries,
        CancellationToken cancellationToken = default,
        Action<int, Exception>? onRetry = null)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                attempt++;
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = CalculateDelay(attempt);
                onRetry?.Invoke(attempt, ex);

                Debug.WriteLine($"Retry attempt {attempt}/{maxRetries} after {delay}ms delay. Error: {ex.Message}");

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic (void return)
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = MaxRetries,
        CancellationToken cancellationToken = default,
        Action<int, Exception>? onRetry = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxRetries, cancellationToken, onRetry);
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter
    /// </summary>
    private static int CalculateDelay(int attempt)
    {
        var exponentialDelay = Math.Min(
            InitialDelayMs * Math.Pow(2, attempt - 1),
            MaxDelayMs
        );

        // Add jitter to prevent thundering herd
        var jitter = exponentialDelay * JitterFactor * (_random.NextDouble() * 2 - 1);

        return (int)(exponentialDelay + jitter);
    }

    /// <summary>
    /// Checks if an exception is retryable
    /// </summary>
    public static bool IsRetryableException(Exception ex)
    {
        return ex is TaskCanceledException or
            TimeoutException or
            HttpRequestException or
            System.Net.Sockets.SocketException or
            System.Net.Http.HttpRequestException;
    }
}
