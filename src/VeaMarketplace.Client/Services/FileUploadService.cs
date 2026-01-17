using System.Net.Http;
using System.Net.Http.Headers;
using VeaMarketplace.Shared.DTOs;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for uploading files to the server
/// </summary>
public interface IFileUploadService : IDisposable
{
    Task<FileUploadResponse> UploadAttachmentAsync(string filePath, string token);
    Task<ProfileImageUploadResponse> UploadAvatarAsync(string filePath, string token);
    Task<ProfileImageUploadResponse> UploadBannerAsync(string filePath, string token);
}

public class FileUploadService : IFileUploadService
{
    private readonly HttpClient _httpClient;
    private static readonly string BaseUrl = AppConstants.Api.GetFilesUrl();
    private bool _disposed;

    // Shared JSON options for consistent serialization
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileUploadService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // 5 minutes for large files
        };
    }

    /// <summary>
    /// Disposes the HttpClient to release network resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Upload a file attachment for chat messages
    /// </summary>
    public async Task<FileUploadResponse> UploadAttachmentAsync(string filePath, string token)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);

            var fileName = Path.GetFileName(filePath);
            var mimeType = GetMimeType(filePath);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/upload")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            // Handle empty or non-JSON responses
            if (string.IsNullOrWhiteSpace(json))
            {
                return new FileUploadResponse
                {
                    Success = false,
                    Message = $"Server returned empty response (HTTP {(int)response.StatusCode})"
                };
            }

            // Check if response is JSON (should start with { or [)
            var trimmed = json.TrimStart();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return new FileUploadResponse
                {
                    Success = false,
                    Message = $"Server error (HTTP {(int)response.StatusCode}): {json.Substring(0, Math.Min(200, json.Length))}"
                };
            }

            return JsonSerializer.Deserialize<FileUploadResponse>(json, JsonOptions)
                ?? new FileUploadResponse { Success = false, Message = "Failed to parse response" };
        }
        catch (Exception ex)
        {
            return new FileUploadResponse
            {
                Success = false,
                Message = $"Upload failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Upload an avatar image
    /// </summary>
    public async Task<ProfileImageUploadResponse> UploadAvatarAsync(string filePath, string token)
    {
        return await UploadProfileImageAsync(filePath, token, "avatar");
    }

    /// <summary>
    /// Upload a banner image
    /// </summary>
    public async Task<ProfileImageUploadResponse> UploadBannerAsync(string filePath, string token)
    {
        return await UploadProfileImageAsync(filePath, token, "banner");
    }

    private async Task<ProfileImageUploadResponse> UploadProfileImageAsync(string filePath, string token, string endpoint)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);

            var fileName = Path.GetFileName(filePath);
            var mimeType = GetMimeType(filePath);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            // Handle empty or non-JSON responses
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ProfileImageUploadResponse
                {
                    Success = false,
                    Message = $"Server returned empty response (HTTP {(int)response.StatusCode})"
                };
            }

            // Check if response is JSON (should start with { or [)
            var trimmed = json.TrimStart();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return new ProfileImageUploadResponse
                {
                    Success = false,
                    Message = $"Server error (HTTP {(int)response.StatusCode}): {json.Substring(0, Math.Min(200, json.Length))}"
                };
            }

            return JsonSerializer.Deserialize<ProfileImageUploadResponse>(json, JsonOptions)
                ?? new ProfileImageUploadResponse { Success = false, Message = "Failed to parse response" };
        }
        catch (Exception ex)
        {
            return new ProfileImageUploadResponse
            {
                Success = false,
                Message = $"Upload failed: {ex.Message}"
            };
        }
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
