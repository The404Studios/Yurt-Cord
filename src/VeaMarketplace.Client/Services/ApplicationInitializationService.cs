using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VeaMarketplace.Client.Helpers;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Manages application initialization and wiring of all services
/// </summary>
public class ApplicationInitializationService
{
    private readonly ICrashReportingService _crashReporter;
    private readonly IHealthCheckService _healthCheck;
    private readonly IConfigurationService _config;
    private readonly IFeatureFlagService _featureFlags;
    private readonly INetworkQualityService _networkQuality;
    private readonly IBackgroundTaskScheduler _taskScheduler;
    private readonly IDiagnosticLoggerService _diagnosticLogger;
    private readonly IAutoReconnectionService _autoReconnect;

    public ApplicationInitializationService(
        ICrashReportingService crashReporter,
        IHealthCheckService healthCheck,
        IConfigurationService config,
        IFeatureFlagService featureFlags,
        INetworkQualityService networkQuality,
        IBackgroundTaskScheduler taskScheduler,
        IDiagnosticLoggerService diagnosticLogger,
        IAutoReconnectionService autoReconnect)
    {
        _crashReporter = crashReporter;
        _healthCheck = healthCheck;
        _config = config;
        _featureFlags = featureFlags;
        _networkQuality = networkQuality;
        _taskScheduler = taskScheduler;
        _diagnosticLogger = diagnosticLogger;
        _autoReconnect = autoReconnect;
    }

    /// <summary>
    /// Initializes all application services in the correct order
    /// </summary>
    public async Task InitializeAsync()
    {
        Debug.WriteLine("Starting application initialization...");

        try
        {
            // 1. Initialize crash reporting first (catches all startup errors)
            _crashReporter.Initialize();
            Debug.WriteLine("✓ Crash reporting initialized");

            // 2. Load configuration
            await _config.LoadAsync();
            Debug.WriteLine("✓ Configuration loaded");

            // 3. Set up default configurations if not exist
            SetupDefaultConfigurations();
            Debug.WriteLine("✓ Default configurations set");

            // 4. Register health checks
            RegisterHealthChecks();
            Debug.WriteLine("✓ Health checks registered");

            // 5. Start health monitoring
            await _healthCheck.StartMonitoringAsync(TimeSpan.FromMinutes(5));
            Debug.WriteLine("✓ Health monitoring started");

            // 6. Start network quality monitoring
            var serverUrl = AppConstants.DefaultServerUrl.Replace("http://", "").Replace(":5000", "");
            await _networkQuality.StartMonitoringAsync(serverUrl);
            Debug.WriteLine($"✓ Network quality monitoring started for {serverUrl}");

            // 7. Schedule background maintenance tasks
            ScheduleMaintenanceTasks();
            Debug.WriteLine("✓ Background tasks scheduled");

            // 8. Start memory monitoring
            MemoryManagementHelper.StartMonitoring(TimeSpan.FromMinutes(1));
            Debug.WriteLine("✓ Memory monitoring started");

            // 9. Log successful initialization
            _diagnosticLogger.Info("AppInit", "Application initialized successfully");

            Debug.WriteLine("Application initialization completed successfully!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Application initialization failed: {ex.Message}");
            _crashReporter.ReportCrash(ex, CrashSeverity.Critical);
            throw;
        }
    }

    /// <summary>
    /// Sets up default configuration values
    /// </summary>
    private void SetupDefaultConfigurations()
    {
        _config.SetIfNotExists("Theme", "Dark");
        _config.SetIfNotExists("EnableNotifications", true);
        _config.SetIfNotExists("EnableSounds", true);
        _config.SetIfNotExists("MasterVolume", 1.0);
        _config.SetIfNotExists("PushToTalkEnabled", false);
        _config.SetIfNotExists("VoiceActivityThreshold", 0.02);
        _config.SetIfNotExists("MaxCacheSizeMB", 500);
        _config.SetIfNotExists("AutoReconnect", true);
        _config.SetIfNotExists("DiagnosticLogging", true);
    }

    /// <summary>
    /// Registers built-in health checks
    /// </summary>
    private void RegisterHealthChecks()
    {
        // Memory health check
        _healthCheck.RegisterHealthCheck(new MemoryHealthCheck(maxMemoryBytes: 1024 * 1024 * 1024)); // 1GB

        // Network health check
        var serverUrl = AppConstants.DefaultServerUrl.Replace("http://", "").Replace(":5000", "");
        _healthCheck.RegisterHealthCheck(new NetworkHealthCheck(serverUrl));

        // Disk space health check
        var appDrive = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)[0].ToString();
        _healthCheck.RegisterHealthCheck(new DiskSpaceHealthCheck($"{appDrive}:\\", minFreeBytes: 1024 * 1024 * 1024)); // 1GB
    }

    /// <summary>
    /// Schedules recurring background maintenance tasks
    /// </summary>
    private void ScheduleMaintenanceTasks()
    {
        // Clean cache every hour
        _taskScheduler.ScheduleRecurringTask(
            "CleanupCache",
            async ct =>
            {
                var cacheManager = App.ServiceProvider?.GetService(typeof(ICacheManagementService)) as ICacheManagementService;
                if (cacheManager != null)
                {
                    var maxSize = _config.Get("MaxCacheSizeMB", 500) * 1024 * 1024L;
                    await cacheManager.OptimizeCacheAsync(maxSize);
                    Debug.WriteLine("Scheduled cache cleanup completed");
                }
            },
            TimeSpan.FromHours(1)
        );

        // Cleanup old crash reports (keep last 30 days)
        _taskScheduler.ScheduleRecurringTask(
            "CleanupOldCrashReports",
            async ct =>
            {
                var reports = await _crashReporter.GetCrashReportsAsync();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var oldReports = reports.Where(r => r.Timestamp < cutoff).ToList();

                Debug.WriteLine($"Found {oldReports.Count} old crash reports to clean up");
            },
            TimeSpan.FromDays(1)
        );

        // Memory optimization every 30 minutes if pressure is high
        _taskScheduler.ScheduleRecurringTask(
            "MemoryOptimization",
            async ct =>
            {
                if (MemoryManagementHelper.IsMemoryConstrained())
                {
                    Debug.WriteLine("High memory pressure detected, optimizing...");
                    MemoryManagementHelper.OptimizeMemory();
                }
                await Task.CompletedTask;
            },
            TimeSpan.FromMinutes(30)
        );

        // Save configuration every 5 minutes (in case of crash)
        _taskScheduler.ScheduleRecurringTask(
            "SaveConfiguration",
            async ct =>
            {
                await _config.SaveAsync();
                Debug.WriteLine("Configuration auto-saved");
            },
            TimeSpan.FromMinutes(5)
        );
    }

    /// <summary>
    /// Performs graceful shutdown of all services
    /// </summary>
    public async Task ShutdownAsync()
    {
        Debug.WriteLine("Starting application shutdown...");

        try
        {
            // Stop monitoring services
            _healthCheck.StopMonitoring();
            _networkQuality.StopMonitoring();
            _autoReconnect.StopMonitoring();
            MemoryManagementHelper.StopMonitoring();

            // Shutdown background tasks
            _taskScheduler.Shutdown();

            // Save final configuration
            await _config.SaveAsync();

            // Log shutdown
            _diagnosticLogger.Info("AppShutdown", "Application shutdown completed successfully");

            Debug.WriteLine("Application shutdown completed successfully!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during shutdown: {ex.Message}");
            _crashReporter.ReportCrash(ex, CrashSeverity.Medium);
        }
    }
}
