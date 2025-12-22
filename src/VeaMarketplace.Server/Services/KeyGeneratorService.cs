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

/// <summary>
/// 6-digit whitelist code for whitelist authentication mode.
/// These codes allow users to self-whitelist by entering a valid code.
/// </summary>
public class WhitelistCode
{
    public string Code { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByUserId { get; set; }
    public string? UsedByUsername { get; set; }
    public bool IsUsed => UsedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;
    /// <summary>
    /// Optional note/description for admin reference
    /// </summary>
    public string? Note { get; set; }
}

public class KeyStore
{
    public List<ActivationKey> Keys { get; set; } = new();
    public List<WhitelistCode> WhitelistCodes { get; set; } = new();
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

    #region Whitelist Code Methods (6-digit codes)

    /// <summary>
    /// Generates a unique 6-digit whitelist code
    /// </summary>
    private string GenerateUnique6DigitCode()
    {
        string code;
        do
        {
            // Generate 6 random digits (0-9)
            code = string.Concat(Enumerable.Range(0, 6).Select(_ => RandomNumberGenerator.GetInt32(10).ToString()));
        } while (_keyStore.WhitelistCodes.Any(c => c.Code == code));

        return code;
    }

    /// <summary>
    /// Generates a new 6-digit whitelist code with optional expiration
    /// </summary>
    /// <param name="expirationHours">Hours until the code expires (default 24 hours)</param>
    /// <param name="note">Optional admin note</param>
    /// <returns>The generated 6-digit code</returns>
    public string GenerateWhitelistCode(int expirationHours = 24, string? note = null)
    {
        lock (_lock)
        {
            var code = GenerateUnique6DigitCode();

            _keyStore.WhitelistCodes.Add(new WhitelistCode
            {
                Code = code,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
                Note = note
            });

            SaveKeyStore();
            _logger.LogInformation("Generated whitelist code: {Code} (expires in {Hours}h)", code, expirationHours);

            return code;
        }
    }

    /// <summary>
    /// Generates multiple 6-digit whitelist codes
    /// </summary>
    public List<string> GenerateWhitelistCodes(int count, int expirationHours = 24, string? note = null)
    {
        var codes = new List<string>();

        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                var code = GenerateUnique6DigitCode();

                _keyStore.WhitelistCodes.Add(new WhitelistCode
                {
                    Code = code,
                    GeneratedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
                    Note = note
                });

                codes.Add(code);
            }

            SaveKeyStore();
            _logger.LogInformation("Generated {Count} whitelist codes (expires in {Hours}h)", count, expirationHours);
        }

        return codes;
    }

    /// <summary>
    /// Validates a whitelist code without consuming it
    /// </summary>
    public bool IsValidWhitelistCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        code = code.Trim();

        lock (_lock)
        {
            return _keyStore.WhitelistCodes.Any(c => c.Code == code && c.IsValid);
        }
    }

    /// <summary>
    /// Uses a whitelist code and returns success status
    /// </summary>
    public (bool success, string? message) UseWhitelistCode(string code, string userId, string username)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "Code cannot be empty");

        code = code.Trim();

        if (code.Length != 6 || !code.All(char.IsDigit))
            return (false, "Invalid code format. Code must be 6 digits.");

        lock (_lock)
        {
            var whitelistCode = _keyStore.WhitelistCodes.FirstOrDefault(c => c.Code == code);

            if (whitelistCode == null)
            {
                _logger.LogWarning("Invalid whitelist code attempted: {Code}", code);
                return (false, "Invalid whitelist code");
            }

            if (whitelistCode.IsExpired)
            {
                _logger.LogWarning("Expired whitelist code attempted: {Code} by {User}", code, username);
                return (false, "This code has expired");
            }

            if (whitelistCode.IsUsed)
            {
                _logger.LogWarning("Already used whitelist code attempted: {Code} by {User}", code, username);
                return (false, "This code has already been used");
            }

            // Mark code as used
            whitelistCode.UsedAt = DateTime.UtcNow;
            whitelistCode.UsedByUserId = userId;
            whitelistCode.UsedByUsername = username;

            SaveKeyStore();

            _logger.LogInformation("Whitelist code {Code} used by {Username} ({UserId})", code, username, userId);

            return (true, "Code activated successfully");
        }
    }

    /// <summary>
    /// Gets available (unused) whitelist codes
    /// </summary>
    public List<WhitelistCode> GetAvailableWhitelistCodes(int count = 10)
    {
        lock (_lock)
        {
            return _keyStore.WhitelistCodes
                .Where(c => c.IsValid)
                .OrderByDescending(c => c.GeneratedAt)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Gets whitelist code statistics
    /// </summary>
    public (int total, int available, int used, int expired) GetWhitelistCodeStats()
    {
        lock (_lock)
        {
            return (
                _keyStore.WhitelistCodes.Count,
                _keyStore.WhitelistCodes.Count(c => c.IsValid),
                _keyStore.WhitelistCodes.Count(c => c.IsUsed),
                _keyStore.WhitelistCodes.Count(c => c.IsExpired && !c.IsUsed)
            );
        }
    }

    /// <summary>
    /// Revokes a whitelist code
    /// </summary>
    public bool RevokeWhitelistCode(string code)
    {
        code = code.Trim();

        lock (_lock)
        {
            var whitelistCode = _keyStore.WhitelistCodes.FirstOrDefault(c => c.Code == code && c.IsValid);
            if (whitelistCode == null)
                return false;

            whitelistCode.UsedAt = DateTime.UtcNow;
            whitelistCode.UsedByUserId = "REVOKED";
            whitelistCode.UsedByUsername = "REVOKED";

            SaveKeyStore();
            _logger.LogInformation("Whitelist code {Code} revoked by admin", code);
            return true;
        }
    }

    /// <summary>
    /// Cleans up expired whitelist codes (optional maintenance)
    /// </summary>
    public int CleanupExpiredCodes()
    {
        lock (_lock)
        {
            var expiredCodes = _keyStore.WhitelistCodes
                .Where(c => c.IsExpired && !c.IsUsed)
                .ToList();

            foreach (var code in expiredCodes)
            {
                _keyStore.WhitelistCodes.Remove(code);
            }

            if (expiredCodes.Count > 0)
            {
                SaveKeyStore();
                _logger.LogInformation("Cleaned up {Count} expired whitelist codes", expiredCodes.Count);
            }

            return expiredCodes.Count;
        }
    }

    #endregion
}
