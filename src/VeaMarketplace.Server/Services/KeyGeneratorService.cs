using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VeaMarketplace.Server.Helpers;

namespace VeaMarketplace.Server.Services;

public class ActivationKey
{
    public string Key { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByUserId { get; set; }
    public string? UsedByUsername { get; set; }
    public bool IsUsed => UsedAt.HasValue;
    public string? ClientSalt { get; set; }
}

public class KeyStore
{
    public List<ActivationKey> Keys { get; set; } = new();
    public DateTime LastGenerated { get; set; }
    public int TotalGenerated { get; set; }
}

public class KeyGeneratorService
{
    private readonly ILogger<KeyGeneratorService> _logger;
    private readonly string _keyFilePath;
    private readonly object _lock = new();
    private KeyStore _keyStore;

    private const int MaxUnusedKeys = 100;
    private const string KeyChars = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // Exclude I, O to avoid confusion
    private const string KeyDigits = "23456789"; // Exclude 0, 1 to avoid confusion

    public KeyGeneratorService(ILogger<KeyGeneratorService> logger)
    {
        _logger = logger;
        _keyFilePath = Path.Combine(ServerPaths.DataDirectory, "activation_keys.json");
        _keyStore = LoadKeyStore();

        // Generate keys on startup if needed
        EnsureMinimumKeys();
    }

