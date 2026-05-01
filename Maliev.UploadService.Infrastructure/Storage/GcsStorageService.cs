using Google.Cloud.Storage.V1;
using Maliev.UploadService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.UploadService.Infrastructure.Storage;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IStorageService"/>.
/// </summary>
public class GcsStorageService : IStorageService
{
    private readonly StorageClient _storageClient;
    private readonly Google.Apis.Auth.OAuth2.GoogleCredential _credential;
    private readonly Dictionary<string, string> _buckets;
    private readonly string _defaultBucket;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GcsStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GcsStorageService"/>.
    /// </summary>
    public GcsStorageService(
        StorageClient storageClient,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        Google.Apis.Auth.OAuth2.GoogleCredential credential,
        ILogger<GcsStorageService> logger)
    {
        _storageClient = storageClient;
        _httpClientFactory = httpClientFactory;
        _credential = credential;
        _logger = logger;

        _buckets = config.GetSection("GoogleCloud:Buckets")
            .GetChildren()
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value!);

        _defaultBucket = _buckets.GetValueOrDefault("temp", "maliev-temp");
    }

    /// <summary>
    /// Routes a storage path to the correct bucket based on path conventions.
    /// </summary>
    private string GetBucketForPath(string storagePath)
    {
        if (storagePath.StartsWith("customer-", StringComparison.OrdinalIgnoreCase) ||
            storagePath.StartsWith("customers/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/customers/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/onboarding/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/kyc/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("customers", "maliev-customers");

        if (storagePath.Contains("/invoices/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/receipts/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/statements/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/financials/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("financials", "maliev-financials");

        if (storagePath.Contains("/orders/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/materials/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/quotations/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/suppliers/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("operations", "maliev-operations");

        return _defaultBucket;
    }

    /// <inheritdoc/>
    public async Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        if (!overwrite)
        {
            var exists = await FileExistsAsync(storagePath, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException($"File already exists at path: {storagePath}. Set overwrite=true to replace it.");
            }
        }

        var uploadedObject = await _storageClient.UploadObjectAsync(
            bucketName,
            storagePath,
            contentType,
            fileStream,
            cancellationToken: cancellationToken);

        return new StorageUploadResult
        {
            StoragePath = uploadedObject.Name,
            ContentType = uploadedObject.ContentType,
            SizeBytes = (long)(uploadedObject.Size ?? 0),
            UploadedAt = uploadedObject.TimeCreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
            ETag = uploadedObject.ETag,
            Md5Hash = uploadedObject.Md5Hash
        };
    }

    /// <inheritdoc/>
    public async Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);
        try
        {
            await _storageClient.GetObjectAsync(bucketName, storagePath, cancellationToken: cancellationToken);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound || ex.Error?.Code == 404)
        {
            return false;
        }
        catch (Google.GoogleApiException ex) when (ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);
        await _storageClient.DeleteObjectAsync(bucketName, storagePath, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        try
        {
            var urlSigner = UrlSigner.FromCredential(_credential);

            var signedUrl = await urlSigner.SignAsync(
                bucketName,
                storagePath,
                expiration,
                HttpMethod.Get,
                cancellationToken: cancellationToken);

            return signedUrl;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("UserCredential is not supported for signing"))
        {
            return $"https://storage.googleapis.com/{bucketName}/{storagePath}";
        }
    }

    /// <inheritdoc/>
    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);
        try
        {
            var obj = await _storageClient.GetObjectAsync(bucketName, storagePath, cancellationToken: cancellationToken);

            return new StorageFileMetadata
            {
                Name = obj.Name,
                ContentType = obj.ContentType,
                SizeBytes = (long)(obj.Size ?? 0),
                CreatedAt = obj.TimeCreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                ETag = obj.ETag,
                Md5Hash = obj.Md5Hash
            };
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound || ex.Error?.Code == 404)
        {
            return null;
        }
        catch (Google.GoogleApiException ex) when (ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = storagePath,
            ContentType = contentType,
            Bucket = bucketName
        };

        var uploadUri = await InitiateGcsResumableUploadAsync(bucketName, objectMetadata, cancellationToken);

        return new ResumableUploadSession
        {
            SessionUri = uploadUri,
            StoragePath = storagePath,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
    }

    /// <inheritdoc/>
    public async Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var contentLength = endByte - startByte + 1;

        var request = new HttpRequestMessage(HttpMethod.Put, sessionUri);
        request.Content = new StreamContent(chunkStream);
        request.Content.Headers.ContentLength = contentLength;
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(startByte, endByte, totalSize);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var uploadedObject = System.Text.Json.JsonSerializer.Deserialize<Google.Apis.Storage.v1.Data.Object>(responseContent);

            return new ResumableUploadProgress
            {
                BytesReceived = totalSize,
                TotalSize = totalSize,
                IsComplete = true,
                StoragePath = uploadedObject?.Name
            };
        }
        else if ((int)response.StatusCode == 308)
        {
            var rangeHeader = response.Headers.FirstOrDefault(h => h.Key.Equals("Range", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
            var bytesReceived = ParseRangeHeader(rangeHeader);

            return new ResumableUploadProgress
            {
                BytesReceived = bytesReceived,
                TotalSize = totalSize,
                IsComplete = false,
                StoragePath = null
            };
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resumable upload failed with status {response.StatusCode}: {errorContent}");
        }
    }

    /// <inheritdoc/>
    public async Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);
        var obj = new Google.Apis.Storage.v1.Data.Object
        {
            Name = storagePath,
            Bucket = bucketName,
            StorageClass = targetStorageClass
        };

        await _storageClient.PatchObjectAsync(
            obj,
            new PatchObjectOptions(),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<StorageUploadResult> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceBucket = GetBucketForPath(sourcePath);
        var destinationBucket = GetBucketForPath(destinationPath);

        _logger.LogInformation("Copying file from {SourceBucket}/{SourcePath} to {DestBucket}/{DestPath}",
            sourceBucket, sourcePath, destinationBucket, destinationPath);

        var copiedObject = await _storageClient.CopyObjectAsync(
            sourceBucket, sourcePath,
            destinationBucket, destinationPath,
            cancellationToken: cancellationToken);

        return new StorageUploadResult
        {
            StoragePath = copiedObject.Name,
            ContentType = copiedObject.ContentType,
            SizeBytes = (long)(copiedObject.Size ?? 0),
            UploadedAt = copiedObject.TimeCreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
            ETag = copiedObject.ETag,
            Md5Hash = copiedObject.Md5Hash
        };
    }

    private async Task<string> InitiateGcsResumableUploadAsync(
        string bucketName,
        Google.Apis.Storage.v1.Data.Object objectMetadata,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var uploadUrl = $"https://storage.googleapis.com/upload/storage/v1/b/{bucketName}/o?uploadType=resumable";

        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        var tokenAccess = (Google.Apis.Auth.OAuth2.ITokenAccess)_credential;
        var accessToken = await tokenAccess.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Upload-Content-Type", objectMetadata.ContentType);
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(objectMetadata);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var sessionUri = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(sessionUri))
        {
            throw new InvalidOperationException("Failed to initiate resumable upload: no session URI returned");
        }

        return sessionUri;
    }

    private static long ParseRangeHeader(string? rangeHeader)
    {
        if (string.IsNullOrEmpty(rangeHeader))
        {
            return 0;
        }

        var parts = rangeHeader.Split('=');
        if (parts.Length == 2)
        {
            var range = parts[1].Split('-');
            if (range.Length == 2 && long.TryParse(range[1], out var endByte))
            {
                return endByte + 1;
            }
        }

        return 0;
    }
}
