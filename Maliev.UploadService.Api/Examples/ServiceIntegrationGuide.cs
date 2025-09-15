using Maliev.UploadService.Api.Models;
using System.Text;

namespace Maliev.UploadService.Api.Examples;

/// <summary>
/// Service Integration Guide showing how external services should interact
/// with the Upload Service using the new generic approach.
///
/// This demonstrates HTTP client patterns for microservice-to-microservice communication.
/// </summary>
public class ServiceIntegrationGuide
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceIntegrationGuide> _logger;
    private readonly string _uploadServiceBaseUrl;

    public ServiceIntegrationGuide(HttpClient httpClient, ILogger<ServiceIntegrationGuide> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _uploadServiceBaseUrl = configuration["Services:UploadService:BaseUrl"] ?? "http://localhost:5000";
    }

    #region Example 1: Direct Path Upload (Recommended for New Services)

    /// <summary>
    /// Example: How QuotationService should upload a document using the new generic approach
    /// </summary>
    public async Task<ApiUploadResult> UploadQuotationDocumentExample(string quotationId, byte[] fileContent, string fileName, string uploadedBy)
    {
        try
        {
            // 1. Business service generates its own storage path
            var objectPath = $"quotations/{quotationId}/documents/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}";

            _logger.LogInformation("Uploading quotation document to path: {ObjectPath}", objectPath);

            // 2. Prepare multipart form data
            using var formData = new MultipartFormDataContent();
            using var fileContent1 = new ByteArrayContent(fileContent);
            fileContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            formData.Add(fileContent1, "file", fileName);

            // 3. Call Upload Service v2 API with direct path
            var response = await _httpClient.PostAsync($"{_uploadServiceBaseUrl}/uploads/v2/path/{objectPath}", formData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
                _logger.LogInformation("Successfully uploaded document. FileId: {FileId}, ObjectPath: {ObjectPath}",
                    result?.FileId, result?.ObjectName);

                return new ApiUploadResult
                {
                    Success = true,
                    FileId = result?.FileId ?? Guid.Empty,
                    ObjectPath = result?.ObjectName ?? objectPath,
                    Message = "Upload successful"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Upload failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);

            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload failed: {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during quotation document upload");
            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload exception: {ex.Message}"
            };
        }
    }

    #endregion

    #region Example 2: Generic Upload Request (Flexible Approach)

    /// <summary>
    /// Example: Using the generic upload endpoint with either ObjectPath or legacy Category approach
    /// </summary>
    public async Task<ApiUploadResult> UploadWithGenericRequestExample(string objectPath, byte[] fileContent, string fileName, Dictionary<string, string>? metadata = null)
    {
        try
        {
            _logger.LogInformation("Uploading file using generic request to path: {ObjectPath}", objectPath);

            // Prepare the generic upload request
            using var formData = new MultipartFormDataContent();

            // Add file
            using var fileContent1 = new ByteArrayContent(fileContent);
            fileContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            formData.Add(fileContent1, "File", fileName);

            // Add object path (new approach)
            formData.Add(new StringContent(objectPath), "ObjectPath");

            // Add optional metadata
            if (metadata != null)
            {
                foreach (var (key, value) in metadata)
                {
                    formData.Add(new StringContent(value), $"ServiceMetadata[{key}]");
                }
            }

            // Optional: Add storage options
            formData.Add(new StringContent("Internal"), "AccessLevel");
            formData.Add(new StringContent("365"), "StorageOptions.RetentionDays");
            formData.Add(new StringContent("true"), "StorageOptions.EnableEncryption");

            var response = await _httpClient.PostAsync($"{_uploadServiceBaseUrl}/uploads/v2", formData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
                return new ApiUploadResult
                {
                    Success = true,
                    FileId = result?.FileId ?? Guid.Empty,
                    ObjectPath = result?.ObjectName ?? objectPath,
                    Message = "Upload successful"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload failed: {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during generic upload");
            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload exception: {ex.Message}"
            };
        }
    }

    #endregion

    #region Example 3: Legacy Category Upload (Backward Compatibility)

    /// <summary>
    /// Example: Using legacy category approach for existing services during migration
    /// </summary>
    public async Task<ApiUploadResult> UploadWithLegacyCategoryExample(string category, string entityId, byte[] fileContent, string fileName)
    {
        try
        {
            _logger.LogInformation("Uploading file using legacy category: {Category}/{EntityId}", category, entityId);

            using var formData = new MultipartFormDataContent();

            // Add file
            using var fileContent1 = new ByteArrayContent(fileContent);
            fileContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            formData.Add(fileContent1, "File", fileName);

            // Add legacy fields (will work with both v1 and v2 APIs)
            formData.Add(new StringContent(category), "Category");
            formData.Add(new StringContent(entityId), "EntityId");
            formData.Add(new StringContent("Internal"), "AccessLevel");

            // Use v2 API that supports both approaches
            var response = await _httpClient.PostAsync($"{_uploadServiceBaseUrl}/uploads/v2", formData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
                return new ApiUploadResult
                {
                    Success = true,
                    FileId = result?.FileId ?? Guid.Empty,
                    ObjectPath = result?.ObjectName ?? "",
                    Message = "Upload successful"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload failed: {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during legacy upload");
            return new ApiUploadResult
            {
                Success = false,
                Message = $"Upload exception: {ex.Message}"
            };
        }
    }

    #endregion

    #region Example 4: Download Operations

    /// <summary>
    /// Example: Download file by object path
    /// </summary>
    public async Task<ApiDownloadResult> DownloadByPathExample(string objectPath)
    {
        try
        {
            _logger.LogInformation("Downloading file from path: {ObjectPath}", objectPath);

            var response = await _httpClient.GetAsync($"{_uploadServiceBaseUrl}/uploads/v2/path/{objectPath}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = ExtractFileNameFromPath(objectPath);

                return new ApiDownloadResult
                {
                    Success = true,
                    Content = content,
                    ContentType = contentType,
                    FileName = fileName,
                    Message = "Download successful"
                };
            }

            return new ApiDownloadResult
            {
                Success = false,
                Message = $"Download failed with status: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during download");
            return new ApiDownloadResult
            {
                Success = false,
                Message = $"Download exception: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Example: Download file by legacy FileId (backward compatibility)
    /// </summary>
    public async Task<ApiDownloadResult> DownloadByFileIdExample(Guid fileId)
    {
        try
        {
            _logger.LogInformation("Downloading file by ID: {FileId}", fileId);

            // This works with both v1 and v2 APIs
            var response = await _httpClient.GetAsync($"{_uploadServiceBaseUrl}/uploads/v2/{fileId}/download");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "download";

                return new ApiDownloadResult
                {
                    Success = true,
                    Content = content,
                    ContentType = contentType,
                    FileName = fileName,
                    Message = "Download successful"
                };
            }

            return new ApiDownloadResult
            {
                Success = false,
                Message = $"Download failed with status: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during download");
            return new ApiDownloadResult
            {
                Success = false,
                Message = $"Download exception: {ex.Message}"
            };
        }
    }

    #endregion

    #region Example 5: File Management Operations

    /// <summary>
    /// Example: Check if file exists at path
    /// </summary>
    public async Task<bool> CheckFileExistsExample(string objectPath)
    {
        try
        {
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"{_uploadServiceBaseUrl}/uploads/v2/path/{objectPath}"));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during file existence check");
            return false;
        }
    }

    /// <summary>
    /// Example: Delete file by path
    /// </summary>
    public async Task<bool> DeleteFileExample(string objectPath)
    {
        try
        {
            _logger.LogInformation("Deleting file at path: {ObjectPath}", objectPath);

            var response = await _httpClient.DeleteAsync($"{_uploadServiceBaseUrl}/uploads/v2/path/{objectPath}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during file deletion");
            return false;
        }
    }

    /// <summary>
    /// Example: Generate signed URL for temporary access
    /// </summary>
    public async Task<string?> GenerateSignedUrlExample(string objectPath, int expirationHours = 1)
    {
        try
        {
            _logger.LogInformation("Generating signed URL for path: {ObjectPath}", objectPath);

            var response = await _httpClient.PostAsync(
                $"{_uploadServiceBaseUrl}/uploads/v2/path/signed-url?objectPath={Uri.EscapeDataString(objectPath)}&expirationHours={expirationHours}",
                null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SignedUrlResponse>();
                return result?.SignedUrl;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during signed URL generation");
            return null;
        }
    }

    #endregion

    #region Example 6: Service Configuration

    /// <summary>
    /// Example: How to configure HttpClient for Upload Service calls
    /// </summary>
    public static void ConfigureUploadServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<ServiceIntegrationGuide>(client =>
        {
            var baseUrl = configuration["Services:UploadService:BaseUrl"] ?? "http://localhost:5000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // Large files may take time

            // Add authentication if required
            var apiKey = configuration["Services:UploadService:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            }

            // Add service identification
            client.DefaultRequestHeaders.Add("X-Calling-Service", "quotation-service");
        });
    }

    /// <summary>
    /// Example: appsettings.json configuration for Upload Service integration
    /// </summary>
    public static string GetExampleConfiguration()
    {
        return """
        {
          "Services": {
            "UploadService": {
              "BaseUrl": "http://maliev-upload-service:5000",
              "ApiKey": "your-api-key-here",
              "DefaultRetentionDays": 365,
              "MaxFileSizeBytes": 104857600,
              "EnablePathGeneration": true
            }
          },
          "StoragePaths": {
            "QuotationDocuments": "quotations/{quotationId}/documents/{documentType}",
            "OrderFiles": "orders/{orderId}/{stage}/{fileType}",
            "CustomerFiles": "customers/{customerId}/{category}"
          }
        }
        """;
    }

    #endregion

    #region Helper Methods

    private static string ExtractFileNameFromPath(string objectPath)
    {
        return Path.GetFileName(objectPath) ?? "download";
    }

    #endregion
}

#region Result Types

public class ApiUploadResult
{
    public bool Success { get; set; }
    public Guid FileId { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApiDownloadResult
{
    public bool Success { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

#endregion