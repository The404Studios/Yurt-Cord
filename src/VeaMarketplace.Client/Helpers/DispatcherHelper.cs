using System.Windows;
using System.Windows.Threading;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Helper methods for safe UI thread invocation.
/// </summary>
public static class DispatcherHelper
{
    /// <summary>
    /// Invokes an action on the UI thread safely.
    /// Returns immediately if the application is shutting down.
    /// </summary>
    public static void InvokeOnUI(Action action)
    {
        var app = Application.Current;
        if (app == null) return;

        var dispatcher = app.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action, DispatcherPriority.Normal);
        }
    }

    /// <summary>
    /// Invokes an action on the UI thread asynchronously and safely.
    /// Returns immediately if the application is shutting down.
    /// </summary>
    public static async Task InvokeOnUIAsync(Action action)
    {
        var app = Application.Current;
        if (app == null) return;

        var dispatcher = app.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
        }
    }

    /// <summary>
    /// Invokes an async action on the UI thread safely.
    /// Returns immediately if the application is shutting down.
    /// </summary>
    public static async Task InvokeOnUIAsync(Func<Task> asyncAction)
    {
        var app = Application.Current;
        if (app == null) return;

        var dispatcher = app.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
        {
            await asyncAction();
        }
        else
        {
            await dispatcher.InvokeAsync(async () => await asyncAction(), DispatcherPriority.Normal);
        }
    }

    /// <summary>
    /// Begins invoking an action on the UI thread without waiting.
    /// Safe for fire-and-forget UI updates.
    /// </summary>
    public static void BeginInvokeOnUI(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        var app = Application.Current;
        if (app == null) return;

        var dispatcher = app.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        dispatcher.BeginInvoke(action, priority);
    }

    /// <summary>
    /// Checks if the current thread is the UI thread.
    /// </summary>
    public static bool IsOnUIThread()
    {
        var app = Application.Current;
        if (app == null) return false;

        var dispatcher = app.Dispatcher;
        return dispatcher?.CheckAccess() ?? false;
    }

    /// <summary>
    /// Runs an action with a delay on the UI thread.
    /// Useful for debouncing or delayed UI updates.
    /// </summary>
    public static async Task DelayedInvokeOnUIAsync(Action action, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            await InvokeOnUIAsync(action);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
    }
}
