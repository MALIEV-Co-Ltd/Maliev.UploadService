using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;
using Maliev.UploadService.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.UploadService.Api.Services;

public class GoogleCloudStorageService : IGoogleCloudStorageService
{
    private readonly StorageClient _storageClient;
    private readonly ILogger<GoogleCloudStorageService> _logger;

    public GoogleCloudStorageService(StorageClient storageClient, ILogger<GoogleCloudStorageService> logger)
    {
        _storageClient = storageClient;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(string bucketName, string objectName, Stream fileStream, string contentType)
    {
        try
        {
            _logger.LogInformation("Starting upload of {ObjectName} to bucket {BucketName}", objectName, bucketName);

            var googleObject = new Google.Apis.Storage.v1.Data.Object
            {
                Bucket = bucketName,
                Name = objectName,
                ContentType = contentType
            };

            // Reset stream position
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var uploadedObject = await _storageClient.UploadObjectAsync(googleObject, fileStream);

            _logger.LogInformation("Successfully uploaded {ObjectName} to bucket {BucketName}. ETag: {ETag}",
                objectName, bucketName, uploadedObject.ETag);

            return uploadedObject.ETag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {ObjectName} to bucket {BucketName}", objectName, bucketName);
            throw;
        }
    }

    public async Task<FileDownloadResponse> DownloadFileAsync(string bucketName, string objectName)
    {
        try
        {
            _logger.LogInformation("Starting download of {ObjectName} from bucket {BucketName}", objectName, bucketName);

            // Get object metadata first
            var objectMetadata = await _storageClient.GetObjectAsync(bucketName, objectName);

            using var memoryStream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(bucketName, objectName, memoryStream);

            var content = memoryStream.ToArray();
            var fileName = Path.GetFileName(objectName);

            _logger.LogInformation("Successfully downloaded {ObjectName} from bucket {BucketName}. Size: {Size} bytes",
                objectName, bucketName, content.Length);

            return new FileDownloadResponse
            {
                Content = content,
                ContentType = objectMetadata.ContentType ?? "application/octet-stream",
                FileName = fileName,
                FileSize = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {ObjectName} from bucket {BucketName}", objectName, bucketName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string bucketName, string objectName)
    {
        try
        {
            _logger.LogInformation("Starting deletion of {ObjectName} from bucket {BucketName}", objectName, bucketName);

            await _storageClient.DeleteObjectAsync(bucketName, objectName);

            _logger.LogInformation("Successfully deleted {ObjectName} from bucket {BucketName}", objectName, bucketName);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Object {ObjectName} not found in bucket {BucketName} during deletion", objectName, bucketName);
            return true; // Consider not found as successfully deleted
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {ObjectName} from bucket {BucketName}", objectName, bucketName);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string bucketName, string objectName)
    {
        try
        {
            await _storageClient.GetObjectAsync(bucketName, objectName);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if {ObjectName} exists in bucket {BucketName}", objectName, bucketName);
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(string bucketName, string objectName)
    {
        try
        {
            var objectMetadata = await _storageClient.GetObjectAsync(bucketName, objectName);
            return (long)(objectMetadata.Size ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get size of {ObjectName} from bucket {BucketName}", objectName, bucketName);
            throw;
        }
    }

    public Task<string> GenerateSignedUrlAsync(string bucketName, string objectName, TimeSpan expiration, bool forUpload = false)
    {
        try
        {
            // For now, return a placeholder URL - will be implemented when GCS credentials are configured
            _logger.LogWarning("Signed URL generation not fully implemented. Returning placeholder for {ObjectName}", objectName);
            return Task.FromResult($"https://storage.googleapis.com/{bucketName}/{objectName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for {ObjectName} in bucket {BucketName}", objectName, bucketName);
            throw;
        }
    }

    public async Task<(string md5Hash, string sha256Hash)> CalculateHashesAsync(Stream fileStream)
    {
        try
        {
            // Reset stream position
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            using var md5 = MD5.Create();
            using var sha256 = SHA256.Create();

            // Read the stream once and calculate both hashes
            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var md5Hash = Convert.ToHexString(md5.Hash!).ToLowerInvariant();
            var sha256Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

            // Reset stream position for subsequent use
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            return (md5Hash, sha256Hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate file hashes");
            throw;
        }
    }
}