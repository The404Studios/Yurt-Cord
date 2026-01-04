using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VeaMarketplace.Client.Helpers;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Provides automatic reconnection capabilities for services
/// </summary>
public interface IAutoReconnectionService
{
    bool IsReconnecting { get; }
    int ReconnectionAttempts { get; }
    event Action<int>? OnReconnecting;
    event Action? OnReconnected;
    event Action<Exception>? OnReconnectionFailed;
    Task StartMonitoringAsync(
        Func<Task<bool>> connectionCheckFunc,
        Func<Task> reconnectFunc,
        CancellationToken cancellationToken = default);
    void StopMonitoring();
}

public class AutoReconnectionService : IAutoReconnectionService
{
    private const int CheckIntervalMs = 5000;
    private const int MaxReconnectionAttempts = 10;

    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;
    private int _reconnectionAttempts;

    public bool IsReconnecting { get; private set; }
    public int ReconnectionAttempts => _reconnectionAttempts;

    public event Action<int>? OnReconnecting;
    public event Action? OnReconnected;
    public event Action<Exception>? OnReconnectionFailed;

    public async Task StartMonitoringAsync(
        Func<Task<bool>> connectionCheckFunc,
        Func<Task> reconnectFunc,
        CancellationToken cancellationToken = default)
    {
        StopMonitoring();

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = MonitorConnectionAsync(connectionCheckFunc, reconnectFunc, _monitoringCts.Token);

        await Task.CompletedTask;
    }

    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        IsReconnecting = false;
        _reconnectionAttempts = 0;
    }

    private async Task MonitorConnectionAsync(
        Func<Task<bool>> connectionCheckFunc,
        Func<Task> reconnectFunc,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine("Auto-reconnection monitoring started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckIntervalMs, cancellationToken);

                // Check if connected
                bool isConnected = false;
                try
                {
                    isConnected = await connectionCheckFunc();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Connection check failed: {ex.Message}");
                }

                if (!isConnected && !IsReconnecting)
                {
                    Debug.WriteLine("Connection lost, initiating reconnection...");
                    await AttemptReconnectionAsync(reconnectFunc, cancellationToken);
                }
                else if (isConnected && IsReconnecting)
                {
                    // Connection restored
                    IsReconnecting = false;
                    _reconnectionAttempts = 0;
                    Debug.WriteLine("Connection restored");
                    OnReconnected?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in connection monitoring: {ex.Message}");
            }
        }

        Debug.WriteLine("Auto-reconnection monitoring stopped");
    }

    private async Task AttemptReconnectionAsync(Func<Task> reconnectFunc, CancellationToken cancellationToken)
    {
        IsReconnecting = true;
        _reconnectionAttempts = 0;

        while (_reconnectionAttempts < MaxReconnectionAttempts && !cancellationToken.IsCancellationRequested)
        {
            _reconnectionAttempts++;

            Debug.WriteLine($"Reconnection attempt {_reconnectionAttempts}/{MaxReconnectionAttempts}");
            OnReconnecting?.Invoke(_reconnectionAttempts);

            try
            {
                await ConnectionRetryHelper.ExecuteWithRetryAsync(
                    async () =>
                    {
                        await reconnectFunc();
                    },
                    maxRetries: 3,
                    cancellationToken: cancellationToken,
                    onRetry: (attempt, ex) =>
                    {
                        Debug.WriteLine($"Retry {attempt} after error: {ex.Message}");
                    }
                );

                // If we got here, reconnection succeeded
                Debug.WriteLine("Reconnection successful");
                IsReconnecting = false;
                _reconnectionAttempts = 0;
                OnReconnected?.Invoke();
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reconnection attempt {_reconnectionAttempts} failed: {ex.Message}");

                if (_reconnectionAttempts >= MaxReconnectionAttempts)
                {
                    Debug.WriteLine("Max reconnection attempts reached, giving up");
                    IsReconnecting = false;
                    OnReconnectionFailed?.Invoke(ex);
                    return;
                }

                // Wait before next attempt with exponential backoff
                var delay = Math.Min(1000 * Math.Pow(2, _reconnectionAttempts - 1), 30000);
                await Task.Delay((int)delay, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Extension methods for services to add auto-reconnection capabilities
/// </summary>
public static class AutoReconnectionExtensions
{
    public static async Task WithAutoReconnectionAsync(
        this Task serviceTask,
        IAutoReconnectionService reconnectionService,
        Func<Task<bool>> connectionCheckFunc,
        Func<Task> reconnectFunc,
        CancellationToken cancellationToken = default)
    {
        await reconnectionService.StartMonitoringAsync(
            connectionCheckFunc,
            reconnectFunc,
            cancellationToken
        );

        await serviceTask;
    }
}
