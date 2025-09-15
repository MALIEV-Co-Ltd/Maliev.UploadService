using Maliev.UploadService.Api.Models;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Clean, business-agnostic file storage interface
/// Only supports path-based operations with no legacy business logic
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a file to a specific object path
    /// </summary>
    Task<FileUploadResponse> UploadFileToPathAsync(
        string objectPath,
        IFormFile file,
        string uploadedBy,
        string ipAddress = "",
        StorageOptions? options = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Download a file by its object path
    /// </summary>
    Task<FileDownloadResponse?> DownloadFileByPathAsync(string objectPath, string accessedBy, string ipAddress = "");

    /// <summary>
    /// Delete a file by its object path
    /// </summary>
    Task<bool> DeleteFileByPathAsync(string objectPath, string deletedBy, string ipAddress = "");

    /// <summary>
    /// Check if a file exists at the specified path
    /// </summary>
    Task<bool> FileExistsByPathAsync(string objectPath);

    /// <summary>
    /// Generate a signed URL for temporary access to a file
    /// </summary>
    Task<string> GenerateSignedUrlByPathAsync(string objectPath, TimeSpan? expiration = null);

    /// <summary>
    /// List files matching a path pattern
    /// </summary>
    Task<List<FileMetadataResponse>> ListFilesByPathPatternAsync(string pathPattern, int maxResults = 100);
}