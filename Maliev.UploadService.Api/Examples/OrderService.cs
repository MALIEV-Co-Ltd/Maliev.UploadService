using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Examples.Shared;

namespace Maliev.UploadService.Api.Examples;

/// <summary>
/// Example implementation showing how Order Service implements domain-specific storage patterns.
///
/// This demonstrates a different approach from QuotationService:
/// - Order Service uses stage-based organization (design/production/shipping)
/// - Implements batch operations for production workflows
/// - Has different retention and security requirements
/// </summary>
public class OrderService : IOrderService
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IFileStorageService storageService,
        ILogger<OrderService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload order document with stage-specific path organization
    /// </summary>
    public async Task<OrderDocumentResult> UploadOrderDocumentAsync(
        string orderId,
        IFormFile document,
        OrderStage stage,
        OrderDocumentType documentType,
        string uploadedBy)
    {
        try
        {
            // Order service uses stage-based organization
            var objectPath = GenerateOrderDocumentPath(orderId, document.FileName, stage, documentType);

            _logger.LogInformation("Uploading order document for {OrderId}: {Stage}/{DocumentType} to {Path}",
                orderId, stage, documentType, objectPath);

            var uploadResult = await _storageService.UploadFileToPathAsync(
                objectPath: objectPath,
                file: document,
                uploadedBy: uploadedBy,
                options: new StorageOptions
                {
                    RetentionDays = GetRetentionPeriod(stage, documentType),
                    EnableVersioning = IsVersioningRequired(stage, documentType),
                    EnableEncryption = IsEncryptionRequired(documentType)
                },
                metadata: new Dictionary<string, string>
                {
                    ["orderId"] = orderId,
                    ["stage"] = stage.ToString(),
                    ["documentType"] = documentType.ToString(),
                    ["serviceName"] = "order-service",
                    ["businessCategory"] = "production"
                });

            // Update order tracking with document reference
            await UpdateOrderDocumentTrackingAsync(orderId, stage, documentType, objectPath, uploadResult);

            return new OrderDocumentResult
            {
                OrderId = orderId,
                Stage = stage,
                DocumentType = documentType,
                ObjectPath = objectPath,
                FileId = uploadResult.FileId,
                UploadedAt = uploadResult.UploadedAt,
                FileSize = uploadResult.FileSize,
                Status = OrderDocumentStatus.Active
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload order document for {OrderId}/{Stage}/{DocumentType}",
                orderId, stage, documentType);
            throw;
        }
    }

    /// <summary>
    /// Batch upload multiple production files for an order
    /// </summary>
    public async Task<List<OrderDocumentResult>> UploadProductionBatchAsync(
        string orderId,
        List<ProductionFile> productionFiles,
        string uploadedBy)
    {
        var results = new List<OrderDocumentResult>();

        try
        {
            _logger.LogInformation("Starting batch upload for order {OrderId}: {FileCount} files",
                orderId, productionFiles.Count);

            // Create batch folder for this production run
            var batchId = Guid.NewGuid().ToString("N")[..8];
            var batchTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            foreach (var productionFile in productionFiles)
            {
                try
                {
                    var objectPath = GenerateProductionBatchPath(orderId, batchId, batchTimestamp,
                        productionFile.File.FileName, productionFile.FileType);

                    var uploadResult = await _storageService.UploadFileToPathAsync(
                        objectPath: objectPath,
                        file: productionFile.File,
                        uploadedBy: uploadedBy,
                        options: new StorageOptions
                        {
                            RetentionDays = 2555, // 7 years for production records
                            EnableVersioning = false, // Production files are immutable
                            EnableEncryption = true
                        },
                        metadata: new Dictionary<string, string>
                        {
                            ["orderId"] = orderId,
                            ["batchId"] = batchId,
                            ["fileType"] = productionFile.FileType.ToString(),
                            ["serviceName"] = "order-service",
                            ["businessCategory"] = "production"
                        });

                    results.Add(new OrderDocumentResult
                    {
                        OrderId = orderId,
                        Stage = OrderStage.Production,
                        DocumentType = OrderDocumentType.ProductionFile,
                        ObjectPath = objectPath,
                        FileId = uploadResult.FileId,
                        UploadedAt = uploadResult.UploadedAt,
                        FileSize = uploadResult.FileSize,
                        Status = OrderDocumentStatus.Active,
                        BatchId = batchId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload production file {FileName} for order {OrderId}",
                        productionFile.File.FileName, orderId);
                    // Continue with other files
                }
            }

            // Update order with batch information
            await UpdateOrderProductionBatchAsync(orderId, batchId, results);

            _logger.LogInformation("Completed batch upload for order {OrderId}: {SuccessCount}/{TotalCount} files uploaded",
                orderId, results.Count, productionFiles.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload production batch for order {OrderId}", orderId);
            throw;
        }
    }

    /// <summary>
    /// Get all documents for an order organized by stage
    /// </summary>
    public async Task<OrderDocumentCollection> GetOrderDocumentsAsync(string orderId)
    {
        try
        {
            var documents = await GetOrderDocumentsFromDatabaseAsync(orderId);

            return new OrderDocumentCollection
            {
                OrderId = orderId,
                DesignDocuments = documents.Where(d => d.Stage == OrderStage.Design).ToList(),
                ProductionDocuments = documents.Where(d => d.Stage == OrderStage.Production).ToList(),
                QualityDocuments = documents.Where(d => d.Stage == OrderStage.QualityControl).ToList(),
                ShippingDocuments = documents.Where(d => d.Stage == OrderStage.Shipping).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order documents for {OrderId}", orderId);
            throw;
        }
    }

    /// <summary>
    /// Create order completion package with all relevant documents
    /// </summary>
    public async Task<OrderCompletionPackage> CreateOrderCompletionPackageAsync(string orderId, string requestedBy)
    {
        try
        {
            _logger.LogInformation("Creating order completion package for {OrderId}", orderId);

            var orderDocuments = await GetOrderDocumentsAsync(orderId);
            var packageId = Guid.NewGuid().ToString("N")[..8];
            var packagePath = $"orders/{orderId}/packages/completion_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{packageId}";

            var package = new OrderCompletionPackage
            {
                PackageId = packageId,
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = requestedBy,
                PackagePath = packagePath,
                Documents = new List<OrderDocumentResult>()
            };

            // Copy key documents to package location
            var keyDocuments = new[]
            {
                orderDocuments.DesignDocuments.FirstOrDefault(d => d.DocumentType == OrderDocumentType.FinalDesign),
                orderDocuments.ProductionDocuments.FirstOrDefault(d => d.DocumentType == OrderDocumentType.QualityReport),
                orderDocuments.ShippingDocuments.FirstOrDefault(d => d.DocumentType == OrderDocumentType.ShippingLabel)
            }.Where(d => d != null);

            foreach (var document in keyDocuments)
            {
                if (document == null) continue;

                try
                {
                    // Download original
                    var fileContent = await _storageService.DownloadFileByPathAsync(document.ObjectPath, requestedBy);
                    if (fileContent != null)
                    {
                        // Upload to package location
                        var packageDocumentPath = $"{packagePath}/{Path.GetFileName(document.ObjectPath)}";
                        using var stream = new MemoryStream(fileContent.Content);
                        var formFile = new FormFileWrapper(stream, fileContent.FileName, fileContent.ContentType);

                        var packageUpload = await _storageService.UploadFileToPathAsync(
                            packageDocumentPath, formFile, requestedBy);

                        package.Documents.Add(new OrderDocumentResult
                        {
                            OrderId = orderId,
                            Stage = document.Stage,
                            DocumentType = document.DocumentType,
                            ObjectPath = packageDocumentPath,
                            FileId = packageUpload.FileId,
                            UploadedAt = packageUpload.UploadedAt,
                            FileSize = packageUpload.FileSize,
                            Status = OrderDocumentStatus.Active,
                            IsPackaged = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add document {ObjectPath} to completion package",
                        document.ObjectPath);
                }
            }

            // Save package metadata
            await SaveOrderCompletionPackageAsync(package);

            _logger.LogInformation("Created order completion package {PackageId} for {OrderId} with {DocumentCount} documents",
                packageId, orderId, package.Documents.Count);

            return package;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order completion package for {OrderId}", orderId);
            throw;
        }
    }

    #region Private Business Logic Methods

    private static string GenerateOrderDocumentPath(string orderId, string fileName, OrderStage stage, OrderDocumentType documentType)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedFileName = SanitizeFileName(fileName);
        var stageFolder = stage.ToString().ToLowerInvariant();
        var typeFolder = documentType.ToString().ToLowerInvariant();

        // Order service uses stage-based organization
        return $"orders/{orderId}/{stageFolder}/{typeFolder}/{timestamp}_{sanitizedFileName}";
    }

    private static string GenerateProductionBatchPath(string orderId, string batchId, string batchTimestamp, string fileName, ProductionFileType fileType)
    {
        var sanitizedFileName = SanitizeFileName(fileName);
        var typeFolder = fileType.ToString().ToLowerInvariant();

        return $"orders/{orderId}/production/batches/{batchTimestamp}_{batchId}/{typeFolder}/{sanitizedFileName}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int GetRetentionPeriod(OrderStage stage, OrderDocumentType documentType)
    {
        return stage switch
        {
            OrderStage.Design => 1095,      // 3 years
            OrderStage.Production => 2555,  // 7 years (compliance requirement)
            OrderStage.QualityControl => 2555, // 7 years
            OrderStage.Shipping => 1825,    // 5 years
            _ => 1095
        };
    }

    private static bool IsVersioningRequired(OrderStage stage, OrderDocumentType documentType)
    {
        return stage == OrderStage.Design && documentType == OrderDocumentType.TechnicalDrawing;
    }

    private static bool IsEncryptionRequired(OrderDocumentType documentType)
    {
        return documentType == OrderDocumentType.CustomerInformation ||
               documentType == OrderDocumentType.PaymentDetails;
    }

    // Database operation stubs - would be implemented with actual database calls
    private Task<List<OrderDocumentResult>> GetOrderDocumentsFromDatabaseAsync(string orderId)
    {
        throw new NotImplementedException("Implement with actual database query");
    }

    private Task UpdateOrderDocumentTrackingAsync(string orderId, OrderStage stage, OrderDocumentType documentType, string objectPath, FileUploadResponse uploadResult)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task UpdateOrderProductionBatchAsync(string orderId, string batchId, List<OrderDocumentResult> documents)
    {
        throw new NotImplementedException("Implement with actual database update");
    }

    private Task SaveOrderCompletionPackageAsync(OrderCompletionPackage package)
    {
        throw new NotImplementedException("Implement with actual database save");
    }

    #endregion
}

#region Supporting Types

public interface IOrderService
{
    Task<OrderDocumentResult> UploadOrderDocumentAsync(string orderId, IFormFile document, OrderStage stage, OrderDocumentType documentType, string uploadedBy);
    Task<List<OrderDocumentResult>> UploadProductionBatchAsync(string orderId, List<ProductionFile> productionFiles, string uploadedBy);
    Task<OrderDocumentCollection> GetOrderDocumentsAsync(string orderId);
    Task<OrderCompletionPackage> CreateOrderCompletionPackageAsync(string orderId, string requestedBy);
}

public enum OrderStage
{
    Design,
    Production,
    QualityControl,
    Shipping
}

public enum OrderDocumentType
{
    CustomerRequirements,
    TechnicalDrawing,
    FinalDesign,
    ProductionInstructions,
    ProductionFile,
    QualityReport,
    ShippingLabel,
    DeliveryConfirmation,
    CustomerInformation,
    PaymentDetails
}

public enum ProductionFileType
{
    Photo,
    Video,
    Measurement,
    TestResult,
    QualityCheck
}

public enum OrderDocumentStatus
{
    Active,
    Archived,
    Deleted
}

public class OrderDocumentResult
{
    public string OrderId { get; set; } = string.Empty;
    public OrderStage Stage { get; set; }
    public OrderDocumentType DocumentType { get; set; }
    public string ObjectPath { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
    public OrderDocumentStatus Status { get; set; }
    public string? BatchId { get; set; }
    public bool IsPackaged { get; set; }
}

public class ProductionFile
{
    public IFormFile File { get; set; } = null!;
    public ProductionFileType FileType { get; set; }
    public string? Description { get; set; }
}

public class OrderDocumentCollection
{
    public string OrderId { get; set; } = string.Empty;
    public List<OrderDocumentResult> DesignDocuments { get; set; } = new();
    public List<OrderDocumentResult> ProductionDocuments { get; set; } = new();
    public List<OrderDocumentResult> QualityDocuments { get; set; } = new();
    public List<OrderDocumentResult> ShippingDocuments { get; set; } = new();
}

public class OrderCompletionPackage
{
    public string PackageId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string PackagePath { get; set; } = string.Empty;
    public List<OrderDocumentResult> Documents { get; set; } = new();
}

#endregion