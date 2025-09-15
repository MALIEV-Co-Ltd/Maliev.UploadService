using Asp.Versioning;
using Maliev.UploadService.Api.Attributes;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.UploadService.Api.Controllers;

/// <summary>
/// Clean, business-agnostic file storage controller
/// Only supports path-based operations with no legacy business logic
/// API v3.0 - Clean architecture
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("uploads/v{version:apiVersion}")]
[AuthorizeFileAccess]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileStorageService fileStorageService,
        ILogger<FilesController> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file to a specific object path
    /// </summary>
    /// <param name="objectPath">The full object path where the file should be stored</param>
    /// <param name="file">The file to upload</param>
    /// <returns>Upload result</returns>
    [HttpPost("path")]
    [FileValidation]
    public async Task<ActionResult<FileUploadResponse>> UploadFileToPath(
        [FromQuery] string objectPath,
        IFormFile file)
    {
        try
        {
            var uploadedBy = GetUploadedBy();
            var ipAddress = GetClientIpAddress();

            var result = await _fileStorageService.UploadFileToPathAsync(
                objectPath, file, uploadedBy, ipAddress);

            _logger.LogInformation("Successfully uploaded file to {ObjectPath} by {UploadedBy}",
                objectPath, uploadedBy);

            return CreatedAtAction(
                nameof(GetFileByPath),
                new { objectPath = result.ObjectName },
                result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid upload request for {ObjectPath}: {Message}",
                objectPath, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to {ObjectPath}", objectPath);
            return StatusCode(500, new { error = "Upload failed" });
        }
    }

    /// <summary>
    /// Upload a file using request body with options
    /// </summary>
    /// <param name="request">The upload request</param>
    /// <returns>Upload result</returns>
    [HttpPost]
    [FileValidation]
    public async Task<ActionResult<FileUploadResponse>> UploadFile([FromForm] FileUploadRequest request)
    {
        try
        {
            if (!request.IsValid())
            {
                return BadRequest(new { error = "ObjectPath is required" });
            }

            var uploadedBy = GetUploadedBy();
            var ipAddress = GetClientIpAddress();

            var result = await _fileStorageService.UploadFileToPathAsync(
                request.ObjectPath,
                request.File,
                uploadedBy,
                ipAddress,
                request.StorageOptions,
                request.ServiceMetadata);

            _logger.LogInformation("Successfully uploaded file to {ObjectPath} by {UploadedBy}",
                request.ObjectPath, uploadedBy);

            return CreatedAtAction(
                nameof(GetFileByPath),
                new { objectPath = result.ObjectName },
                result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid upload request for {ObjectPath}: {Message}",
                request.ObjectPath, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to {ObjectPath}", request.ObjectPath);
            return StatusCode(500, new { error = "Upload failed" });
        }
    }

    /// <summary>
    /// Download a file by its object path
    /// </summary>
    /// <param name="objectPath">The object path of the file to download</param>
    /// <returns>The file content</returns>
    [HttpGet("path")]
    public async Task<IActionResult> GetFileByPath([FromQuery] string objectPath)
    {
        try
        {
            var accessedBy = GetUploadedBy();
            var ipAddress = GetClientIpAddress();

            var file = await _fileStorageService.DownloadFileByPathAsync(objectPath, accessedBy, ipAddress);
            if (file == null)
            {
                return NotFound(new { error = "File not found" });
            }

            _logger.LogInformation("File downloaded from {ObjectPath} by {AccessedBy}",
                objectPath, accessedBy);

            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid download request for {ObjectPath}: {Message}",
                objectPath, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from {ObjectPath}", objectPath);
            return StatusCode(500, new { error = "Download failed" });
        }
    }

    /// <summary>
    /// Delete a file by its object path
    /// </summary>
    /// <param name="objectPath">The object path of the file to delete</param>
    /// <returns>Success status</returns>
    [HttpDelete("path")]
    public async Task<IActionResult> DeleteFileByPath([FromQuery] string objectPath)
    {
        try
        {
            var deletedBy = GetUploadedBy();
            var ipAddress = GetClientIpAddress();

            var success = await _fileStorageService.DeleteFileByPathAsync(objectPath, deletedBy, ipAddress);
            if (!success)
            {
                return NotFound(new { error = "File not found or could not be deleted" });
            }

            _logger.LogInformation("File deleted from {ObjectPath} by {DeletedBy}",
                objectPath, deletedBy);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid delete request for {ObjectPath}: {Message}",
                objectPath, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file at {ObjectPath}", objectPath);
            return StatusCode(500, new { error = "Delete failed" });
        }
    }

    /// <summary>
    /// Check if a file exists at the specified path
    /// </summary>
    /// <param name="objectPath">The object path to check</param>
    /// <returns>HTTP 200 if exists, 404 if not</returns>
    [HttpHead("path")]
    public async Task<IActionResult> CheckFileExists([FromQuery] string objectPath)
    {
        try
        {
            var exists = await _fileStorageService.FileExistsByPathAsync(objectPath);
            return exists ? Ok() : NotFound();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid file existence check for {ObjectPath}: {Message}",
                objectPath, ex.Message);
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence at {ObjectPath}", objectPath);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Generate a signed URL for temporary access to a file
    /// </summary>
    /// <param name="objectPath">The object path of the file</param>
    /// <param name="expirationHours">Hours until the URL expires (default: 1)</param>
    /// <returns>Signed URL response</returns>
    [HttpPost("path/signed-url")]
    public async Task<ActionResult<SignedUrlResponse>> GenerateSignedUrl(
        [FromQuery] string objectPath,
        [FromQuery] int expirationHours = 1)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return BadRequest(new { error = "objectPath is required" });
            }

            if (expirationHours < 1 || expirationHours > 24)
            {
                return BadRequest(new { error = "expirationHours must be between 1 and 24" });
            }

            var expiration = TimeSpan.FromHours(expirationHours);
            var signedUrl = await _fileStorageService.GenerateSignedUrlByPathAsync(objectPath, expiration);

            _logger.LogInformation("Generated signed URL for {ObjectPath} (expires in {Hours}h)",
                objectPath, expirationHours);

            return Ok(new SignedUrlResponse
            {
                SignedUrl = signedUrl,
                ObjectPath = objectPath,
                ExpiresAt = DateTime.UtcNow.Add(expiration)
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid signed URL request for {ObjectPath}: {Message}",
                objectPath, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for {ObjectPath}", objectPath);
            return StatusCode(500, new { error = "Failed to generate signed URL" });
        }
    }

    #region Helper Methods

    private string GetUploadedBy()
    {
        return User?.Identity?.Name ?? "anonymous";
    }

    private string GetClientIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    #endregion
}