    private KeyStore LoadKeyStore()
    {
        try
        {
            if (File.Exists(_keyFilePath))
            {
                var json = File.ReadAllText(_keyFilePath);
                var store = JsonSerializer.Deserialize<KeyStore>(json);
                if (store != null)
                {
                    _logger.LogInformation("Loaded {Count} activation keys from store", store.Keys.Count);
                    return store;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load key store, creating new one");
        }

        return new KeyStore();
    }

    private void SaveKeyStore()
    {
        try
        {
            ServerPaths.EnsureDirectoryExists(ServerPaths.DataDirectory);
            var json = JsonSerializer.Serialize(_keyStore, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_keyFilePath, json);
            _logger.LogDebug("Saved key store with {Count} keys", _keyStore.Keys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save key store");
        }
    }

    /// <summary>
    /// Ensures there are at least MaxUnusedKeys unused keys available
    /// </summary>
    public void EnsureMinimumKeys()
    {
        lock (_lock)
        {
            var unusedCount = _keyStore.Keys.Count(k => !k.IsUsed);

            if (unusedCount >= MaxUnusedKeys)
            {
                _logger.LogInformation("Key store has {Count} unused keys, no generation needed", unusedCount);
                return;
            }

            var keysToGenerate = MaxUnusedKeys - unusedCount;
            _logger.LogInformation("Generating {Count} new activation keys", keysToGenerate);

            for (int i = 0; i < keysToGenerate; i++)
            {
                var key = GenerateUniqueKey();
                _keyStore.Keys.Add(new ActivationKey
                {
                    Key = key,
                    GeneratedAt = DateTime.UtcNow,
                    ClientSalt = GenerateClientSalt()
                });
                _keyStore.TotalGenerated++;
            }

            _keyStore.LastGenerated = DateTime.UtcNow;
            SaveKeyStore();

            _logger.LogInformation("Generated {Count} new keys. Total unused: {Unused}",
                keysToGenerate, _keyStore.Keys.Count(k => !k.IsUsed));
        }
    }

    /// <summary>
    /// Generates a unique key in format XXX-XXX (3 chars - 3 digits)
    /// </summary>
    private string GenerateUniqueKey()
    {
        string key;
        do
        {
            key = GenerateKeyFormat();
        } while (_keyStore.Keys.Any(k => k.Key == key));

        return key;
    }

    /// <summary>
    /// Generates key in format: ABC-123 (3 letters - 3 digits)
    /// </summary>
    private string GenerateKeyFormat()
    {
        var chars = new char[7];

        // First 3 characters (letters)
        for (int i = 0; i < 3; i++)
        {
            chars[i] = KeyChars[RandomNumberGenerator.GetInt32(KeyChars.Length)];
        }

        // Dash
        chars[3] = '-';

        // Last 3 characters (digits)
        for (int i = 4; i < 7; i++)
        {
            chars[i] = KeyDigits[RandomNumberGenerator.GetInt32(KeyDigits.Length)];
        }

        return new string(chars);
    }

    /// <summary>
    /// Generates a unique salt for client encryption
    /// </summary>
    private string GenerateClientSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Validates an activation key without consuming it
    /// </summary>
    public bool IsValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        key = key.ToUpperInvariant().Trim();

        lock (_lock)
        {
            return _keyStore.Keys.Any(k => k.Key == key && !k.IsUsed);
        }
    }

    /// <summary>
    /// Validates and consumes an activation key
    /// Returns the client salt if successful, null otherwise
    /// </summary>
    public (bool success, string? clientSalt, string? message) UseKey(string key, string userId, string username)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (false, null, "Key cannot be empty");

        key = key.ToUpperInvariant().Trim();

        lock (_lock)
        {
            var activationKey = _keyStore.Keys.FirstOrDefault(k => k.Key == key);

            if (activationKey == null)
            {
                _logger.LogWarning("Invalid key attempted: {Key}", key);
                return (false, null, "Invalid activation key");
            }

            if (activationKey.IsUsed)
            {
                _logger.LogWarning("Already used key attempted: {Key} by {User}", key, username);
                return (false, null, "This key has already been used");
            }

            // Mark key as used
            activationKey.UsedAt = DateTime.UtcNow;
            activationKey.UsedByUserId = userId;
            activationKey.UsedByUsername = username;

            SaveKeyStore();

            _logger.LogInformation("Key {Key} used by {Username} ({UserId})", key, username, userId);

            // Generate new keys if needed
            EnsureMinimumKeys();

            return (true, activationKey.ClientSalt, "Key activated successfully");
        }
    }

    /// <summary>
    /// Gets statistics about the key store
    /// </summary>
    public (int total, int unused, int used, DateTime? lastGenerated) GetStats()
    {
        lock (_lock)
        {
            return (
                _keyStore.Keys.Count,
                _keyStore.Keys.Count(k => !k.IsUsed),
                _keyStore.Keys.Count(k => k.IsUsed),
                _keyStore.LastGenerated
            );
        }
    }

    /// <summary>
    /// Gets a list of available (unused) keys - for admin purposes
    /// </summary>
    public List<string> GetAvailableKeys(int count = 10)
    {
        lock (_lock)
        {
            return _keyStore.Keys
                .Where(k => !k.IsUsed)
                .OrderByDescending(k => k.GeneratedAt)
                .Take(count)
                .Select(k => k.Key)
                .ToList();
        }
    }

    /// <summary>
    /// Admin method to generate a specific number of new keys
    /// </summary>
    public List<string> GenerateKeys(int count)
    {
        var newKeys = new List<string>();

        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                var key = GenerateUniqueKey();
                _keyStore.Keys.Add(new ActivationKey
                {
                    Key = key,
                    GeneratedAt = DateTime.UtcNow,
                    ClientSalt = GenerateClientSalt()
                });
                _keyStore.TotalGenerated++;
                newKeys.Add(key);
            }

            _keyStore.LastGenerated = DateTime.UtcNow;
            SaveKeyStore();
        }

        _logger.LogInformation("Admin generated {Count} new keys", count);
        return newKeys;
    }

    /// <summary>
    /// Admin method to revoke a key (mark as used without a user)
    /// </summary>
    public bool RevokeKey(string key)
    {
        key = key.ToUpperInvariant().Trim();

        lock (_lock)
        {
            var activationKey = _keyStore.Keys.FirstOrDefault(k => k.Key == key && !k.IsUsed);
            if (activationKey == null)
                return false;

            activationKey.UsedAt = DateTime.UtcNow;
            activationKey.UsedByUserId = "REVOKED";
            activationKey.UsedByUsername = "REVOKED";

            SaveKeyStore();
            _logger.LogInformation("Key {Key} revoked by admin", key);
            return true;
        }
    }
}
