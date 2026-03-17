using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Google Cloud Storage implementation of the IStorageService.
/// </summary>
public class GcsStorageService : IStorageService
{
    private readonly StorageClient _storageClient;
    private readonly Google.Apis.Auth.OAuth2.GoogleCredential _credential;
    private readonly Dictionary<string, string> _buckets;
    private readonly string _defaultBucket;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the GcsStorageService class.
    /// </summary>
    /// <param name="storageClient">The GCS storage client.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="credential">The Google credentials.</param>
    public GcsStorageService(StorageClient storageClient, IConfiguration config, IHttpClientFactory httpClientFactory, Google.Apis.Auth.OAuth2.GoogleCredential credential)
    {
        _storageClient = storageClient;
        _httpClientFactory = httpClientFactory;
        _credential = credential;

        // Load bucket names from config (GoogleCloud:Buckets section)
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
        // Customer documents
        if (storagePath.StartsWith("customer-", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/customers/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/onboarding/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/kyc/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("customers", "maliev-customers");

        // Financial documents
        if (storagePath.Contains("/invoices/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/receipts/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/statements/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/financials/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("financials", "maliev-financials");

        // Operations documents
        if (storagePath.Contains("/orders/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/materials/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/quotations/", StringComparison.OrdinalIgnoreCase) ||
            storagePath.Contains("/suppliers/", StringComparison.OrdinalIgnoreCase))
            return _buckets.GetValueOrDefault("operations", "maliev-operations");

        // Default to temp bucket for AI extraction, temp files, and anything unmatched
        return _defaultBucket;
    }

    /// <summary>
    /// Uploads a file to Google Cloud Storage.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="overwrite">Whether to overwrite an existing file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The upload result.</returns>
    public async Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        // Check if file exists and overwrite is not allowed
        if (!overwrite)
        {
            var exists = await FileExistsAsync(storagePath, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException($"File already exists at path: {storagePath}. Set overwrite=true to replace it.");
            }
        }

        // Upload file using streaming to avoid loading entire file into memory
        // No PredefinedAcl — buckets use Uniform Bucket-Level Access (UBLA)
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

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the file exists, otherwise false.</returns>
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

    /// <summary>
    /// Deletes a file from Google Cloud Storage.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);
        await _storageClient.DeleteObjectAsync(bucketName, storagePath, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Generates a signed URL for file download.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="expiration">The expiration time span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The signed URL.</returns>
    public async Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        try
        {
            // Generate signed URL using V4 signing
            // Use the injected credential which handles both Service Account keys and GKE Workload Identity
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
            // Fallback for local development environments where ADC is a User Account
            // Return a direct storage URL (user must have permissions to access)
            return $"https://storage.googleapis.com/{bucketName}/{storagePath}";
        }
    }

    /// <summary>
    /// Gets file metadata from Google Cloud Storage.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file metadata if found, otherwise null.</returns>
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

    /// <summary>
    /// Initiates a resumable upload session with GCS (FR-022, FR-024)
    /// </summary>
    public async Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketForPath(storagePath);

        // Create object metadata
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = storagePath,
            ContentType = contentType,
            Bucket = bucketName
        };

        // Initiate resumable upload using GCS API
        var uploadUri = await InitiateGcsResumableUploadAsync(bucketName, objectMetadata, cancellationToken);

        return new ResumableUploadSession
        {
            SessionUri = uploadUri,
            StoragePath = storagePath,
            ExpiresAt = DateTime.UtcNow.AddHours(24) // GCS sessions typically expire after 24 hours
        };
    }

    /// <summary>
    /// Resumes an upload by sending a chunk to the GCS session URI (FR-022, FR-024)
    /// </summary>
    public async Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient();

        // Calculate expected content length
        var contentLength = endByte - startByte + 1;

        // Create request with proper headers for resumable upload and stream directly
        var request = new HttpRequestMessage(HttpMethod.Put, sessionUri);
        request.Content = new StreamContent(chunkStream, (int)contentLength);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(startByte, endByte, totalSize);

        var response = await httpClient.SendAsync(request, cancellationToken);

        // Check response status
        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            // Upload complete
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
        else if ((int)response.StatusCode == 308) // Resume Incomplete
        {
            // Parse Range header to determine how many bytes were received
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

    private async Task<string> InitiateGcsResumableUploadAsync(string bucketName, Google.Apis.Storage.v1.Data.Object objectMetadata, CancellationToken cancellationToken)
    {
        // Use HttpClient to initiate resumable upload via GCS JSON API
        var httpClient = _httpClientFactory.CreateClient();

        // Get GCS upload endpoint
        var uploadUrl = $"https://storage.googleapis.com/upload/storage/v1/b/{bucketName}/o?uploadType=resumable";

        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(objectMetadata);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // GCS returns the session URI in the Location header
        var sessionUri = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(sessionUri))
        {
            throw new InvalidOperationException("Failed to initiate resumable upload: no session URI returned");
        }

        return sessionUri;
    }

    private long ParseRangeHeader(string? rangeHeader)
    {
        if (string.IsNullOrEmpty(rangeHeader))
        {
            return 0;
        }

        // Range header format: "bytes=0-1234567"
        var parts = rangeHeader.Split('=');
        if (parts.Length == 2)
        {
            var range = parts[1].Split('-');
            if (range.Length == 2 && long.TryParse(range[1], out var endByte))
            {
                return endByte + 1; // Return bytes received (end byte is inclusive)
            }
        }

        return 0;
    }

    /// <inheritdoc />
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
}
