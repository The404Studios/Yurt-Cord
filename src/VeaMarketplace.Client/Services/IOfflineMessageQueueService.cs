using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VeaMarketplace.Client.Helpers;

namespace VeaMarketplace.Client.Services;

public class QueuedMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string? RecipientId { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public bool IsDirectMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public interface IOfflineMessageQueueService
{
    Task EnqueueMessageAsync(QueuedMessage message);
    Task<List<QueuedMessage>> GetPendingMessagesAsync();
    Task<bool> ProcessQueueAsync(Func<QueuedMessage, Task<bool>> sendMessageFunc);
    Task<bool> RemoveMessageAsync(string messageId);
    Task<int> ClearQueueAsync();
    Task<int> GetQueueCountAsync();
    event Action<int>? OnQueueSizeChanged;
}

public class OfflineMessageQueueService : IOfflineMessageQueueService
{
    private const int MaxRetryCount = 5;
    private const int MaxQueueSize = 1000;
    private readonly string _queueFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly List<QueuedMessage> _messageQueue = new();
    private readonly ReaderWriterLockSlim _queueLock = new();

    public event Action<int>? OnQueueSizeChanged;

    public OfflineMessageQueueService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YurtCord",
            "Data"
        );

        Directory.CreateDirectory(appDataPath);

        _queueFilePath = Path.Combine(appDataPath, "message_queue.json");

        // Load queue from disk on startup
        _ = LoadQueueFromDiskAsync();
    }

    public async Task EnqueueMessageAsync(QueuedMessage message)
    {
        _queueLock.EnterWriteLock();
        try
        {
            // Prevent queue from growing too large
            if (_messageQueue.Count >= MaxQueueSize)
            {
                Debug.WriteLine("Message queue is full, removing oldest message");
                _messageQueue.RemoveAt(0);
            }

            _messageQueue.Add(message);
            Debug.WriteLine($"Message queued: {message.Id} (Queue size: {_messageQueue.Count})");

            OnQueueSizeChanged?.Invoke(_messageQueue.Count);
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }

        await SaveQueueToDiskAsync();
    }

    public async Task<List<QueuedMessage>> GetPendingMessagesAsync()
    {
        await Task.CompletedTask;

        _queueLock.EnterReadLock();
        try
        {
            return _messageQueue.ToList();
        }
        finally
        {
            _queueLock.ExitReadLock();
        }
    }

    public async Task<bool> ProcessQueueAsync(Func<QueuedMessage, Task<bool>> sendMessageFunc)
    {
        var messagesToProcess = await GetPendingMessagesAsync();

        if (messagesToProcess.Count == 0)
        {
            return true;
        }

        Debug.WriteLine($"Processing {messagesToProcess.Count} queued messages");

        var successCount = 0;
        var failureCount = 0;

        foreach (var message in messagesToProcess)
        {
            try
            {
                var success = await sendMessageFunc(message);

                if (success)
                {
                    await RemoveMessageAsync(message.Id);
                    successCount++;
                    Debug.WriteLine($"Successfully sent queued message: {message.Id}");
                }
                else
                {
                    // Increment retry count
                    _queueLock.EnterWriteLock();
                    try
                    {
                        var queuedMsg = _messageQueue.FirstOrDefault(m => m.Id == message.Id);
                        if (queuedMsg != null)
                        {
                            queuedMsg.RetryCount++;

                            // Remove if max retries exceeded
                            if (queuedMsg.RetryCount >= MaxRetryCount)
                            {
                                Debug.WriteLine($"Message {message.Id} exceeded max retries, removing from queue");
                                _messageQueue.Remove(queuedMsg);
                                OnQueueSizeChanged?.Invoke(_messageQueue.Count);
                            }
                        }
                    }
                    finally
                    {
                        _queueLock.ExitWriteLock();
                    }

                    failureCount++;
                    Debug.WriteLine($"Failed to send queued message: {message.Id}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing queued message {message.Id}: {ex.Message}");
                failureCount++;
            }
        }

        await SaveQueueToDiskAsync();

        Debug.WriteLine($"Queue processing complete: {successCount} succeeded, {failureCount} failed");

        return failureCount == 0;
    }

    public async Task<bool> RemoveMessageAsync(string messageId)
    {
        bool removed = false;

        _queueLock.EnterWriteLock();
        try
        {
            var message = _messageQueue.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                _messageQueue.Remove(message);
                removed = true;
                Debug.WriteLine($"Message removed from queue: {messageId}");

                OnQueueSizeChanged?.Invoke(_messageQueue.Count);
            }
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }

        if (removed)
        {
            await SaveQueueToDiskAsync();
        }

        return removed;
    }

    public async Task<int> ClearQueueAsync()
    {
        int count;

        _queueLock.EnterWriteLock();
        try
        {
            count = _messageQueue.Count;
            _messageQueue.Clear();
            Debug.WriteLine($"Message queue cleared: {count} messages removed");

            OnQueueSizeChanged?.Invoke(0);
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }

        await SaveQueueToDiskAsync();

        return count;
    }

    public async Task<int> GetQueueCountAsync()
    {
        await Task.CompletedTask;

        _queueLock.EnterReadLock();
        try
        {
            return _messageQueue.Count;
        }
        finally
        {
            _queueLock.ExitReadLock();
        }
    }

    private async Task SaveQueueToDiskAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            List<QueuedMessage> queueCopy;

            _queueLock.EnterReadLock();
            try
            {
                queueCopy = _messageQueue.ToList();
            }
            finally
            {
                _queueLock.ExitReadLock();
            }

            var json = JsonSerializer.Serialize(queueCopy, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_queueFilePath, json);

            Debug.WriteLine($"Message queue saved to disk: {queueCopy.Count} messages");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save message queue to disk: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task LoadQueueFromDiskAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_queueFilePath))
            {
                Debug.WriteLine("No message queue file found, starting with empty queue");
                return;
            }

            var json = await File.ReadAllTextAsync(_queueFilePath);
            var loadedMessages = JsonSerializer.Deserialize<List<QueuedMessage>>(json);

            if (loadedMessages != null)
            {
                _queueLock.EnterWriteLock();
                try
                {
                    _messageQueue.Clear();
                    _messageQueue.AddRange(loadedMessages);

                    Debug.WriteLine($"Message queue loaded from disk: {_messageQueue.Count} messages");

                    OnQueueSizeChanged?.Invoke(_messageQueue.Count);
                }
                finally
                {
                    _queueLock.ExitWriteLock();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load message queue from disk: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
