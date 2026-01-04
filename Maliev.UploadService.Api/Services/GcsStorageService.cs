using Google.Cloud.Storage.V1;

namespace Maliev.UploadService.Api.Services;

public class GcsStorageService : IStorageService
{
    private readonly StorageClient _storageClient;
    private readonly string _bucketName;
    private readonly IHttpClientFactory _httpClientFactory;

    public GcsStorageService(StorageClient storageClient, string bucketName, IHttpClientFactory httpClientFactory)
    {
        _storageClient = storageClient;
        _bucketName = bucketName;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
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
        var uploadOptions = new UploadObjectOptions
        {
            PredefinedAcl = PredefinedObjectAcl.Private
        };

        var uploadedObject = await _storageClient.UploadObjectAsync(
            _bucketName,
            storagePath,
            contentType,
            fileStream,
            uploadOptions,
            cancellationToken);

        return new StorageUploadResult
        {
            StoragePath = uploadedObject.Name,
            ContentType = uploadedObject.ContentType,
            SizeBytes = (long)(uploadedObject.Size ?? 0),
            UploadedAt = uploadedObject.TimeCreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow
        };
    }

    public async Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await _storageClient.GetObjectAsync(_bucketName, storagePath, cancellationToken: cancellationToken);
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

    public async Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        await _storageClient.DeleteObjectAsync(_bucketName, storagePath, cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        // Generate signed URL using V4 signing
        var urlSigner = UrlSigner.FromCredentialFile("path-to-service-account.json"); // TODO: Get from configuration
        var signedUrl = await urlSigner.SignAsync(
            _bucketName,
            storagePath,
            expiration,
            HttpMethod.Get);

        return signedUrl;
    }

    public async Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var obj = await _storageClient.GetObjectAsync(_bucketName, storagePath, cancellationToken: cancellationToken);

            return new StorageFileMetadata
            {
                Name = obj.Name,
                ContentType = obj.ContentType,
                SizeBytes = (long)(obj.Size ?? 0),
                CreatedAt = obj.TimeCreatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                ETag = obj.ETag
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
        // Create object metadata
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = storagePath,
            ContentType = contentType,
            Bucket = _bucketName
        };

        // Initiate resumable upload using GCS API
        // GCS resumable upload API returns a session URI that can be used to upload chunks
        var uploadUri = await InitiateGcsResumableUploadAsync(objectMetadata, cancellationToken);

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

        // Read chunk into memory (needed for Content-Range calculation)
        var chunkData = new byte[endByte - startByte + 1];
        await chunkStream.ReadExactlyAsync(chunkData, cancellationToken);

        // Create request with proper headers for resumable upload
        var request = new HttpRequestMessage(HttpMethod.Put, sessionUri);
        request.Content = new ByteArrayContent(chunkData);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(startByte, endByte, totalSize);

        var response = await httpClient.SendAsync(request, cancellationToken);

        // Check response status
        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            // Upload complete
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            // Parse GCS response to get object name
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

    private async Task<string> InitiateGcsResumableUploadAsync(Google.Apis.Storage.v1.Data.Object objectMetadata, CancellationToken cancellationToken)
    {
        // Use HttpClient to initiate resumable upload via GCS JSON API
        var httpClient = _httpClientFactory.CreateClient();

        // Get GCS upload endpoint
        var uploadUrl = $"https://storage.googleapis.com/upload/storage/v1/b/{_bucketName}/o?uploadType=resumable";

        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(objectMetadata);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        // Add authentication header (using StorageClient's credentials)
        // Note: In production, this should use proper authentication from StorageClient
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
}

