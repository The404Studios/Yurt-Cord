using System.Runtime.InteropServices;

namespace VeaMarketplace.Server.Helpers;

/// <summary>
/// Cross-platform path helper for server data directories.
/// Uses application base directory by default, but can be configured via environment variables.
///
/// Environment variables:
/// - VEA_DATA_DIR: Override the data directory (for Data/, database, etc.)
/// - VEA_UPLOAD_DIR: Override the uploads directory
/// </summary>
public static class ServerPaths
{
    private static string? s_dataDirectory;
    private static string? s_uploadDirectory;

    /// <summary>
    /// Gets the data directory for the server.
    /// Used for: database files, config files, etc.
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            if (s_dataDirectory != null)
                return s_dataDirectory;

            // Check environment variable first
            var envDataDir = Environment.GetEnvironmentVariable("VEA_DATA_DIR");
            if (!string.IsNullOrEmpty(envDataDir))
            {
                s_dataDirectory = envDataDir;
                return s_dataDirectory;
            }

            // Default to application base directory + Data
            s_dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
            return s_dataDirectory;
        }
    }

    /// <summary>
    /// Gets the uploads directory for the server.
    /// Used for: user uploads, avatars, attachments, etc.
    /// </summary>
    public static string UploadDirectory
    {
        get
        {
            if (s_uploadDirectory != null)
                return s_uploadDirectory;

            // Check environment variable first
            var envUploadDir = Environment.GetEnvironmentVariable("VEA_UPLOAD_DIR");
            if (!string.IsNullOrEmpty(envUploadDir))
            {
                s_uploadDirectory = envUploadDir;
                return s_uploadDirectory;
            }

            // Default to application base directory + uploads
            s_uploadDirectory = Path.Combine(AppContext.BaseDirectory, "uploads");
            return s_uploadDirectory;
        }
    }

    /// <summary>
    /// Gets the path for the roles configuration file.
    /// </summary>
    public static string RolesConfigPath => Path.Combine(DataDirectory, "roles-config.json");

    /// <summary>
    /// Gets the avatars upload directory.
    /// </summary>
    public static string AvatarsDirectory => Path.Combine(UploadDirectory, "avatars");

    /// <summary>
    /// Gets the banners upload directory.
    /// </summary>
    public static string BannersDirectory => Path.Combine(UploadDirectory, "banners");

    /// <summary>
    /// Gets the attachments upload directory.
    /// </summary>
    public static string AttachmentsDirectory => Path.Combine(UploadDirectory, "attachments");

    /// <summary>
    /// Gets the thumbnails directory.
    /// </summary>
    public static string ThumbnailsDirectory => Path.Combine(UploadDirectory, "thumbnails");

    /// <summary>
    /// Ensures a directory exists with proper error handling.
    /// Returns true if the directory exists or was created successfully.
    /// Logs errors using ILogger if available.
    /// </summary>
    public static bool EnsureDirectoryExists(string path, ILogger? logger = null)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                logger?.LogError("EnsureDirectoryExists: Path is null or empty");
                return false;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            Directory.CreateDirectory(path);
            logger?.LogInformation("Created directory: {Path}", path);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogError(ex, "Permission denied creating directory {Path}", path);
            return false;
        }
        catch (PathTooLongException ex)
        {
            logger?.LogError(ex, "Path too long creating directory {Path}", path);
            return false;
        }
        catch (IOException ex)
        {
            logger?.LogError(ex, "IO error creating directory {Path}", path);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating directory {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Initializes all server directories.
    /// Call this at server startup to ensure directories exist.
    /// </summary>
    public static bool InitializeDirectories(ILogger? logger = null)
    {
        var allSuccess = true;

        logger?.LogInformation("Initializing server directories...");
        logger?.LogInformation("  Data directory: {Path}", DataDirectory);
        logger?.LogInformation("  Upload directory: {Path}", UploadDirectory);

        if (!EnsureDirectoryExists(DataDirectory, logger))
        {
            logger?.LogError("Failed to create data directory: {Path}", DataDirectory);
            allSuccess = false;
        }

        if (!EnsureDirectoryExists(UploadDirectory, logger))
        {
            logger?.LogError("Failed to create upload directory: {Path}", UploadDirectory);
            allSuccess = false;
        }

        if (!EnsureDirectoryExists(AvatarsDirectory, logger))
        {
            logger?.LogError("Failed to create avatars directory: {Path}", AvatarsDirectory);
            allSuccess = false;
        }

        if (!EnsureDirectoryExists(BannersDirectory, logger))
        {
            logger?.LogError("Failed to create banners directory: {Path}", BannersDirectory);
            allSuccess = false;
        }

        if (!EnsureDirectoryExists(AttachmentsDirectory, logger))
        {
            logger?.LogError("Failed to create attachments directory: {Path}", AttachmentsDirectory);
            allSuccess = false;
        }

        if (!EnsureDirectoryExists(ThumbnailsDirectory, logger))
        {
            logger?.LogError("Failed to create thumbnails directory: {Path}", ThumbnailsDirectory);
            allSuccess = false;
        }

        if (allSuccess)
        {
            logger?.LogInformation("All server directories initialized successfully");
        }
        else
        {
            logger?.LogWarning("Some server directories could not be created");
        }

        return allSuccess;
    }

    /// <summary>
    /// Gets the database connection string for LiteDB.
    /// </summary>
    public static string GetDatabasePath(string filename = "marketplace.db")
    {
        return Path.Combine(DataDirectory, filename);
    }
}
