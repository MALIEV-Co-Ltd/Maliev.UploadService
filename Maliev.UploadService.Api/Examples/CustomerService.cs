using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Examples.Shared;

namespace Maliev.UploadService.Api.Examples;

/// <summary>
/// Example implementation showing how Customer Service implements customer-centric storage patterns.
///
/// This demonstrates a different approach from both QuotationService and OrderService:
/// - Customer Service uses customer-centric organization (profile/contracts/communications)
/// - Implements hierarchical folder structure for different customer document types
/// - Has strict access control and compliance requirements
/// - Uses customer lifecycle-based retention policies
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IFileStorageService storageService,
        ILogger<CustomerService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload customer document with customer-specific path organization
    /// </summary>
    public async Task<CustomerDocumentResult> UploadCustomerDocumentAsync(
        string customerId,
        IFormFile document,
        CustomerDocumentType documentType,
        CustomerDocumentCategory category,
        string uploadedBy)
    {
        try
        {
            // Customer service uses customer-centric organization
            var objectPath = GenerateCustomerDocumentPath(customerId, document.FileName, documentType, category);

            _logger.LogInformation("Uploading customer document for {CustomerId}: {DocumentType}/{Category} to {Path}",
                customerId, documentType, category, objectPath);

            var uploadResult = await _storageService.UploadFileToPathAsync(
                objectPath: objectPath,
                file: document,
                uploadedBy: uploadedBy,
                options: new StorageOptions
                {
                    RetentionDays = GetRetentionPeriod(documentType, category),
                    EnableVersioning = IsVersioningRequired(documentType),
                    EnableEncryption = IsEncryptionRequired(documentType, category),
                    ContentType = GetExpectedContentType(documentType)
                },
                metadata: new Dictionary<string, string>
                {
                    ["customerId"] = customerId,
                    ["documentType"] = documentType.ToString(),
                    ["category"] = category.ToString(),
                    ["serviceName"] = "customer-service",
                    ["businessCategory"] = "customer-management",
                    ["complianceLevel"] = GetComplianceLevel(documentType).ToString()
                });

            // Update customer record with document reference
            await UpdateCustomerDocumentTrackingAsync(customerId, documentType, category, objectPath, uploadResult);

            return new CustomerDocumentResult
            {
                CustomerId = customerId,
                DocumentType = documentType,
                Category = category,
                ObjectPath = objectPath,
                FileId = uploadResult.FileId,
                UploadedAt = uploadResult.UploadedAt,
                FileSize = uploadResult.FileSize,
                Status = CustomerDocumentStatus.Active,
                ComplianceLevel = GetComplianceLevel(documentType)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload customer document for {CustomerId}/{DocumentType}/{Category}",
                customerId, documentType, category);
            throw;
        }
    }

    /// <summary>
    /// Upload customer profile image with specific handling
    /// </summary>
    public async Task<CustomerDocumentResult> UploadCustomerProfileImageAsync(
        string customerId,
        IFormFile image,
        CustomerProfileImageType imageType,
        string uploadedBy)
    {
        try
        {
            ValidateImageFile(image);

            var objectPath = GenerateCustomerProfileImagePath(customerId, image.FileName, imageType);

            _logger.LogInformation("Uploading customer profile image for {CustomerId}: {ImageType}",
                customerId, imageType);

            var uploadResult = await _storageService.UploadFileToPathAsync(
                objectPath: objectPath,
                file: image,
                uploadedBy: uploadedBy,
                options: new StorageOptions
                {
                    RetentionDays = 1095, // 3 years for profile images
                    EnableVersioning = true, // Keep history of profile changes
                    EnableEncryption = false, // Public images don't need encryption
                    ContentType = image.ContentType
                },
                metadata: new Dictionary<string, string>
                {
                    ["customerId"] = customerId,
                    ["imageType"] = imageType.ToString(),
                    ["serviceName"] = "customer-service",
                    ["isPublic"] = "true"
                });

            await UpdateCustomerProfileImageAsync(customerId, imageType, objectPath, uploadResult);

            return new CustomerDocumentResult
            {
                CustomerId = customerId,
                DocumentType = CustomerDocumentType.ProfileImage,
                Category = CustomerDocumentCategory.Profile,
                ObjectPath = objectPath,
                FileId = uploadResult.FileId,
                UploadedAt = uploadResult.UploadedAt,
                FileSize = uploadResult.FileSize,
                Status = CustomerDocumentStatus.Active,
                ComplianceLevel = ComplianceLevel.Public
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload customer profile image for {CustomerId}/{ImageType}",
                customerId, imageType);
            throw;
        }
    }

    /// <summary>
    /// Batch upload communication files (emails, chat logs, call recordings)
    /// </summary>
    public async Task<List<CustomerDocumentResult>> UploadCommunicationBatchAsync(
        string customerId,
        List<CommunicationFile> communicationFiles,
        string communicationSessionId,
        string uploadedBy)
    {
        var results = new List<CustomerDocumentResult>();

        try
        {
            _logger.LogInformation("Starting communication batch upload for customer {CustomerId}: {FileCount} files",
                customerId, communicationFiles.Count);

            var batchTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            foreach (var commFile in communicationFiles)
            {
                try
                {
                    var objectPath = GenerateCommunicationBatchPath(customerId, communicationSessionId,
                        batchTimestamp, commFile.File.FileName, commFile.CommunicationType);

                    var uploadResult = await _storageService.UploadFileToPathAsync(
                        objectPath: objectPath,
                        file: commFile.File,
                        uploadedBy: uploadedBy,
                        options: new StorageOptions
                        {
                            RetentionDays = 2555, // 7 years for compliance
                            EnableVersioning = false, // Communication files are immutable
                            EnableEncryption = true // All communication requires encryption
                        },
                        metadata: new Dictionary<string, string>
                        {
                            ["customerId"] = customerId,
                            ["sessionId"] = communicationSessionId,
                            ["communicationType"] = commFile.CommunicationType.ToString(),
                            ["serviceName"] = "customer-service",
                            ["complianceRequired"] = "true"
                        });

                    results.Add(new CustomerDocumentResult
                    {
                        CustomerId = customerId,
                        DocumentType = CustomerDocumentType.CommunicationLog,
                        Category = CustomerDocumentCategory.Communications,
                        ObjectPath = objectPath,
                        FileId = uploadResult.FileId,
                        UploadedAt = uploadResult.UploadedAt,
                        FileSize = uploadResult.FileSize,
                        Status = CustomerDocumentStatus.Active,
                        ComplianceLevel = ComplianceLevel.Confidential,
                        SessionId = communicationSessionId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload communication file {FileName} for customer {CustomerId}",
                        commFile.File.FileName, customerId);
                    // Continue with other files
                }
            }

            // Update customer communication tracking
            await UpdateCustomerCommunicationBatchAsync(customerId, communicationSessionId, results);

            _logger.LogInformation("Completed communication batch upload for customer {CustomerId}: {SuccessCount}/{TotalCount} files uploaded",
                customerId, results.Count, communicationFiles.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload communication batch for customer {CustomerId}", customerId);
            throw;
        }
    }

    /// <summary>
    /// Get all documents for a customer organized by category
    /// </summary>
    public async Task<CustomerDocumentCollection> GetCustomerDocumentsAsync(string customerId)
    {
        try
        {
            var documents = await GetCustomerDocumentsFromDatabaseAsync(customerId);

            return new CustomerDocumentCollection
            {
                CustomerId = customerId,
                ProfileDocuments = documents.Where(d => d.Category == CustomerDocumentCategory.Profile).ToList(),
                ContractDocuments = documents.Where(d => d.Category == CustomerDocumentCategory.Contracts).ToList(),
                CommunicationDocuments = documents.Where(d => d.Category == CustomerDocumentCategory.Communications).ToList(),
                ComplianceDocuments = documents.Where(d => d.Category == CustomerDocumentCategory.Compliance).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get customer documents for {CustomerId}", customerId);
            throw;
        }
    }

    /// <summary>
    /// Archive customer documents when customer relationship ends
    /// </summary>
    public async Task<CustomerArchiveResult> ArchiveCustomerDocumentsAsync(
        string customerId,
        CustomerArchiveReason reason,
        string archivedBy)
    {
        try
        {
            _logger.LogInformation("Starting customer document archival for {CustomerId}, reason: {Reason}",
                customerId, reason);

            var customerDocuments = await GetCustomerDocumentsAsync(customerId);
            var archiveId = Guid.NewGuid().ToString("N")[..8];
            var archiveTimestamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var archiveBasePath = $"archive/customers/{archiveTimestamp}/{customerId}_{archiveId}";

            var archivedDocuments = new List<CustomerDocumentResult>();

            // Archive each category separately
            foreach (var category in Enum.GetValues<CustomerDocumentCategory>())
            {
                var categoryDocuments = GetDocumentsByCategory(customerDocuments, category);
                foreach (var document in categoryDocuments)
                {
                    try
                    {
                        var archivePath = $"{archiveBasePath}/{category.ToString().ToLower()}/{Path.GetFileName(document.ObjectPath)}";

                        // Download original
                        var fileContent = await _storageService.DownloadFileByPathAsync(document.ObjectPath, archivedBy);
                        if (fileContent != null)
                        {
                            // Upload to archive location
                            using var stream = new MemoryStream(fileContent.Content);
                            var formFile = new FormFileWrapper(stream, fileContent.FileName, fileContent.ContentType);

                            await _storageService.UploadFileToPathAsync(
                                archivePath, formFile, archivedBy,
                                options: new StorageOptions
                                {
                                    RetentionDays = GetArchiveRetentionDays(reason),
                                    EnableEncryption = true
                                });

                            // Delete original
                            await _storageService.DeleteFileByPathAsync(document.ObjectPath, archivedBy);

                            archivedDocuments.Add(new CustomerDocumentResult
                            {
                                CustomerId = customerId,
                                DocumentType = document.DocumentType,
                                Category = document.Category,
                                ObjectPath = archivePath,
                                FileId = document.FileId,
                                UploadedAt = document.UploadedAt,
                                FileSize = document.FileSize,
                                Status = CustomerDocumentStatus.Archived
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to archive document: {ObjectPath}", document.ObjectPath);
                        // Continue with other documents
                    }
                }
            }

            var archiveResult = new CustomerArchiveResult
            {
                CustomerId = customerId,
                ArchiveId = archiveId,
                ArchivedAt = DateTime.UtcNow,
                ArchivedBy = archivedBy,
                Reason = reason,
                ArchiveBasePath = archiveBasePath,
                ArchivedDocuments = archivedDocuments,
                TotalDocumentsArchived = archivedDocuments.Count
            };

            // Update customer status in database
            await UpdateCustomerArchiveStatusAsync(customerId, archiveResult);

            _logger.LogInformation("Completed customer document archival for {CustomerId}: {ArchivedCount} documents archived",
                customerId, archivedDocuments.Count);

            return archiveResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive customer documents for {CustomerId}", customerId);
            throw;
        }
    }

    #region Private Business Logic Methods

    private static string GenerateCustomerDocumentPath(string customerId, string fileName, CustomerDocumentType documentType, CustomerDocumentCategory category)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var categoryFolder = category.ToString().ToLowerInvariant();
        var typeFolder = documentType.ToString().ToLowerInvariant();

        // Customer service uses customer-centric organization
        return $"customers/{customerId}/{categoryFolder}/{typeFolder}/{timestamp}_{sanitizedFileName}";
    }

    private static string GenerateCustomerProfileImagePath(string customerId, string fileName, CustomerProfileImageType imageType)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var typeFolder = imageType.ToString().ToLowerInvariant();

        return $"customers/{customerId}/profile/images/{typeFolder}/{timestamp}_{sanitizedFileName}";
    }

    private static string GenerateCommunicationBatchPath(string customerId, string sessionId, string batchTimestamp, string fileName, CommunicationType communicationType)
    {
        var sanitizedFileName = SanitizeFileName(fileName);
        var typeFolder = communicationType.ToString().ToLowerInvariant();

        return $"customers/{customerId}/communications/{typeFolder}/{batchTimestamp}_{sessionId}/{sanitizedFileName}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int GetRetentionPeriod(CustomerDocumentType documentType, CustomerDocumentCategory category)
    {
        return (documentType, category) switch
        {
            (CustomerDocumentType.Contract, _) => 2555,           // 7 years
            (CustomerDocumentType.ComplianceDocument, _) => 2555, // 7 years
            (CustomerDocumentType.CommunicationLog, _) => 2555,  // 7 years
            (CustomerDocumentType.ProfileImage, _) => 1095,      // 3 years
            (CustomerDocumentType.CompanyLogo, _) => 1825,       // 5 years
            _ => 1095                                             // 3 years default
        };
    }

    private static bool IsVersioningRequired(CustomerDocumentType documentType)
    {
        return documentType == CustomerDocumentType.Contract ||
               documentType == CustomerDocumentType.ProfileImage ||
               documentType == CustomerDocumentType.CompanyLogo;
    }

    private static bool IsEncryptionRequired(CustomerDocumentType documentType, CustomerDocumentCategory category)
    {
        return documentType == CustomerDocumentType.Contract ||
               documentType == CustomerDocumentType.ComplianceDocument ||
               documentType == CustomerDocumentType.CommunicationLog ||
               category == CustomerDocumentCategory.Compliance;
    }

    private static string? GetExpectedContentType(CustomerDocumentType documentType)
    {
        return documentType switch
        {
            CustomerDocumentType.Contract => "application/pdf",
            CustomerDocumentType.ComplianceDocument => "application/pdf",
            CustomerDocumentType.ProfileImage => "image/jpeg",
            CustomerDocumentType.CompanyLogo => "image/png",
            _ => null
        };
    }

    private static ComplianceLevel GetComplianceLevel(CustomerDocumentType documentType)
    {
        return documentType switch
        {
            CustomerDocumentType.Contract => ComplianceLevel.Confidential,
            CustomerDocumentType.ComplianceDocument => ComplianceLevel.Restricted,
            CustomerDocumentType.CommunicationLog => ComplianceLevel.Confidential,
            CustomerDocumentType.ProfileImage => ComplianceLevel.Public,
            CustomerDocumentType.CompanyLogo => ComplianceLevel.Public,
            _ => ComplianceLevel.Internal
        };
    }

    private static void ValidateImageFile(IFormFile image)
    {
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        if (!allowedTypes.Contains(image.ContentType))
        {
            throw new ArgumentException($"Invalid image type: {image.ContentType}");
        }

        if (image.Length > 5 * 1024 * 1024) // 5MB
        {
            throw new ArgumentException("Image file too large (max 5MB)");
        }
    }

    private static List<CustomerDocumentResult> GetDocumentsByCategory(CustomerDocumentCollection collection, CustomerDocumentCategory category)
    {
        return category switch
        {
            CustomerDocumentCategory.Profile => collection.ProfileDocuments,
            CustomerDocumentCategory.Contracts => collection.ContractDocuments,
            CustomerDocumentCategory.Communications => collection.CommunicationDocuments,
            CustomerDocumentCategory.Compliance => collection.ComplianceDocuments,
            _ => new List<CustomerDocumentResult>()
        };
    }

    private static int GetArchiveRetentionDays(CustomerArchiveReason reason)
    {
        return reason switch
        {
            CustomerArchiveReason.ContractExpired => 2555,    // 7 years
            CustomerArchiveReason.BusinessClosed => 3650,     // 10 years
            CustomerArchiveReason.ComplianceRequired => 3650, // 10 years
            CustomerArchiveReason.CustomerRequest => 1095,    // 3 years
            _ => 2555
        };
    }

    // Database operation stubs - would be implemented with actual database calls
    private Task<List<CustomerDocumentResult>> GetCustomerDocumentsFromDatabaseAsync(string customerId)
    {
        throw new NotImplementedException("Implement with actual database query");
    }

    private Task UpdateCustomerDocumentTrackingAsync(string customerId, CustomerDocumentType documentType, CustomerDocumentCategory category, string objectPath, FileUploadResponse uploadResult)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task UpdateCustomerProfileImageAsync(string customerId, CustomerProfileImageType imageType, string objectPath, FileUploadResponse uploadResult)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task UpdateCustomerCommunicationBatchAsync(string customerId, string sessionId, List<CustomerDocumentResult> documents)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task UpdateCustomerArchiveStatusAsync(string customerId, CustomerArchiveResult archiveResult)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    #endregion
}

#region Supporting Types

public interface ICustomerService
{
    Task<CustomerDocumentResult> UploadCustomerDocumentAsync(string customerId, IFormFile document, CustomerDocumentType documentType, CustomerDocumentCategory category, string uploadedBy);
    Task<CustomerDocumentResult> UploadCustomerProfileImageAsync(string customerId, IFormFile image, CustomerProfileImageType imageType, string uploadedBy);
    Task<List<CustomerDocumentResult>> UploadCommunicationBatchAsync(string customerId, List<CommunicationFile> communicationFiles, string sessionId, string uploadedBy);
    Task<CustomerDocumentCollection> GetCustomerDocumentsAsync(string customerId);
    Task<CustomerArchiveResult> ArchiveCustomerDocumentsAsync(string customerId, CustomerArchiveReason reason, string archivedBy);
}

public enum CustomerDocumentType
{
    Contract,
    ComplianceDocument,
    CommunicationLog,
    ProfileImage,
    CompanyLogo,
    BusinessCertificate,
    TaxDocument
}

public enum CustomerDocumentCategory
{
    Profile,
    Contracts,
    Communications,
    Compliance
}

public enum CustomerProfileImageType
{
    Avatar,
    CompanyLogo,
    BusinessCard
}

public enum CommunicationType
{
    Email,
    Phone,
    Chat,
    Meeting,
    Document
}

public enum CustomerDocumentStatus
{
    Active,
    Archived,
    Deleted
}

public enum ComplianceLevel
{
    Public,
    Internal,
    Confidential,
    Restricted
}

public enum CustomerArchiveReason
{
    ContractExpired,
    BusinessClosed,
    CustomerRequest,
    ComplianceRequired
}

public class CustomerDocumentResult
{
    public string CustomerId { get; set; } = string.Empty;
    public CustomerDocumentType DocumentType { get; set; }
    public CustomerDocumentCategory Category { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
    public CustomerDocumentStatus Status { get; set; }
    public ComplianceLevel ComplianceLevel { get; set; }
    public string? SessionId { get; set; }
}

public class CommunicationFile
{
    public IFormFile File { get; set; } = null!;
    public CommunicationType CommunicationType { get; set; }
    public string? Description { get; set; }
    public DateTime CommunicationDate { get; set; }
}

public class CustomerDocumentCollection
{
    public string CustomerId { get; set; } = string.Empty;
    public List<CustomerDocumentResult> ProfileDocuments { get; set; } = new();
    public List<CustomerDocumentResult> ContractDocuments { get; set; } = new();
    public List<CustomerDocumentResult> CommunicationDocuments { get; set; } = new();
    public List<CustomerDocumentResult> ComplianceDocuments { get; set; } = new();
}

public class CustomerArchiveResult
{
    public string CustomerId { get; set; } = string.Empty;
    public string ArchiveId { get; set; } = string.Empty;
    public DateTime ArchivedAt { get; set; }
    public string ArchivedBy { get; set; } = string.Empty;
    public CustomerArchiveReason Reason { get; set; }
    public string ArchiveBasePath { get; set; } = string.Empty;
    public List<CustomerDocumentResult> ArchivedDocuments { get; set; } = new();
    public int TotalDocumentsArchived { get; set; }
}

#endregion