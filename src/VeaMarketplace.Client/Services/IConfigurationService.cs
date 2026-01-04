using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Configuration value with metadata
/// </summary>
public class ConfigurationValue
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public Type? ValueType { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
}

public interface IConfigurationService
{
    T? Get<T>(string key, T? defaultValue = default);
    void Set<T>(string key, T value, string? description = null);
    bool TryGet<T>(string key, out T? value);
    bool Exists(string key);
    void Remove(string key);
    void Clear();
    Task<bool> SaveAsync();
    Task<bool> LoadAsync();
    Task<bool> ReloadAsync();
    Dictionary<string, object?> GetAll();
    event Action<string, object?>? OnConfigurationChanged;
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private readonly Dictionary<string, ConfigurationValue> _configurations = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Cached JSON serializer options for performance
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public event Action<string, object?>? OnConfigurationChanged;

    public ConfigurationService(string? configFilePath = null)
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YurtCord",
            "Config"
        );

        Directory.CreateDirectory(appDataPath);

        _configFilePath = configFilePath ?? Path.Combine(appDataPath, "app_config.json");

        // Load configuration on startup
        _ = LoadAsync();
    }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (_configurations.TryGetValue(key, out var configValue))
            {
                try
                {
                    if (configValue.Value is JsonElement jsonElement)
                    {
                        return jsonElement.Deserialize<T>();
                    }

                    return (T?)Convert.ChangeType(configValue.Value, typeof(T));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to convert config value '{key}' to type {typeof(T).Name}: {ex.Message}");
                    return defaultValue;
                }
            }

            return defaultValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Set<T>(string key, T value, string? description = null)
    {
        _lock.EnterWriteLock();
        try
        {
            var oldValue = _configurations.ContainsKey(key) ? _configurations[key].Value : null;

            _configurations[key] = new ConfigurationValue
            {
                Key = key,
                Value = value,
                ValueType = typeof(T),
                LastModified = DateTime.UtcNow,
                Description = description
            };

            Debug.WriteLine($"Configuration '{key}' set to: {value}");

            // Notify change
            OnConfigurationChanged?.Invoke(key, value);

            // Auto-save after changes
            _ = SaveAsync();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryGet<T>(string key, out T? value)
    {
        _lock.EnterReadLock();
        try
        {
            if (_configurations.TryGetValue(key, out var configValue))
            {
                try
                {
                    if (configValue.Value is JsonElement jsonElement)
                    {
                        value = jsonElement.Deserialize<T>();
                        return true;
                    }

                    value = (T?)Convert.ChangeType(configValue.Value, typeof(T));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to convert config value '{key}': {ex.Message}");
                    value = default;
                    return false;
                }
            }

            value = default;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool Exists(string key)
    {
        _lock.EnterReadLock();
        try
        {
            return _configurations.ContainsKey(key);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Remove(string key)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_configurations.Remove(key))
            {
                Debug.WriteLine($"Configuration '{key}' removed");
                OnConfigurationChanged?.Invoke(key, null);
                _ = SaveAsync();
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _configurations.Clear();
            Debug.WriteLine("All configurations cleared");
            _ = SaveAsync();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task<bool> SaveAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            Dictionary<string, ConfigurationValue> configCopy;

            _lock.EnterReadLock();
            try
            {
                configCopy = _configurations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            var json = JsonSerializer.Serialize(configCopy, JsonOptions);

            await File.WriteAllTextAsync(_configFilePath, json);

            Debug.WriteLine($"Configuration saved to: {_configFilePath} ({configCopy.Count} entries)");

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> LoadAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_configFilePath))
            {
                Debug.WriteLine("No configuration file found, starting with empty configuration");
                return true;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var loadedConfigs = JsonSerializer.Deserialize<Dictionary<string, ConfigurationValue>>(json);

            if (loadedConfigs != null)
            {
                _lock.EnterWriteLock();
                try
                {
                    _configurations.Clear();

                    foreach (var kvp in loadedConfigs)
                    {
                        _configurations[kvp.Key] = kvp.Value;
                    }

                    Debug.WriteLine($"Configuration loaded from: {_configFilePath} ({_configurations.Count} entries)");
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load configuration: {ex.Message}");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> ReloadAsync()
    {
        Debug.WriteLine("Reloading configuration from disk...");
        return await LoadAsync();
    }

    public Dictionary<string, object?> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _configurations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Value
            );
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

/// <summary>
/// Extension methods for configuration service
/// </summary>
public static class ConfigurationServiceExtensions
{
    public static T GetOrDefault<T>(this IConfigurationService config, string key, T defaultValue)
    {
        return config.Get(key, defaultValue) ?? defaultValue;
    }

    public static void SetIfNotExists<T>(this IConfigurationService config, string key, T value)
    {
        if (!config.Exists(key))
        {
            config.Set(key, value);
        }
    }

    public static void Update<T>(this IConfigurationService config, string key, Func<T?, T> updateFunc)
    {
        var currentValue = config.Get<T>(key);
        var newValue = updateFunc(currentValue);
        config.Set(key, newValue);
    }
}
