using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Feature flag definition
/// </summary>
public class FeatureFlag
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EnabledAt { get; set; }
    public string? EnabledBy { get; set; }
}

/// <summary>
/// Feature flag with percentage rollout
/// </summary>
public class RolloutFeatureFlag : FeatureFlag
{
    public int RolloutPercentage { get; set; } = 100; // 0-100
    public List<string>? AllowedUsers { get; set; }
    public List<string>? BlockedUsers { get; set; }
}

public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
    bool IsEnabled(string featureName, string userId);
    void Enable(string featureName, string? enabledBy = null);
    void Disable(string featureName);
    void SetRolloutPercentage(string featureName, int percentage);
    void AddToAllowlist(string featureName, string userId);
    void RemoveFromAllowlist(string featureName, string userId);
    void AddToBlocklist(string featureName, string userId);
    void RemoveFromBlocklist(string featureName, string userId);
    FeatureFlag? GetFeatureFlag(string featureName);
    Dictionary<string, FeatureFlag> GetAllFeatureFlags();
    void RegisterFeatureFlag(FeatureFlag featureFlag);
    event Action<string, bool>? OnFeatureFlagChanged;
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly Dictionary<string, RolloutFeatureFlag> _featureFlags = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Random _random = new();

    public event Action<string, bool>? OnFeatureFlagChanged;

    public FeatureFlagService()
    {
        InitializeDefaultFeatureFlags();
    }

    public bool IsEnabled(string featureName)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_featureFlags.TryGetValue(featureName, out var flag))
            {
                Debug.WriteLine($"Feature flag '{featureName}' not found, defaulting to disabled");
                return false;
            }

            return flag.IsEnabled;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool IsEnabled(string featureName, string userId)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_featureFlags.TryGetValue(featureName, out var flag))
            {
                Debug.WriteLine($"Feature flag '{featureName}' not found, defaulting to disabled");
                return false;
            }

            if (!flag.IsEnabled)
            {
                return false;
            }

            // Check blocklist first
            if (flag.BlockedUsers?.Contains(userId) == true)
            {
                return false;
            }

            // Check allowlist
            if (flag.AllowedUsers?.Contains(userId) == true)
            {
                return true;
            }

            // Check rollout percentage
            if (flag.RolloutPercentage < 100)
            {
                // Deterministic rollout based on user ID hash
                var userHash = Math.Abs(userId.GetHashCode());
                var userPercentile = userHash % 100;

                return userPercentile < flag.RolloutPercentage;
            }

            return true;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Enable(string featureName, string? enabledBy = null)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_featureFlags.ContainsKey(featureName))
            {
                _featureFlags[featureName] = new RolloutFeatureFlag
                {
                    Name = featureName,
                    IsEnabled = true,
                    EnabledAt = DateTime.UtcNow,
                    EnabledBy = enabledBy
                };
            }
            else
            {
                _featureFlags[featureName].IsEnabled = true;
                _featureFlags[featureName].EnabledAt = DateTime.UtcNow;
                _featureFlags[featureName].EnabledBy = enabledBy;
            }

            Debug.WriteLine($"Feature flag '{featureName}' enabled" + (enabledBy != null ? $" by {enabledBy}" : ""));

            OnFeatureFlagChanged?.Invoke(featureName, true);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Disable(string featureName)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_featureFlags.TryGetValue(featureName, out var flag))
            {
                flag.IsEnabled = false;

                Debug.WriteLine($"Feature flag '{featureName}' disabled");

                OnFeatureFlagChanged?.Invoke(featureName, false);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void SetRolloutPercentage(string featureName, int percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentException("Percentage must be between 0 and 100", nameof(percentage));
        }

        _lock.EnterWriteLock();
        try
        {
            if (!_featureFlags.ContainsKey(featureName))
            {
                _featureFlags[featureName] = new RolloutFeatureFlag
                {
                    Name = featureName,
                    RolloutPercentage = percentage
                };
            }
            else
            {
                _featureFlags[featureName].RolloutPercentage = percentage;
            }

            Debug.WriteLine($"Feature flag '{featureName}' rollout set to {percentage}%");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void AddToAllowlist(string featureName, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_featureFlags.ContainsKey(featureName))
            {
                _featureFlags[featureName] = new RolloutFeatureFlag
                {
                    Name = featureName,
                    AllowedUsers = new List<string> { userId }
                };
            }
            else
            {
                _featureFlags[featureName].AllowedUsers ??= new List<string>();
                if (!_featureFlags[featureName].AllowedUsers!.Contains(userId))
                {
                    _featureFlags[featureName].AllowedUsers!.Add(userId);
                }
            }

            Debug.WriteLine($"User '{userId}' added to allowlist for feature '{featureName}'");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveFromAllowlist(string featureName, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_featureFlags.TryGetValue(featureName, out var flag))
            {
                flag.AllowedUsers?.Remove(userId);
                Debug.WriteLine($"User '{userId}' removed from allowlist for feature '{featureName}'");
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void AddToBlocklist(string featureName, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_featureFlags.ContainsKey(featureName))
            {
                _featureFlags[featureName] = new RolloutFeatureFlag
                {
                    Name = featureName,
                    BlockedUsers = new List<string> { userId }
                };
            }
            else
            {
                _featureFlags[featureName].BlockedUsers ??= new List<string>();
                if (!_featureFlags[featureName].BlockedUsers!.Contains(userId))
                {
                    _featureFlags[featureName].BlockedUsers!.Add(userId);
                }
            }

            Debug.WriteLine($"User '{userId}' added to blocklist for feature '{featureName}'");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveFromBlocklist(string featureName, string userId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_featureFlags.TryGetValue(featureName, out var flag))
            {
                flag.BlockedUsers?.Remove(userId);
                Debug.WriteLine($"User '{userId}' removed from blocklist for feature '{featureName}'");
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public FeatureFlag? GetFeatureFlag(string featureName)
    {
        _lock.EnterReadLock();
        try
        {
            return _featureFlags.TryGetValue(featureName, out var flag) ? flag : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Dictionary<string, FeatureFlag> GetAllFeatureFlags()
    {
        _lock.EnterReadLock();
        try
        {
            return _featureFlags.ToDictionary(
                kvp => kvp.Key,
                kvp => (FeatureFlag)kvp.Value
            );
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void RegisterFeatureFlag(FeatureFlag featureFlag)
    {
        _lock.EnterWriteLock();
        try
        {
            var rolloutFlag = featureFlag as RolloutFeatureFlag ?? new RolloutFeatureFlag
            {
                Name = featureFlag.Name,
                IsEnabled = featureFlag.IsEnabled,
                Description = featureFlag.Description,
                Metadata = featureFlag.Metadata,
                CreatedAt = featureFlag.CreatedAt,
                EnabledAt = featureFlag.EnabledAt,
                EnabledBy = featureFlag.EnabledBy
            };

            _featureFlags[featureFlag.Name] = rolloutFlag;

            Debug.WriteLine($"Feature flag registered: {featureFlag.Name} (enabled: {featureFlag.IsEnabled})");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void InitializeDefaultFeatureFlags()
    {
        // Register default feature flags
        var defaultFlags = new[]
        {
            new RolloutFeatureFlag
            {
                Name = "ExperimentalUI",
                IsEnabled = false,
                Description = "Enable experimental UI features",
                RolloutPercentage = 0
            },
            new RolloutFeatureFlag
            {
                Name = "BetaFeatures",
                IsEnabled = false,
                Description = "Enable beta features for testing",
                RolloutPercentage = 0
            },
            new RolloutFeatureFlag
            {
                Name = "AdvancedAnalytics",
                IsEnabled = true,
                Description = "Enable advanced analytics tracking",
                RolloutPercentage = 100
            },
            new RolloutFeatureFlag
            {
                Name = "VideoCallsHD",
                IsEnabled = true,
                Description = "Enable HD video calls",
                RolloutPercentage = 100
            },
            new RolloutFeatureFlag
            {
                Name = "ScreenShareHD",
                IsEnabled = true,
                Description = "Enable HD screen sharing",
                RolloutPercentage = 100
            }
        };

        foreach (var flag in defaultFlags)
        {
            RegisterFeatureFlag(flag);
        }

        Debug.WriteLine($"Initialized {defaultFlags.Length} default feature flags");
    }
}

/// <summary>
/// Extension methods for feature flags
/// </summary>
public static class FeatureFlagExtensions
{
    public static T? WhenEnabled<T>(
        this IFeatureFlagService featureFlags,
        string featureName,
        Func<T> enabledFunc,
        Func<T>? disabledFunc = null)
    {
        if (featureFlags.IsEnabled(featureName))
        {
            return enabledFunc();
        }

        return disabledFunc != null ? disabledFunc() : default;
    }

    public static void WhenEnabled(
        this IFeatureFlagService featureFlags,
        string featureName,
        Action enabledAction,
        Action? disabledAction = null)
    {
        if (featureFlags.IsEnabled(featureName))
        {
            enabledAction();
        }
        else
        {
            disabledAction?.Invoke();
        }
    }
}
