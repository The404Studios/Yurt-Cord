using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

public enum NetworkQuality
{
    Excellent,
    Good,
    Fair,
    Poor,
    VeryPoor,
    Disconnected
}

public interface INetworkQualityService
{
    NetworkQuality CurrentQuality { get; }
    int CurrentLatency { get; }
    double PacketLossRate { get; }
    bool IsConnected { get; }
    event Action<NetworkQuality>? OnQualityChanged;
    Task StartMonitoringAsync(string serverHost, CancellationToken cancellationToken = default);
    void StopMonitoring();
}

public class NetworkQualityService : INetworkQualityService
{
    private const int MonitoringIntervalMs = 5000;
    private const int SampleSize = 10;
    private const int TimeoutMs = 3000;

    private readonly Queue<int> _latencySamples = new();
    private readonly Queue<bool> _connectivitySamples = new();
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;

    public NetworkQuality CurrentQuality { get; private set; } = NetworkQuality.Disconnected;
    public int CurrentLatency { get; private set; }
    public double PacketLossRate { get; private set; }
    public bool IsConnected { get; private set; }

    public event Action<NetworkQuality>? OnQualityChanged;

    public async Task StartMonitoringAsync(string serverHost, CancellationToken cancellationToken = default)
    {
        StopMonitoring();

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = MonitorNetworkQualityAsync(serverHost, _monitoringCts.Token);

        await Task.CompletedTask;
    }

    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private async Task MonitorNetworkQualityAsync(string serverHost, CancellationToken cancellationToken)
    {
        using var ping = new Ping();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                PingReply? reply = null;

                try
                {
                    reply = await ping.SendPingAsync(serverHost, TimeoutMs);
                }
                catch (PingException ex)
                {
                    Debug.WriteLine($"Ping failed: {ex.Message}");
                }

                stopwatch.Stop();

                bool isSuccess = reply?.Status == IPStatus.Success;
                int latency = isSuccess && reply != null ? (int)reply.RoundtripTime : TimeoutMs;

                UpdateSamples(latency, isSuccess);
                UpdateQuality();

                await Task.Delay(MonitoringIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Network monitoring error: {ex.Message}");
                await Task.Delay(MonitoringIntervalMs, cancellationToken);
            }
        }
    }

    private void UpdateSamples(int latency, bool isSuccess)
    {
        _latencySamples.Enqueue(latency);
        _connectivitySamples.Enqueue(isSuccess);

        if (_latencySamples.Count > SampleSize)
        {
            _latencySamples.Dequeue();
        }

        if (_connectivitySamples.Count > SampleSize)
        {
            _connectivitySamples.Dequeue();
        }
    }

    private void UpdateQuality()
    {
        if (_connectivitySamples.Count == 0)
        {
            return;
        }

        // Calculate metrics
        var successfulPings = _connectivitySamples.Count(s => s);
        var totalPings = _connectivitySamples.Count;
        PacketLossRate = 1.0 - ((double)successfulPings / totalPings);

        IsConnected = successfulPings > 0;

        if (successfulPings > 0)
        {
            CurrentLatency = (int)_latencySamples.Where((_, i) => _connectivitySamples.ElementAt(i)).Average();
        }
        else
        {
            CurrentLatency = TimeoutMs;
        }

        // Determine quality
        var newQuality = CalculateQuality(CurrentLatency, PacketLossRate, IsConnected);

        if (newQuality != CurrentQuality)
        {
            var oldQuality = CurrentQuality;
            CurrentQuality = newQuality;

            Debug.WriteLine($"Network quality changed: {oldQuality} -> {newQuality} (Latency: {CurrentLatency}ms, Loss: {PacketLossRate:P1})");

            OnQualityChanged?.Invoke(newQuality);
        }
    }

    private static NetworkQuality CalculateQuality(int latency, double packetLoss, bool isConnected)
    {
        if (!isConnected)
        {
            return NetworkQuality.Disconnected;
        }

        // Excellent: <50ms latency, <1% loss
        if (latency < 50 && packetLoss < 0.01)
        {
            return NetworkQuality.Excellent;
        }

        // Good: <100ms latency, <5% loss
        if (latency < 100 && packetLoss < 0.05)
        {
            return NetworkQuality.Good;
        }

        // Fair: <200ms latency, <10% loss
        if (latency < 200 && packetLoss < 0.10)
        {
            return NetworkQuality.Fair;
        }

        // Poor: <400ms latency, <25% loss
        if (latency < 400 && packetLoss < 0.25)
        {
            return NetworkQuality.Poor;
        }

        // Very Poor: everything else
        return NetworkQuality.VeryPoor;
    }
}
