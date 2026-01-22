using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using VeaMarketplace.Client.Helpers;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Client.Views;

namespace VeaMarketplace.Client;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Set up global exception handlers FIRST, before anything else
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Initialize XDG-compliant directories early
        XdgDirectories.InitializeDirectories();

        // Configure ThreadPool for high-performance streaming
        // Add extra threads for video encoding, decoding, and network I/O
        ConfigureThreadPoolForStreaming();

        // Configure GPU rendering for 60fps performance
        ConfigureGpuRendering();

        // Show splash screen
        var splash = new Views.SplashScreen();
        splash.Show();

        var services = new ServiceCollection();

        // Core Services
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IVoiceService, VoiceService>();
        services.AddSingleton<IWebRTCService, WebRTCService>(); // P2P connections for voice/video
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IFriendService, FriendService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INotificationHubService, NotificationHubService>();
        services.AddSingleton<IRoomHubService, RoomHubService>();
        services.AddSingleton<IToastNotificationService, ToastNotificationService>();
        services.AddSingleton<IFileUploadService, FileUploadService>();
        services.AddSingleton<IImageCacheService, ImageCacheService>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IQoLService, QoLService>();
        services.AddSingleton<ISocialService, SocialService>();
        services.AddSingleton<ILeaderboardService, LeaderboardService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<ITranslationService, TranslationService>();

        // Advanced Infrastructure Services
        services.AddSingleton<IPerformanceMonitorService, PerformanceMonitorService>();
        services.AddSingleton<INetworkQualityService, NetworkQualityService>();
        services.AddSingleton<IConnectionStatsService, ConnectionStatsService>();
        services.AddSingleton<ICacheManagementService, CacheManagementService>();
        services.AddSingleton<IAutoReconnectionService, AutoReconnectionService>();
        services.AddSingleton<IOfflineMessageQueueService, OfflineMessageQueueService>();
        services.AddSingleton<IBandwidthThrottleService, BandwidthThrottleService>();
        services.AddSingleton<IDiagnosticLoggerService, DiagnosticLoggerService>();

        // Enterprise Services
        services.AddSingleton<IRateLimitingService, RateLimitingService>();
        services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
        services.AddSingleton<IBackgroundTaskScheduler, BackgroundTaskScheduler>();
        services.AddSingleton<ICrashReportingService, CrashReportingService>();

        // Application Initialization Service
        services.AddSingleton<ApplicationInitializationService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton<ChatViewModel>(); // Singleton - shared chat state across all views
        services.AddTransient<MarketplaceViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<VoiceChannelViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<FriendsViewModel>();

        // Additional ViewModels for feature views
        services.AddTransient<CartViewModel>();
        services.AddTransient<WishlistViewModel>();
        services.AddTransient<OrderHistoryViewModel>();
        services.AddTransient<NotificationCenterViewModel>();
        services.AddTransient<ModerationPanelViewModel>();
        services.AddTransient<ProductReviewsViewModel>();
        services.AddTransient<ActivityFeedViewModel>();
        services.AddTransient<LeaderboardViewModel>();
        services.AddTransient<ServerAdminViewModel>();
        services.AddTransient<DiscoverViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<CheckoutViewModel>();
        services.AddTransient<ProductDetailViewModel>();

        ServiceProvider = services.BuildServiceProvider();

        // Initialize all services using the initialization service
        var appInit = ServiceProvider.GetRequiredService<ApplicationInitializationService>();
        await appInit.InitializeAsync();

        // Initialize services that need async startup (with error handling)
        var socialService = ServiceProvider.GetRequiredService<ISocialService>();
        _ = SafeFireAndForgetAsync(socialService.LoadAsync(), "SocialService.LoadAsync");

        var leaderboardService = ServiceProvider.GetRequiredService<ILeaderboardService>();
        _ = SafeFireAndForgetAsync(leaderboardService.LoadAsync(), "LeaderboardService.LoadAsync");

        // Initialize WebRTC service for P2P connections
        var webRTCService = ServiceProvider.GetRequiredService<IWebRTCService>();
        _ = SafeFireAndForgetAsync(webRTCService.InitializeAsync(), "WebRTCService.InitializeAsync");

        // Wire up cross-service event handlers for social activity tracking
        WireUpSocialActivityTracking();

        // Wait for splash to complete
        await Task.Delay(2500);
        await splash.CompleteAndClose();

        // Show Overseer main window with Matrix background and circular design
        var overseerWindow = new OverseerWindow();
        overseerWindow.Show();

        // Initialize the transparent overlay for flying messages across screen
        InitializeOverseerOverlay();

        base.OnStartup(e);
    }

    private void InitializeOverseerOverlay()
    {
        // Create and show the overlay (it's transparent and click-through)
        var overlay = OverseerOverlay.Instance;
        overlay.Show();

        // Wire up friend service for flying message notifications
        var friendService = (IFriendService)ServiceProvider.GetService(typeof(IFriendService))!;
        friendService.OnDirectMessageReceived += (msg) =>
        {
            // Spawn flying message when DM received
            overlay.SpawnFlyingMessage(msg.SenderUsername, msg.Content);
        };

        // Wire up for friend requests
        friendService.OnNewFriendRequest += (request) =>
        {
            overlay.SpawnFlyingNotification(
                "FRIEND REQUEST",
                $"{request.RequesterUsername} wants to connect",
                OverseerOverlay.NotificationType.FriendRequest);
        };

        // Wire up voice service for nudges
        var voiceService = (IVoiceService)ServiceProvider.GetService(typeof(IVoiceService))!;
        voiceService.OnNudgeReceived += (nudge) =>
        {
            overlay.SpawnFlyingEnvelope(nudge.FromUsername);
        };

        // Wire up WebRTC for P2P voice connections
        WireUpWebRTCIntegration(voiceService);
    }

    /// <summary>
    /// Wire up WebRTC service with VoiceService for P2P connections.
    /// Enables lower latency audio for 1:1 and small group calls.
    /// </summary>
    private static void WireUpWebRTCIntegration(IVoiceService voiceService)
    {
        try
        {
            var webRTCService = ServiceProvider.GetRequiredService<IWebRTCService>();

            // When a P2P connection is requested, handle it through WebRTC service
            voiceService.OnP2PConnectionRequested += async (connectionId, userId, username) =>
            {
                Debug.WriteLine($"P2P connection requested from {username}, handling via WebRTC service");
                await webRTCService.HandleP2PRequestAsync(connectionId, userId, username);
            };

            // Forward WebRTC offer to the service
            voiceService.OnWebRTCOfferReceived += async (connectionId, userId, username, sdpOffer) =>
            {
                await webRTCService.HandleOfferAsync(connectionId, sdpOffer);
            };

            // Forward WebRTC answer to the service
            voiceService.OnWebRTCAnswerReceived += async (connectionId, userId, username, sdpAnswer) =>
            {
                await webRTCService.HandleAnswerAsync(connectionId, sdpAnswer);
            };

            // Forward ICE candidates to the service
            voiceService.OnICECandidateReceived += async (connectionId, candidate, sdpMid, sdpMLineIndex) =>
            {
                await webRTCService.HandleICECandidateAsync(connectionId, candidate, sdpMid, sdpMLineIndex);
            };

            // Log P2P connection success
            voiceService.OnP2PConnected += (connectionId) =>
            {
                Debug.WriteLine($"P2P connection established with {connectionId}");
            };

            // Handle P2P failure - already falls back to server relay automatically
            voiceService.OnP2PFailed += (connectionId, reason) =>
            {
                Debug.WriteLine($"P2P connection failed with {connectionId}: {reason}. Using server relay.");
            };

            Debug.WriteLine("WebRTC integration wired up successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to wire up WebRTC integration: {ex.Message}");
            // Non-critical - voice will still work via server relay
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var rootException = UnwrapException(e.Exception);
        var errorMessage = FormatExceptionDetails(rootException, e.Exception);

        LogException(errorMessage);
        ShowErrorDialog(rootException, errorMessage);

        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var rootException = UnwrapException(ex);
            var errorMessage = FormatExceptionDetails(rootException, ex);

            LogException(errorMessage);
            ShowErrorDialog(rootException, errorMessage);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var rootException = UnwrapException(e.Exception);
        var errorMessage = FormatExceptionDetails(rootException, e.Exception);

        LogException(errorMessage);
        e.SetObserved();
    }

    /// <summary>
    /// Unwraps wrapper exceptions to find the real root cause.
    /// TargetInvocationException, XamlParseException, and TypeInitializationException
    /// all wrap the actual exception that occurred.
    /// </summary>
    private static Exception UnwrapException(Exception ex)
    {
        var current = ex;

        while (current != null)
        {
            // These exception types are wrappers - the real error is in InnerException
            if (current is TargetInvocationException ||
                current is XamlParseException ||
                current is TypeInitializationException)
            {
                if (current.InnerException != null)
                {
                    current = current.InnerException;
                    continue;
                }
            }

            // AggregateException can contain multiple exceptions
            if (current is AggregateException aggEx && aggEx.InnerExceptions.Count > 0)
            {
                current = aggEx.InnerExceptions[0];
                continue;
            }

            // If we have an inner exception and current is a generic wrapper, dig deeper
            if (current.InnerException != null &&
                current.Message.Contains("target of an invocation", StringComparison.OrdinalIgnoreCase))
            {
                current = current.InnerException;
                continue;
            }

            break;
        }

        return current ?? ex;
    }

    private static string FormatExceptionDetails(Exception rootException, Exception originalException)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                    UNHANDLED EXCEPTION                        ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Show the ROOT cause prominently
        sb.AppendLine("ROOT CAUSE:");
        sb.AppendLine($"  Type: {rootException.GetType().FullName}");
        sb.AppendLine($"  Message: {rootException.Message}");

        if (!string.IsNullOrEmpty(rootException.StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine("STACK TRACE (root cause):");
            sb.AppendLine(rootException.StackTrace);
        }

        // If the original was wrapped, show the wrapper chain
        if (originalException != rootException)
        {
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("EXCEPTION WRAPPER CHAIN:");

            var current = originalException;
            var depth = 0;
            while (current != null && depth < 10)
            {
                var indent = new string(' ', depth * 2);
                sb.AppendLine($"{indent}[{depth}] {current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                depth++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    private static void LogException(string errorMessage)
    {
        try
        {
            var logPath = XdgDirectories.CrashLogPath;

            XdgDirectories.EnsureDirectoryExists(XdgDirectories.LogDirectory);

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{errorMessage}\n\n";
            File.AppendAllText(logPath, logEntry);
        }
        catch
        {
            // If we can't log, don't throw another exception
        }
    }

    /// <summary>
    /// Safely executes a task in fire-and-forget fashion with proper exception handling.
    /// </summary>
    private static async Task SafeFireAndForgetAsync(Task task, string operationName)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{operationName}] Background task failed: {ex.Message}");
            LogException($"Background task '{operationName}' failed:\n{ex}");
        }
    }

    private static void ShowErrorDialog(Exception rootException, string fullDetails)
    {
        var userMessage = $"""
            An error occurred in the application.

            {rootException.GetType().Name}: {rootException.Message}

            This error has been logged to:
            {XdgDirectories.CrashLogPath}

            Click OK to exit the application.
            """;

        try
        {
            MessageBox.Show(
                userMessage,
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If MessageBox fails, write to console as last resort
            Console.Error.WriteLine(fullDetails);
        }
    }

    // Cache of voice channel users for social activity tracking
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, VoiceUserState> _cachedVoiceUsers = new();

    /// <summary>
    /// Wire up cross-service event handlers for social activity tracking.
    /// Connects voice/screen share events to social service for friend activity feeds.
    /// </summary>
    private static void WireUpSocialActivityTracking()
    {
        try
        {
            var voiceService = ServiceProvider.GetRequiredService<IVoiceService>();
            var socialService = ServiceProvider.GetRequiredService<ISocialService>();
            var friendService = ServiceProvider.GetRequiredService<IFriendService>();

            // Cache voice channel users for lookup
            voiceService.OnVoiceChannelUsers += (users) =>
            {
                _cachedVoiceUsers.Clear();
                foreach (var user in users)
                {
                    _cachedVoiceUsers[user.ConnectionId] = user;
                }
            };

            voiceService.OnUserJoinedVoice += (user) =>
            {
                if (user != null)
                {
                    _cachedVoiceUsers[user.ConnectionId] = user;

                    // Track voice channel join for friends
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(user.UserId))
                            {
                                var friends = friendService.Friends;
                                var isFriend = friends.Any(f => f.UserId == user.UserId);

                                if (isFriend)
                                {
                                    await socialService.RecordInteractionAsync(
                                        user.UserId,
                                        Models.InteractionType.VoiceChannelJoined,
                                        user.Username);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to track voice join activity: {ex.Message}");
                        }
                    });
                }
            };

            voiceService.OnUserLeftVoice += (user) =>
            {
                if (user != null)
                {
                    _cachedVoiceUsers.TryRemove(user.ConnectionId, out _);

                    // Track voice channel leave for friends
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(user.UserId))
                            {
                                var friends = friendService.Friends;
                                var isFriend = friends.Any(f => f.UserId == user.UserId);

                                if (isFriend)
                                {
                                    await socialService.RecordInteractionAsync(
                                        user.UserId,
                                        Models.InteractionType.VoiceChannelLeft,
                                        user.Username);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to track voice leave activity: {ex.Message}");
                        }
                    });
                }
            };

            // Track screen share activity for friends in the same voice channel
            voiceService.OnUserScreenShareChanged += (connectionId, isSharing) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Get user from cached voice users
                        if (_cachedVoiceUsers.TryGetValue(connectionId, out var user) &&
                            !string.IsNullOrEmpty(user.UserId))
                        {
                            // Check if this user is a friend
                            var friends = friendService.Friends;
                            var isFriend = friends.Any(f => f.UserId == user.UserId);

                            if (isFriend)
                            {
                                var interactionType = isSharing
                                    ? Models.InteractionType.ScreenShareStarted
                                    : Models.InteractionType.ScreenShareEnded;

                                await socialService.RecordInteractionAsync(
                                    user.UserId,
                                    interactionType,
                                    user.Username);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to track screen share activity: {ex.Message}");
                    }
                });
            };

            Debug.WriteLine("Social activity tracking wired up successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to wire up social activity tracking: {ex.Message}");
        }
    }

    /// <summary>
    /// Configure ThreadPool for high-performance streaming.
    /// Increases min threads to handle video encoding, decoding, and network I/O concurrently.
    /// </summary>
    private static void ConfigureThreadPoolForStreaming()
    {
        // Get current min threads
        ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);

        // Increase minimum threads for streaming workloads:
        // - 2 extra for video encoding (capture + encode)
        // - 2 extra for video decoding (decode + color convert)
        // - 2 extra for network I/O (send + receive)
        // - Keep existing minimum as base
        var processorCount = Environment.ProcessorCount;
        var newWorkerMin = Math.Max(workerThreads, processorCount + 6);
        var newCompletionMin = Math.Max(completionPortThreads, processorCount + 4);

        ThreadPool.SetMinThreads(newWorkerMin, newCompletionMin);

        Debug.WriteLine($"ThreadPool configured: min workers={newWorkerMin}, min IO={newCompletionMin} (processors={processorCount})");
    }

    /// <summary>
    /// Configure GPU rendering for optimal 60fps performance.
    /// Enables hardware acceleration and sets rendering options for smooth animations.
    /// </summary>
    private static void ConfigureGpuRendering()
    {
        try
        {
            // Check if hardware rendering is available
            var renderingTier = RenderCapability.Tier >> 16;
            Debug.WriteLine($"WPF Rendering Tier: {renderingTier}");

            // Tier 0: Software rendering only
            // Tier 1: Partial hardware acceleration
            // Tier 2: Full hardware acceleration (ideal)

            if (renderingTier >= 2)
            {
                Debug.WriteLine("Full GPU hardware acceleration available - optimal for 60fps");
            }
            else if (renderingTier == 1)
            {
                Debug.WriteLine("Partial hardware acceleration - some effects may render in software");
            }
            else
            {
                Debug.WriteLine("Software rendering only - performance may be limited");
            }

            // Set process priority to Above Normal for smoother rendering
            // (only if not debugging to avoid IDE performance issues)
            if (!Debugger.IsAttached)
            {
                try
                {
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
                    Debug.WriteLine("Process priority set to AboveNormal for smoother rendering");
                }
                catch
                {
                    // May fail without admin rights - not critical
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GPU rendering configuration warning: {ex.Message}");
        }
    }
}
