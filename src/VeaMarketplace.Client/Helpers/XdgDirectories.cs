using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace VeaMarketplace.Client.Helpers;

/// <summary>
/// Cross-platform directory helper that follows XDG Base Directory Specification on Linux/macOS.
/// On Windows, uses standard Environment.SpecialFolder values.
///
/// XDG Standards:
/// - XDG_CONFIG_HOME: User-specific config files (default: ~/.config)
/// - XDG_DATA_HOME: User-specific data files (default: ~/.local/share)
/// - XDG_CACHE_HOME: User-specific cache files (default: ~/.cache)
/// - XDG_STATE_HOME: User-specific state files (default: ~/.local/state)
/// </summary>
public static class XdgDirectories
{
    private const string AppName = "VeaMarketplace";

    /// <summary>
    /// Gets the configuration directory for the application.
    /// Use for: settings.json, user preferences, config files
    /// </summary>
    public static string ConfigHome
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppName);
            }

            // Linux/macOS: Check XDG_CONFIG_HOME, fallback to ~/.config
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfig))
            {
                return Path.Combine(xdgConfig, AppName);
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".config", AppName);
        }
    }

    /// <summary>
    /// Gets the data directory for the application.
    /// Use for: persistent data files, databases, user-created content
    /// </summary>
    public static string DataHome
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName);
            }

            // Linux/macOS: Check XDG_DATA_HOME, fallback to ~/.local/share
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgData))
            {
                return Path.Combine(xdgData, AppName);
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".local", "share", AppName);
        }
    }

    /// <summary>
    /// Gets the cache directory for the application.
    /// Use for: temporary/cached files, image caches, logs
    /// </summary>
    public static string CacheHome
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName,
                    "Cache");
            }

            // Linux/macOS: Check XDG_CACHE_HOME, fallback to ~/.cache
            var xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrEmpty(xdgCache))
            {
                return Path.Combine(xdgCache, AppName);
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".cache", AppName);
        }
    }

    /// <summary>
    /// Gets the state directory for the application.
    /// Use for: state files that should persist between restarts but aren't config
    /// </summary>
    public static string StateHome
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName,
                    "State");
            }

            // Linux/macOS: Check XDG_STATE_HOME, fallback to ~/.local/state
            var xdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (!string.IsNullOrEmpty(xdgState))
            {
                return Path.Combine(xdgState, AppName);
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".local", "state", AppName);
        }
    }

    /// <summary>
    /// Gets the log directory for the application.
    /// </summary>
    public static string LogDirectory => Path.Combine(CacheHome, "logs");

    /// <summary>
    /// Gets the image cache directory for the application.
    /// </summary>
    public static string ImageCacheDirectory => Path.Combine(CacheHome, "ImageCache");

    /// <summary>
    /// Ensures a directory exists with proper error handling.
    /// Returns true if the directory exists or was created successfully.
    /// </summary>
    public static bool EnsureDirectoryExists(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.WriteLine("EnsureDirectoryExists: Path is null or empty");
                return false;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);
            Debug.WriteLine($"Created directory: {path}");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Permission denied creating directory {path}: {ex.Message}");
            return false;
        }
        catch (PathTooLongException ex)
        {
            Debug.WriteLine($"Path too long creating directory {path}: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"IO error creating directory {path}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating directory {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets a path within the config directory.
    /// </summary>
    public static string GetConfigPath(string relativePath)
    {
        return Path.Combine(ConfigHome, relativePath);
    }

    /// <summary>
    /// Gets a path within the data directory.
    /// </summary>
    public static string GetDataPath(string relativePath)
    {
        return Path.Combine(DataHome, relativePath);
    }

    /// <summary>
    /// Gets a path within the cache directory.
    /// </summary>
    public static string GetCachePath(string relativePath)
    {
        return Path.Combine(CacheHome, relativePath);
    }

    /// <summary>
    /// Gets the crash log path.
    /// </summary>
    public static string CrashLogPath => Path.Combine(LogDirectory, "crash.log");

    /// <summary>
    /// Initializes all application directories.
    /// Call this at application startup to ensure directories exist.
    /// </summary>
    public static void InitializeDirectories()
    {
        EnsureDirectoryExists(ConfigHome);
        EnsureDirectoryExists(DataHome);
        EnsureDirectoryExists(CacheHome);
        EnsureDirectoryExists(StateHome);
        EnsureDirectoryExists(LogDirectory);
        EnsureDirectoryExists(ImageCacheDirectory);

        Debug.WriteLine($"XDG Directories initialized:");
        Debug.WriteLine($"  Config: {ConfigHome}");
        Debug.WriteLine($"  Data: {DataHome}");
        Debug.WriteLine($"  Cache: {CacheHome}");
        Debug.WriteLine($"  State: {StateHome}");
    }
}
