using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Background task status
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Scheduled task information
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public Func<CancellationToken, Task> Action { get; set; } = _ => Task.CompletedTask;
    public TimeSpan? Interval { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Exception? Exception { get; set; }
    public int ExecutionCount { get; set; }
    public int MaxExecutions { get; set; } = -1; // -1 = infinite
}

public interface IBackgroundTaskScheduler
{
    string ScheduleTask(string name, Func<CancellationToken, Task> action, TimeSpan delay);
    string ScheduleRecurringTask(string name, Func<CancellationToken, Task> action, TimeSpan interval, int maxExecutions = -1);
    string ScheduleTaskAt(string name, Func<CancellationToken, Task> action, DateTime scheduledFor);
    bool CancelTask(string taskId);
    bool PauseTask(string taskId);
    bool ResumeTask(string taskId);
    ScheduledTask? GetTask(string taskId);
    List<ScheduledTask> GetAllTasks();
    List<ScheduledTask> GetRunningTasks();
    void ClearCompletedTasks();
    void Shutdown();
    event Action<string, TaskStatus>? OnTaskStatusChanged;
}

public class BackgroundTaskScheduler : IBackgroundTaskScheduler
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCancellations = new();
    private readonly ConcurrentDictionary<string, Task> _runningTasks = new();
    private readonly Timer _schedulerTimer;
    private readonly SemaphoreSlim _executionLock = new(10, 10); // Max 10 concurrent tasks
    private bool _isShuttingDown;

    public event Action<string, TaskStatus>? OnTaskStatusChanged;

    public BackgroundTaskScheduler()
    {
        // Check for pending tasks every second
        _schedulerTimer = new Timer(_ => ProcessPendingTasks(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        Debug.WriteLine("Background task scheduler initialized");
    }

    public string ScheduleTask(string name, Func<CancellationToken, Task> action, TimeSpan delay)
    {
        var scheduledFor = DateTime.UtcNow.Add(delay);
        return ScheduleTaskAt(name, action, scheduledFor);
    }

    public string ScheduleRecurringTask(string name, Func<CancellationToken, Task> action, TimeSpan interval, int maxExecutions = -1)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Action = action,
            Interval = interval,
            ScheduledFor = DateTime.UtcNow.Add(interval),
            MaxExecutions = maxExecutions
        };

        _tasks[task.Id] = task;

        Debug.WriteLine($"Scheduled recurring task '{name}' (ID: {task.Id}) with interval {interval.TotalSeconds}s");

        return task.Id;
    }

    public string ScheduleTaskAt(string name, Func<CancellationToken, Task> action, DateTime scheduledFor)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Action = action,
            ScheduledFor = scheduledFor
        };

        _tasks[task.Id] = task;

        Debug.WriteLine($"Scheduled task '{name}' (ID: {task.Id}) for {scheduledFor:yyyy-MM-dd HH:mm:ss}");

        return task.Id;
    }

    public bool CancelTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            if (_taskCancellations.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
            }

            task.Status = TaskStatus.Cancelled;
            UpdateTaskStatus(taskId, TaskStatus.Cancelled);

            Debug.WriteLine($"Task '{task.Name}' (ID: {taskId}) cancelled");

            return true;
        }

        return false;
    }

    public bool PauseTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task.Status == TaskStatus.Pending)
        {
            // Set scheduled time far in the future to effectively pause
            task.ScheduledFor = DateTime.MaxValue;

            Debug.WriteLine($"Task '{task.Name}' (ID: {taskId}) paused");

            return true;
        }

        return false;
    }

    public bool ResumeTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task.ScheduledFor == DateTime.MaxValue)
        {
            // Resume with original interval if recurring, otherwise schedule for now
            task.ScheduledFor = task.Interval.HasValue
                ? DateTime.UtcNow.Add(task.Interval.Value)
                : DateTime.UtcNow;

            Debug.WriteLine($"Task '{task.Name}' (ID: {taskId}) resumed");

            return true;
        }

        return false;
    }

    public ScheduledTask? GetTask(string taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    public List<ScheduledTask> GetAllTasks()
    {
        return _tasks.Values.ToList();
    }

    public List<ScheduledTask> GetRunningTasks()
    {
        return _tasks.Values.Where(t => t.Status == TaskStatus.Running).ToList();
    }

    public void ClearCompletedTasks()
    {
        var completedTasks = _tasks.Where(kvp =>
            kvp.Value.Status == TaskStatus.Completed ||
            kvp.Value.Status == TaskStatus.Failed ||
            kvp.Value.Status == TaskStatus.Cancelled
        ).ToList();

        foreach (var kvp in completedTasks)
        {
            _tasks.TryRemove(kvp.Key, out _);
            _taskCancellations.TryRemove(kvp.Key, out var cts);
            cts?.Dispose();
        }

        Debug.WriteLine($"Cleared {completedTasks.Count} completed tasks");
    }

    public void Shutdown()
    {
        _isShuttingDown = true;

        Debug.WriteLine("Shutting down background task scheduler...");

        // Cancel all running tasks
        foreach (var cts in _taskCancellations.Values)
        {
            cts.Cancel();
        }

        // Wait for running tasks to complete (with timeout)
        var waitTask = Task.WhenAll(_runningTasks.Values);
        waitTask.Wait(TimeSpan.FromSeconds(10));

        _schedulerTimer.Dispose();

        Debug.WriteLine("Background task scheduler shutdown complete");
    }

    private void ProcessPendingTasks()
    {
        if (_isShuttingDown)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var tasksToExecute = _tasks.Values
            .Where(t => t.Status == TaskStatus.Pending &&
                       t.ScheduledFor.HasValue &&
                       t.ScheduledFor.Value <= now)
            .ToList();

        foreach (var task in tasksToExecute)
        {
            _ = ExecuteTaskAsync(task);
        }
    }

    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        // Throttle concurrent executions
        await _executionLock.WaitAsync();

        try
        {
            task.Status = TaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;
            UpdateTaskStatus(task.Id, TaskStatus.Running);

            var cts = new CancellationTokenSource();
            _taskCancellations[task.Id] = cts;

            var executionTask = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine($"Executing task '{task.Name}' (ID: {task.Id})");

                    await task.Action(cts.Token);

                    task.Status = TaskStatus.Completed;
                    task.CompletedAt = DateTime.UtcNow;
                    task.ExecutionCount++;

                    Debug.WriteLine($"Task '{task.Name}' (ID: {task.Id}) completed (execution #{task.ExecutionCount})");

                    UpdateTaskStatus(task.Id, TaskStatus.Completed);

                    // Reschedule if recurring and not exceeded max executions
                    if (task.Interval.HasValue &&
                        (task.MaxExecutions == -1 || task.ExecutionCount < task.MaxExecutions))
                    {
                        task.ScheduledFor = DateTime.UtcNow.Add(task.Interval.Value);
                        task.Status = TaskStatus.Pending;
                        UpdateTaskStatus(task.Id, TaskStatus.Pending);

                        Debug.WriteLine($"Rescheduled recurring task '{task.Name}' for {task.ScheduledFor:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                catch (OperationCanceledException)
                {
                    task.Status = TaskStatus.Cancelled;
                    task.CompletedAt = DateTime.UtcNow;

                    Debug.WriteLine($"Task '{task.Name}' (ID: {task.Id}) was cancelled");

                    UpdateTaskStatus(task.Id, TaskStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    task.Status = TaskStatus.Failed;
                    task.CompletedAt = DateTime.UtcNow;
                    task.Exception = ex;

                    Debug.WriteLine($"Task '{task.Name}' (ID: {task.Id}) failed: {ex.Message}");

                    UpdateTaskStatus(task.Id, TaskStatus.Failed);
                }
                finally
                {
                    _taskCancellations.TryRemove(task.Id, out _);
                    _runningTasks.TryRemove(task.Id, out _);
                    cts.Dispose();
                }
            }, cts.Token);

            _runningTasks[task.Id] = executionTask;
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private void UpdateTaskStatus(string taskId, TaskStatus status)
    {
        try
        {
            OnTaskStatusChanged?.Invoke(taskId, status);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error notifying task status change: {ex.Message}");
        }
    }
}

/// <summary>
/// Extension methods for background task scheduler
/// </summary>
public static class BackgroundTaskSchedulerExtensions
{
    public static string ScheduleDaily(
        this IBackgroundTaskScheduler scheduler,
        string name,
        Func<CancellationToken, Task> action,
        TimeSpan timeOfDay)
    {
        var now = DateTime.Now;
        var scheduledTime = now.Date.Add(timeOfDay);

        if (scheduledTime < now)
        {
            scheduledTime = scheduledTime.AddDays(1);
        }

        return scheduler.ScheduleRecurringTask(name, action, TimeSpan.FromDays(1));
    }

    public static string ScheduleHourly(
        this IBackgroundTaskScheduler scheduler,
        string name,
        Func<CancellationToken, Task> action)
    {
        return scheduler.ScheduleRecurringTask(name, action, TimeSpan.FromHours(1));
    }

    public static string ScheduleEveryMinute(
        this IBackgroundTaskScheduler scheduler,
        string name,
        Func<CancellationToken, Task> action)
    {
        return scheduler.ScheduleRecurringTask(name, action, TimeSpan.FromMinutes(1));
    }
}
