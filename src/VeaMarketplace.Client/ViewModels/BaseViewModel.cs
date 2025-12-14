using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VeaMarketplace.Client.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _statusMessage;

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        StatusMessage = null;
    }

    protected void ClearError()
    {
        ErrorMessage = null;
        HasError = false;
    }

    protected void SetStatus(string message)
    {
        StatusMessage = message;
        ClearError();
    }

    protected void ClearStatus()
    {
        StatusMessage = null;
    }

    /// <summary>
    /// Executes an async operation with automatic loading state and error handling.
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation, string? errorPrefix = null)
    {
        if (IsLoading) return;

        IsLoading = true;
        ClearError();

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrEmpty(errorPrefix)
                ? ex.Message
                : $"{errorPrefix}: {ex.Message}";
            SetError(message);
            Debug.WriteLine($"[{GetType().Name}] {message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an async operation with automatic loading state and error handling, returning a result.
    /// </summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? errorPrefix = null)
    {
        if (IsLoading) return default;

        IsLoading = true;
        ClearError();

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrEmpty(errorPrefix)
                ? ex.Message
                : $"{errorPrefix}: {ex.Message}";
            SetError(message);
            Debug.WriteLine($"[{GetType().Name}] {message}");
            return default;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an async operation without blocking if already loading.
    /// Useful for background refresh operations.
    /// </summary>
    protected async Task ExecuteInBackgroundAsync(Func<Task> operation, string? errorPrefix = null)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{GetType().Name}] Background operation failed: {ex.Message}");
        }
    }
}
