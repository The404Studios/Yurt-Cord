using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly FileService _fileService;
    private readonly AuthService _authService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly IHubContext<ContentHub> _contentHubContext;

    public FileController(
        FileService fileService,
        AuthService authService,
        IHubContext<ChatHub> chatHubContext,
        IHubContext<ContentHub> contentHubContext)
    {
        _fileService = fileService;
        _authService = authService;
        _chatHubContext = chatHubContext;
        _contentHubContext = contentHubContext;
    }

    /// <summary>
    /// Upload a file attachment for chat messages
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResponse>> UploadAttachment([FromHeader(Name = "Authorization")] string? authorization)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized(new FileUploadResponse { Success = false, Message = "Unauthorized" });

        if (Request.Form.Files.Count == 0)
            return BadRequest(new FileUploadResponse { Success = false, Message = "No file provided" });

        var file = Request.Form.Files[0];
        if (file.Length == 0)
            return BadRequest(new FileUploadResponse { Success = false, Message = "Empty file" });

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            userId,
            FileCategory.Attachment
        );

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Upload a profile avatar image
    /// </summary>
    [HttpPost("avatar")]
    public async Task<ActionResult<ProfileImageUploadResponse>> UploadAvatar([FromHeader(Name = "Authorization")] string? authorization)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized(new ProfileImageUploadResponse { Success = false, Message = "Unauthorized" });

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ProfileImageUploadResponse { Success = false, Message = "No file provided" });

        var file = Request.Form.Files[0];
        if (file.Length == 0)
            return BadRequest(new ProfileImageUploadResponse { Success = false, Message = "Empty file" });

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadProfileImageAsync(
            stream,
            file.FileName,
            file.ContentType,
            userId,
            isAvatar: true
        );

        if (!result.Success)
            return BadRequest(result);

        // Update user's avatar in database
        var user = _authService.GetUserById(userId);
        if (user != null)
        {
            var oldAvatarUrl = user.AvatarUrl;
            var updateRequest = new UpdateProfileRequest { AvatarUrl = result.ImageUrl };
            _authService.UpdateProfile(userId, updateRequest);

            // Broadcast profile update to all clients via ChatHub
            await ChatHub.BroadcastProfileUpdate(_chatHubContext, user);

            // Also broadcast via ContentHub for real-time image updates everywhere
            await ContentHub.BroadcastProfilePictureUpdate(_contentHubContext, new ProfilePictureUpdateEvent
            {
                UserId = userId,
                SourceUserId = userId,
                SourceUsername = user.Username,
                SourceAvatarUrl = result.ImageUrl,
                OldAvatarUrl = oldAvatarUrl,
                NewAvatarUrl = result.ImageUrl!,
                ThumbnailUrl = result.ThumbnailUrl
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Upload a profile banner image
    /// </summary>
    [HttpPost("banner")]
    public async Task<ActionResult<ProfileImageUploadResponse>> UploadBanner([FromHeader(Name = "Authorization")] string? authorization)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized(new ProfileImageUploadResponse { Success = false, Message = "Unauthorized" });

        if (Request.Form.Files.Count == 0)
            return BadRequest(new ProfileImageUploadResponse { Success = false, Message = "No file provided" });

        var file = Request.Form.Files[0];
        if (file.Length == 0)
            return BadRequest(new ProfileImageUploadResponse { Success = false, Message = "Empty file" });

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadProfileImageAsync(
            stream,
            file.FileName,
            file.ContentType,
            userId,
            isAvatar: false
        );

        if (!result.Success)
            return BadRequest(result);

        // Update user's banner in database
        var user = _authService.GetUserById(userId);
        if (user != null)
        {
            var oldBannerUrl = user.BannerUrl;
            var updateRequest = new UpdateProfileRequest { BannerUrl = result.ImageUrl };
            _authService.UpdateProfile(userId, updateRequest);

            // Broadcast profile update to all clients via ChatHub
            await ChatHub.BroadcastProfileUpdate(_chatHubContext, user);

            // Also broadcast via ContentHub for real-time banner updates
            await ContentHub.BroadcastBannerUpdate(_contentHubContext, new BannerUpdateEvent
            {
                UserId = userId,
                SourceUserId = userId,
                SourceUsername = user.Username,
                SourceAvatarUrl = user.AvatarUrl,
                OldBannerUrl = oldBannerUrl,
                NewBannerUrl = result.ImageUrl!
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get/serve a file by path
    /// </summary>
    [HttpGet("{category}/{fileName}")]
    public IActionResult GetFile(string category, string fileName)
    {
        var uploadBasePath = _fileService.GetUploadBasePath();

        // For thumbnails, serve directly from disk (they're not stored in DB)
        if (category == "thumbnails")
        {
            var thumbnailPath = Path.Combine(uploadBasePath, "thumbnails", fileName);
            if (!System.IO.File.Exists(thumbnailPath))
                return NotFound();

            var stream = System.IO.File.OpenRead(thumbnailPath);
            return File(stream, "image/jpeg", fileName);
        }

        // Extract file ID from filename (remove extension)
        var fileId = Path.GetFileNameWithoutExtension(fileName);

        var (stream2, mimeType, originalFileName) = _fileService.GetFileStream(fileId);
        if (stream2 == null || mimeType == null)
        {
            // Fallback: try to serve file directly from disk if not found in DB
            var directPath = Path.Combine(uploadBasePath, category, fileName);
            if (System.IO.File.Exists(directPath))
            {
                var directStream = System.IO.File.OpenRead(directPath);
                var contentType = GetMimeType(fileName);
                return File(directStream, contentType, fileName);
            }
            return NotFound();
        }

        // Add cache headers for profile images
        if (category == "avatars" || category == "banners")
        {
            Response.Headers.Append("Cache-Control", "public, max-age=3600"); // 1 hour cache
        }
        else
        {
            Response.Headers.Append("Cache-Control", "public, max-age=86400"); // 24 hour cache for attachments
        }

        return File(stream2, mimeType, originalFileName);
    }

    private static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
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
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    [HttpDelete("{fileId}")]
    public ActionResult DeleteFile(string fileId, [FromHeader(Name = "Authorization")] string? authorization)
    {
        var userId = ValidateToken(authorization);
        if (userId == null)
            return Unauthorized();

        var success = _fileService.DeleteFile(fileId, userId);
        if (!success)
            return NotFound();

        return Ok(new { Success = true });
    }

    private string? ValidateToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        var user = _authService.ValidateToken(token);
        return user?.Id;
    }
}
