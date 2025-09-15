using Maliev.UploadService.Api.Models;

namespace Maliev.UploadService.Api.Services;

public interface IGoogleCloudStorageService
{
    Task<string> UploadFileAsync(string bucketName, string objectName, Stream fileStream, string contentType);
    Task<FileDownloadResponse> DownloadFileAsync(string bucketName, string objectName);
    Task<bool> DeleteFileAsync(string bucketName, string objectName);
    Task<bool> FileExistsAsync(string bucketName, string objectName);
    Task<long> GetFileSizeAsync(string bucketName, string objectName);
    Task<string> GenerateSignedUrlAsync(string bucketName, string objectName, TimeSpan expiration, bool forUpload = false);
    Task<(string md5Hash, string sha256Hash)> CalculateHashesAsync(Stream fileStream);
}