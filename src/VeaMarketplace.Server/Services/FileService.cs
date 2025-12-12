using LiteDB;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// Service for handling file uploads, storage, and serving files.
/// Files are stored on disk and metadata is stored in the database.
/// </summary>
public class FileService
{
    private readonly DatabaseService _db;
    private readonly ILiteCollection<StoredFile> _files;
    private readonly string _uploadPath;
    private readonly string _baseUrl;

    // Maximum file sizes (in bytes)
    private const long MaxImageSize = 10 * 1024 * 1024;    // 10 MB
    private const long MaxVideoSize = 100 * 1024 * 1024;   // 100 MB
    private const long MaxAudioSize = 50 * 1024 * 1024;    // 50 MB
    private const long MaxDocumentSize = 25 * 1024 * 1024; // 25 MB
    private const long MaxFileSize = 50 * 1024 * 1024;     // 50 MB general

    // Allowed MIME types
    private static readonly HashSet<string> AllowedImageTypes = new()
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/bmp",
        "image/x-icon", "image/vnd.microsoft.icon", "image/ico"
    };

    private static readonly HashSet<string> AllowedVideoTypes = new()
    {
        "video/mp4", "video/webm", "video/quicktime", "video/x-msvideo"
    };

    private static readonly HashSet<string> AllowedAudioTypes = new()
    {
        "audio/mpeg", "audio/mp3", "audio/wav", "audio/ogg", "audio/webm", "audio/aac"
    };

    private static readonly HashSet<string> AllowedDocumentTypes = new()
    {
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "text/csv", "application/json"
    };

    public FileService(DatabaseService db, IConfiguration configuration)
    {
        _db = db;
        _files = _db.StoredFiles;

        // Get configuration for file storage
        var configuredPath = configuration.GetValue<string>("FileStorage:UploadPath") ?? "uploads";

        // Ensure the upload path is absolute to avoid issues when serving files
        if (Path.IsPathRooted(configuredPath))
        {
            _uploadPath = configuredPath;
        }
        else
        {
            // Convert relative path to absolute based on current directory
            _uploadPath = Path.GetFullPath(configuredPath);
        }

        _baseUrl = configuration.GetValue<string>("FileStorage:BaseUrl") ?? "http://162.248.94.23:5000/api/files";

        // Ensure upload directories exist
        EnsureDirectoriesExist();

        // Create indexes
        _files.EnsureIndex(x => x.UploaderId);
        _files.EnsureIndex(x => x.Hash);
        _files.EnsureIndex(x => x.CreatedAt);
    }

    private void EnsureDirectoriesExist()
    {
        var directories = new[] { "avatars", "banners", "attachments", "thumbnails" };
        foreach (var dir in directories)
        {
            var path = Path.Combine(_uploadPath, dir);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    /// <summary>
    /// Upload a file and store it on disk
    /// </summary>
    public async Task<FileUploadResponse> UploadFileAsync(Stream fileStream, string fileName, string mimeType, string uploaderId, FileCategory category)
    {
        try
        {
            // Validate file type and size
            var attachmentType = GetAttachmentType(mimeType);
            var maxSize = GetMaxSizeForType(attachmentType);

            // Read file into memory to get size and hash
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            if (fileBytes.Length > maxSize)
            {
                return new FileUploadResponse
                {
                    Success = false,
                    Message = $"File size exceeds maximum allowed size of {maxSize / (1024 * 1024)} MB"
                };
            }

            // Calculate file hash for deduplication
            var hash = ComputeHash(fileBytes);

            // Check if file already exists (deduplication)
            var existingFile = _files.FindOne(f => f.Hash == hash && f.UploaderId == uploaderId);
            if (existingFile != null)
            {
                // Return existing file URL
                return new FileUploadResponse
                {
                    Success = true,
                    FileId = existingFile.Id,
                    FileUrl = existingFile.Url,
                    ThumbnailUrl = existingFile.ThumbnailUrl,
                    FileName = existingFile.OriginalFileName,
                    FileSize = existingFile.Size,
                    MimeType = existingFile.MimeType,
                    FileType = attachmentType,
                    Width = existingFile.Width,
                    Height = existingFile.Height
                };
            }

            // Generate unique filename
            var fileId = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var storedFileName = $"{fileId}{extension}";

            // Determine subdirectory based on category
            var subDirectory = category switch
            {
                FileCategory.Avatar => "avatars",
                FileCategory.Banner => "banners",
                FileCategory.Attachment => "attachments",
                _ => "attachments"
            };

            var filePath = Path.Combine(_uploadPath, subDirectory, storedFileName);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, fileBytes);

            // Get image dimensions if applicable
            int? width = null;
            int? height = null;
            string? thumbnailUrl = null;

            if (attachmentType == AttachmentType.Image)
            {
                (width, height) = GetImageDimensions(fileBytes);

                // Generate thumbnail for images
                thumbnailUrl = await GenerateThumbnailAsync(fileBytes, fileId, extension);
            }

            // Create file URL
            var fileUrl = $"{_baseUrl}/{subDirectory}/{storedFileName}";

            // Store metadata in database
            var storedFile = new StoredFile
            {
                Id = fileId,
                OriginalFileName = fileName,
                StoredFileName = storedFileName,
                MimeType = mimeType,
                Size = fileBytes.Length,
                Hash = hash,
                Url = fileUrl,
                ThumbnailUrl = thumbnailUrl,
                Path = filePath,
                UploaderId = uploaderId,
                Category = category,
                AttachmentType = attachmentType,
                Width = width,
                Height = height,
                CreatedAt = DateTime.UtcNow
            };

            _files.Insert(storedFile);

            return new FileUploadResponse
            {
                Success = true,
                FileId = fileId,
                FileUrl = fileUrl,
                ThumbnailUrl = thumbnailUrl,
                FileName = fileName,
                FileSize = fileBytes.Length,
                MimeType = mimeType,
                FileType = attachmentType,
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResponse
            {
                Success = false,
                Message = $"Failed to upload file: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Upload a profile image (avatar or banner)
    /// </summary>
    public async Task<ProfileImageUploadResponse> UploadProfileImageAsync(Stream fileStream, string fileName, string mimeType, string userId, bool isAvatar)
    {
        // Validate that it's an image
        if (!AllowedImageTypes.Contains(mimeType.ToLowerInvariant()))
        {
            return new ProfileImageUploadResponse
            {
                Success = false,
                Message = "Only image files are allowed for profile pictures"
            };
        }

        var category = isAvatar ? FileCategory.Avatar : FileCategory.Banner;
        var result = await UploadFileAsync(fileStream, fileName, mimeType, userId, category);

        if (!result.Success)
        {
            return new ProfileImageUploadResponse
            {
                Success = false,
                Message = result.Message
            };
        }

        return new ProfileImageUploadResponse
        {
            Success = true,
            ImageUrl = result.FileUrl,
            ThumbnailUrl = result.ThumbnailUrl
        };
    }

    /// <summary>
    /// Get file by ID
    /// </summary>
    public StoredFile? GetFile(string fileId)
    {
        return _files.FindById(fileId);
    }

    /// <summary>
    /// Get the base upload path for file storage
    /// </summary>
    public string GetUploadBasePath()
    {
        return _uploadPath;
    }

    /// <summary>
    /// Get file stream by ID
    /// </summary>
    public (Stream? stream, string? mimeType, string? fileName) GetFileStream(string fileId)
    {
        var file = _files.FindById(fileId);
        if (file == null || !File.Exists(file.Path))
            return (null, null, null);

        var stream = File.OpenRead(file.Path);
        return (stream, file.MimeType, file.OriginalFileName);
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    public bool DeleteFile(string fileId, string requesterId)
    {
        var file = _files.FindById(fileId);
        if (file == null)
            return false;

        // Only allow owner to delete
        if (file.UploaderId != requesterId)
            return false;

        // Delete from disk
        if (File.Exists(file.Path))
            File.Delete(file.Path);

        // Delete thumbnail if exists
        if (!string.IsNullOrEmpty(file.ThumbnailUrl))
        {
            var thumbnailPath = Path.Combine(_uploadPath, "thumbnails", $"{fileId}_thumb.jpg");
            if (File.Exists(thumbnailPath))
                File.Delete(thumbnailPath);
        }

        // Delete from database
        _files.Delete(fileId);
        return true;
    }

    private AttachmentType GetAttachmentType(string mimeType)
    {
        var mime = mimeType.ToLowerInvariant();

        if (AllowedImageTypes.Contains(mime))
            return AttachmentType.Image;
        if (AllowedVideoTypes.Contains(mime))
            return AttachmentType.Video;
        if (AllowedAudioTypes.Contains(mime))
            return AttachmentType.Audio;
        if (AllowedDocumentTypes.Contains(mime))
            return AttachmentType.Document;

        return AttachmentType.File;
    }

    private long GetMaxSizeForType(AttachmentType type)
    {
        return type switch
        {
            AttachmentType.Image => MaxImageSize,
            AttachmentType.Video => MaxVideoSize,
            AttachmentType.Audio => MaxAudioSize,
            AttachmentType.Document => MaxDocumentSize,
            _ => MaxFileSize
        };
    }

    private string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes);
    }

    [SupportedOSPlatform("windows")]
    private (int? width, int? height) GetImageDimensions(byte[] imageData)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var image = System.Drawing.Image.FromStream(ms);
            return (image.Width, image.Height);
        }
        catch
        {
            return (null, null);
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<string?> GenerateThumbnailAsync(byte[] imageData, string fileId, string extension)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var original = System.Drawing.Image.FromStream(ms);

            // Calculate thumbnail size (max 200px)
            const int maxSize = 200;
            var ratio = Math.Min((double)maxSize / original.Width, (double)maxSize / original.Height);
            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);

            using var thumbnail = original.GetThumbnailImage(newWidth, newHeight, () => false, IntPtr.Zero);

            var thumbnailFileName = $"{fileId}_thumb.jpg";
            var thumbnailPath = Path.Combine(_uploadPath, "thumbnails", thumbnailFileName);

            thumbnail.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Jpeg);

            return await Task.FromResult($"{_baseUrl}/thumbnails/{thumbnailFileName}");
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Stored file metadata
/// </summary>
public class StoredFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string Path { get; set; } = string.Empty;
    public string UploaderId { get; set; } = string.Empty;
    public FileCategory Category { get; set; }
    public AttachmentType AttachmentType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FileCategory
{
    Avatar,
    Banner,
    Attachment
}
