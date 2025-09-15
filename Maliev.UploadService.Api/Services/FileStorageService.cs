using Maliev.UploadService.Api.Models;
using Microsoft.Extensions.Options;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Clean, business-agnostic file storage service
/// Only supports path-based uploads with no legacy business logic
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly IGoogleCloudStorageService _storageService;
    private readonly StorageServiceOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IGoogleCloudStorageService storageService,
        IOptions<StorageServiceOptions> options,
        ILogger<FileStorageService> logger)
    {
        _storageService = storageService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileUploadResponse> UploadFileToPathAsync(
        string objectPath,
        IFormFile file,
        string uploadedBy,
        string ipAddress = "",
        StorageOptions? options = null,
        Dictionary<string, string>? metadata = null)
    {
        ValidateObjectPath(objectPath);
        ValidateFile(file);

        var bucketName = options?.BucketName ?? _options.DefaultBucketName;
        var contentType = options?.ContentType ?? file.ContentType;
        var sanitizedPath = SanitizeObjectPath(objectPath);
        var finalObjectPath = EnsureUniqueObjectPath(sanitizedPath);

        _logger.LogInformation("Starting file upload to: {ObjectPath}", finalObjectPath);

        try
        {
            // Calculate file hashes
            using var fileStream = file.OpenReadStream();
            var (md5Hash, sha256Hash) = await _storageService.CalculateHashesAsync(fileStream);

            // Upload to Google Cloud Storage
            var etag = await _storageService.UploadFileAsync(bucketName, finalObjectPath, fileStream, contentType);

            _logger.LogInformation("Successfully uploaded file to: {ObjectPath}", finalObjectPath);

            return new FileUploadResponse
            {
                FileId = Guid.NewGuid(), // Generate ID for tracking
                ObjectName = finalObjectPath,
                Bucket = bucketName,
                FileSize = file.Length,
                ContentType = contentType,
                UploadedAt = DateTime.UtcNow,
                Category = "", // No longer used
                EntityId = "", // No longer used
                AccessLevel = AccessLevel.Internal, // Default
                ProcessingStatus = ProcessingStatus.Completed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to: {ObjectPath}", finalObjectPath);
            throw;
        }
    }

    public async Task<FileDownloadResponse?> DownloadFileByPathAsync(string objectPath, string accessedBy, string ipAddress = "")
    {
        ValidateObjectPath(objectPath);

        var bucketName = _options.DefaultBucketName;
        _logger.LogInformation("Downloading file from path: {ObjectPath}", objectPath);

        try
        {
            return await _storageService.DownloadFileAsync(bucketName, objectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from: {ObjectPath}", objectPath);
            throw;
        }
    }

    public async Task<bool> DeleteFileByPathAsync(string objectPath, string deletedBy, string ipAddress = "")
    {
        ValidateObjectPath(objectPath);

        var bucketName = _options.DefaultBucketName;
        _logger.LogInformation("Deleting file at path: {ObjectPath}", objectPath);

        try
        {
            return await _storageService.DeleteFileAsync(bucketName, objectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file at: {ObjectPath}", objectPath);
            return false;
        }
    }

    public async Task<bool> FileExistsByPathAsync(string objectPath)
    {
        ValidateObjectPath(objectPath);

        var bucketName = _options.DefaultBucketName;

        try
        {
            return await _storageService.FileExistsAsync(bucketName, objectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence at: {ObjectPath}", objectPath);
            return false;
        }
    }

    public async Task<string> GenerateSignedUrlByPathAsync(string objectPath, TimeSpan? expiration = null)
    {
        ValidateObjectPath(objectPath);

        var bucketName = _options.DefaultBucketName;
        var exp = expiration ?? TimeSpan.FromHours(1);

        try
        {
            return await _storageService.GenerateSignedUrlAsync(bucketName, objectPath, exp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for: {ObjectPath}", objectPath);
            throw;
        }
    }

    public async Task<List<FileMetadataResponse>> ListFilesByPathPatternAsync(string pathPattern, int maxResults = 100)
    {
        // This would require Google Cloud Storage list operations
        // For now, return empty list as this would need additional GCS client methods
        _logger.LogWarning("ListFilesByPathPatternAsync not yet implemented for pattern: {Pattern}", pathPattern);
        await Task.CompletedTask;
        return new List<FileMetadataResponse>();
    }

    // Helper methods
    private void ValidateObjectPath(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            throw new ArgumentException("Object path cannot be null or empty", nameof(objectPath));
        }

        if (objectPath.Length > 500)
        {
            throw new ArgumentException("Object path cannot exceed 500 characters", nameof(objectPath));
        }

        // Check for invalid characters
        var invalidChars = new[] { "..", "\\", "<", ">", ":", "\"", "|", "?", "*" };
        if (invalidChars.Any(objectPath.Contains))
        {
            throw new ArgumentException("Object path contains invalid characters", nameof(objectPath));
        }
    }

    private void ValidateFile(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("File is empty", nameof(file));
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size exceeds maximum allowed size of {_options.MaxFileSizeBytes} bytes", nameof(file));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (_options.BlockedFileExtensions.Contains(extension))
        {
            throw new ArgumentException($"File type '{extension}' is not allowed", nameof(file));
        }

        if (_options.AllowedFileExtensions.Length > 0 && !_options.AllowedFileExtensions.Contains(extension))
        {
            throw new ArgumentException($"File type '{extension}' is not in the allowed file types list", nameof(file));
        }
    }

    private static string SanitizeObjectPath(string objectPath)
    {
        // Remove leading/trailing slashes and normalize separators
        var sanitized = objectPath.Trim('/').Replace('\\', '/');

        // Remove any double slashes
        while (sanitized.Contains("//"))
        {
            sanitized = sanitized.Replace("//", "/");
        }

        return sanitized;
    }

    private string EnsureUniqueObjectPath(string objectPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var directory = Path.GetDirectoryName(objectPath)?.Replace('\\', '/') ?? "";
        var fileName = Path.GetFileName(objectPath);
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Add timestamp to ensure uniqueness
        var uniqueFileName = $"{nameWithoutExtension}_{timestamp}{extension}";

        return string.IsNullOrEmpty(directory)
            ? uniqueFileName
            : $"{directory}/{uniqueFileName}";
    }
}