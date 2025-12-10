using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Set up global exception handlers FIRST, before anything else
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IVoiceService, VoiceService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<MarketplaceViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<VoiceChannelViewModel>();

        ServiceProvider = services.BuildServiceProvider();

        base.OnStartup(e);
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
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VeaMarketplace",
                "crash.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{errorMessage}\n\n";
            File.AppendAllText(logPath, logEntry);
        }
        catch
        {
            // If we can't log, don't throw another exception
        }
    }

    private static void ShowErrorDialog(Exception rootException, string fullDetails)
    {
        var userMessage = $"""
            An error occurred in the application.

            {rootException.GetType().Name}: {rootException.Message}

            This error has been logged to:
            %LocalAppData%\VeaMarketplace\crash.log

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
}
