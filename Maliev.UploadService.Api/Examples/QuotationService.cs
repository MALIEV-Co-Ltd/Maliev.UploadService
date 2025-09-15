using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Examples.Shared;

namespace Maliev.UploadService.Api.Examples;

/// <summary>
/// Example implementation showing how a domain service (QuotationService)
/// should interact with the new generic storage service.
///
/// This demonstrates the recommended pattern where each business domain
/// owns its storage structure and delegates file operations to the storage service.
/// </summary>
public class QuotationService : IQuotationService
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<QuotationService> _logger;

    public QuotationService(
        IFileStorageService storageService,
        ILogger<QuotationService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a quotation document with business-specific path generation
    /// </summary>
    public async Task<QuotationDocumentResult> UploadQuotationDocumentAsync(
        string quotationId,
        IFormFile document,
        QuotationDocumentType documentType,
        string uploadedBy)
    {
        try
        {
            // Business logic: Generate domain-specific storage path
            var objectPath = GenerateQuotationDocumentPath(quotationId, document.FileName, documentType);

            _logger.LogInformation("Uploading quotation document for {QuotationId}: {DocumentType} to {Path}",
                quotationId, documentType, objectPath);

            // Delegate storage operation to generic service
            var uploadResult = await _storageService.UploadFileToPathAsync(
                objectPath: objectPath,
                file: document,
                uploadedBy: uploadedBy,
                options: new StorageOptions
                {
                    RetentionDays = GetRetentionPeriod(documentType),
                    EnableVersioning = documentType == QuotationDocumentType.FinalQuotation,
                    ContentType = GetExpectedContentType(documentType)
                },
                metadata: new Dictionary<string, string>
                {
                    ["quotationId"] = quotationId,
                    ["documentType"] = documentType.ToString(),
                    ["serviceName"] = "quotation-service",
                    ["businessCategory"] = "sales"
                });

            // Business logic: Update quotation record with document reference
            await UpdateQuotationWithDocumentAsync(quotationId, objectPath, documentType, uploadResult);

            return new QuotationDocumentResult
            {
                QuotationId = quotationId,
                DocumentType = documentType,
                ObjectPath = objectPath,
                FileId = uploadResult.FileId,
                UploadedAt = uploadResult.UploadedAt,
                FileSize = uploadResult.FileSize,
                Status = QuotationDocumentStatus.Active
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload quotation document for {QuotationId}", quotationId);
            throw;
        }
    }

    /// <summary>
    /// Download a quotation document by business identifier
    /// </summary>
    public async Task<FileDownloadResponse?> DownloadQuotationDocumentAsync(
        string quotationId,
        QuotationDocumentType documentType,
        string accessedBy)
    {
        try
        {
            // Business logic: Resolve storage path from business identifiers
            var objectPath = await GetQuotationDocumentPathAsync(quotationId, documentType);
            if (objectPath == null)
            {
                _logger.LogWarning("Quotation document not found: {QuotationId}/{DocumentType}", quotationId, documentType);
                return null;
            }

            // Business logic: Check access permissions
            if (!await CanAccessQuotationDocumentAsync(quotationId, documentType, accessedBy))
            {
                _logger.LogWarning("Access denied to quotation document: {QuotationId}/{DocumentType} for {User}",
                    quotationId, documentType, accessedBy);
                throw new UnauthorizedAccessException("Access denied to quotation document");
            }

            // Delegate download to generic service
            return await _storageService.DownloadFileByPathAsync(objectPath, accessedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download quotation document: {QuotationId}/{DocumentType}", quotationId, documentType);
            throw;
        }
    }

    /// <summary>
    /// List all documents for a quotation
    /// </summary>
    public async Task<List<QuotationDocumentResult>> GetQuotationDocumentsAsync(string quotationId)
    {
        try
        {
            // Business logic: This would typically query your business database
            // For this example, we'll demonstrate the concept
            var documents = await GetQuotationDocumentsFromDatabaseAsync(quotationId);

            return documents.Select(doc => new QuotationDocumentResult
            {
                QuotationId = quotationId,
                DocumentType = doc.DocumentType,
                ObjectPath = doc.ObjectPath,
                FileId = doc.FileId,
                UploadedAt = doc.UploadedAt,
                FileSize = doc.FileSize,
                Status = doc.Status
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get quotation documents for {QuotationId}", quotationId);
            throw;
        }
    }

    /// <summary>
    /// Archive old quotation documents based on business rules
    /// </summary>
    public async Task ArchiveOldQuotationDocumentsAsync(DateTime cutoffDate)
    {
        try
        {
            _logger.LogInformation("Starting quotation document archival for documents older than {CutoffDate}", cutoffDate);

            // Business logic: Get expired quotations from business database
            var expiredDocuments = await GetExpiredQuotationDocumentsAsync(cutoffDate);

            foreach (var document in expiredDocuments)
            {
                try
                {
                    // Move to archive location with new path
                    var archivePath = GenerateArchivePath(document.ObjectPath);

                    // Download original
                    var fileContent = await _storageService.DownloadFileByPathAsync(document.ObjectPath, "system");
                    if (fileContent != null)
                    {
                        // Upload to archive location
                        using var stream = new MemoryStream(fileContent.Content);
                        var formFile = new FormFileWrapper(stream, fileContent.FileName, fileContent.ContentType);

                        await _storageService.UploadFileToPathAsync(
                            archivePath, formFile, "system",
                            options: new StorageOptions { RetentionDays = 2555 }); // 7 years

                        // Delete original
                        await _storageService.DeleteFileByPathAsync(document.ObjectPath, "system");

                        // Update business database
                        await UpdateDocumentStatusAsync(document.QuotationId, document.DocumentType,
                            QuotationDocumentStatus.Archived, archivePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to archive document: {ObjectPath}", document.ObjectPath);
                    // Continue with other documents
                }
            }

            _logger.LogInformation("Completed quotation document archival");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive quotation documents");
            throw;
        }
    }

    #region Private Business Logic Methods

    /// <summary>
    /// Business logic: Generate storage path following quotation service conventions
    /// </summary>
    private static string GenerateQuotationDocumentPath(string quotationId, string fileName, QuotationDocumentType documentType)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var typeFolder = documentType.ToString().ToLowerInvariant();

        // Quotation service owns this path structure
        return $"quotations/{quotationId}/documents/{typeFolder}/{timestamp}_{sanitizedFileName}";
    }

    private static string GenerateArchivePath(string originalPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"archive/{timestamp}/{originalPath}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int GetRetentionPeriod(QuotationDocumentType documentType)
    {
        return documentType switch
        {
            QuotationDocumentType.DraftQuotation => 365,    // 1 year
            QuotationDocumentType.FinalQuotation => 2555,   // 7 years (business requirement)
            QuotationDocumentType.ClientFeedback => 1095,   // 3 years
            QuotationDocumentType.TechnicalSpecs => 2555,   // 7 years
            _ => 365
        };
    }

    private static string? GetExpectedContentType(QuotationDocumentType documentType)
    {
        return documentType switch
        {
            QuotationDocumentType.DraftQuotation => "application/pdf",
            QuotationDocumentType.FinalQuotation => "application/pdf",
            QuotationDocumentType.TechnicalSpecs => null, // Allow various types
            _ => null
        };
    }

    // These would be implemented with actual database operations
    private Task<string?> GetQuotationDocumentPathAsync(string quotationId, QuotationDocumentType documentType)
    {
        // Implementation would query business database for document path
        throw new NotImplementedException("Implement with actual database query");
    }

    private Task<bool> CanAccessQuotationDocumentAsync(string quotationId, QuotationDocumentType documentType, string userId)
    {
        // Implementation would check business authorization rules
        throw new NotImplementedException("Implement with actual authorization logic");
    }

    private Task<List<QuotationDocumentRecord>> GetQuotationDocumentsFromDatabaseAsync(string quotationId)
    {
        // Implementation would query business database
        throw new NotImplementedException("Implement with actual database query");
    }

    private Task<List<QuotationDocumentRecord>> GetExpiredQuotationDocumentsAsync(DateTime cutoffDate)
    {
        // Implementation would query business database for expired documents
        throw new NotImplementedException("Implement with actual database query");
    }

    private Task UpdateQuotationWithDocumentAsync(string quotationId, string objectPath, QuotationDocumentType documentType, FileUploadResponse uploadResult)
    {
        // Implementation would update business database with document reference
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task UpdateDocumentStatusAsync(string quotationId, QuotationDocumentType documentType, QuotationDocumentStatus status, string? newPath = null)
    {
        // Implementation would update business database
        throw new NotImplementedException("Implement with actual database update");
    }

    #endregion
}

#region Supporting Types

public interface IQuotationService
{
    Task<QuotationDocumentResult> UploadQuotationDocumentAsync(string quotationId, IFormFile document, QuotationDocumentType documentType, string uploadedBy);
    Task<FileDownloadResponse?> DownloadQuotationDocumentAsync(string quotationId, QuotationDocumentType documentType, string accessedBy);
    Task<List<QuotationDocumentResult>> GetQuotationDocumentsAsync(string quotationId);
    Task ArchiveOldQuotationDocumentsAsync(DateTime cutoffDate);
}

public enum QuotationDocumentType
{
    DraftQuotation,
    FinalQuotation,
    ClientFeedback,
    TechnicalSpecs,
    SupportingDocuments
}

public enum QuotationDocumentStatus
{
    Active,
    Archived,
    Deleted
}

public class QuotationDocumentResult
{
    public string QuotationId { get; set; } = string.Empty;
    public QuotationDocumentType DocumentType { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
    public QuotationDocumentStatus Status { get; set; }
}

public class QuotationDocumentRecord
{
    public string QuotationId { get; set; } = string.Empty;
    public QuotationDocumentType DocumentType { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
    public QuotationDocumentStatus Status { get; set; }
}

#endregion