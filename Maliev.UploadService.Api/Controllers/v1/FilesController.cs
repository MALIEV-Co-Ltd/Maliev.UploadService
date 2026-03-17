using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.MessagingContracts.Contracts.Uploads;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts;
using Maliev.UploadService.Api.Extensions;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Maliev.UploadService.Api.Controllers.v1;

/// <summary>
/// Controller for file management operations (FR-029, FR-030, FR-031)
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("upload/v{version:apiVersion}/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IAuthorizationPolicyService _authorizationService;
    private readonly UploadDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FilesController> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private const int SignedUrlCacheExpirationMinutes = 5;

    /// <summary>
    /// Initializes a new instance of the FilesController class.
    /// </summary>
    /// <param name="storageService">The storage service.</param>
    /// <param name="authorizationService">The authorization policy service.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="logger">The logger for this controller.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    public FilesController(
        IStorageService storageService,
        IAuthorizationPolicyService authorizationService,
        UploadDbContext dbContext,
        IDistributedCache cache,
        ILogger<FilesController> logger,
        IPublishEndpoint publishEndpoint)
    {
        _storageService = storageService;
        _authorizationService = authorizationService;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Get file metadata by upload ID with authorization check
    /// </summary>
    [HttpGet("{uploadId}")]
    [RequirePermission(UploadPermissions.FilesRead, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(FileMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFileMetadata(
        string uploadId,
        CancellationToken cancellationToken)
    {
        var serviceId = User.Identity?.Name ?? "unknown";

        // Retrieve file metadata from database
        var fileMetadata = await _dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == uploadId, cancellationToken);

        if (fileMetadata == null)
        {
            _logger.LogWarning("File not found for UploadId: {UploadId}", uploadId);
            return NotFound(new { error = "File not found" });
        }

        // T097: Authorization check - ensure service can access this file (Resource-scoped)
        var canAccess = await _authorizationService.CanAccessPathAsync(
            serviceId,
            fileMetadata.StoragePath,
            cancellationToken);

        if (!canAccess)
        {
            _logger.LogWarning(
                "Unauthorized access attempt by service {ServiceId} to file {UploadId} at path {StoragePath}",
                serviceId, uploadId, fileMetadata.StoragePath);

            // T100: Audit logging
            await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
                UploadEventType.FileRetrieved, EventResult.Failure, cancellationToken);

            return Forbid();
        }

        // Update last accessed timestamp (use raw SQL to avoid concurrency conflicts on reads)
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE file_metadata SET last_accessed_at = @p0 WHERE upload_id = @p1",
            new object[] { DateTime.UtcNow, uploadId },
            cancellationToken);

        // T100: Audit logging
        await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
            UploadEventType.FileRetrieved, EventResult.Success, cancellationToken);

        // T101: Metrics instrumentation
        _logger.LogDebug(
            "File metadata retrieved. UploadId: {UploadId}, Service: {ServiceId}",
            uploadId, serviceId);

        return Ok(fileMetadata.ToResponse());
    }

    /// <summary>
    /// Query files by path prefix with pagination and authorization
    /// </summary>
    [HttpGet]
    [RequirePermission(UploadPermissions.FilesList, RequireLiveCheck = true, ResourcePathTemplate = "folders/{request.PathPrefix}")]
    [ProducesResponseType(typeof(QueryFilesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryFiles(
        [FromQuery] QueryFilesRequest request,
        CancellationToken cancellationToken)
    {
        var serviceId = User.Identity?.Name ?? "unknown";

        // T098: Authorization check - ensure service can access the path prefix
        if (!string.IsNullOrWhiteSpace(request.PathPrefix))
        {
            var canAccess = await _authorizationService.CanAccessPathAsync(
                serviceId,
                request.PathPrefix,
                cancellationToken);

            if (!canAccess)
            {
                _logger.LogWarning(
                    "Unauthorized query attempt by service {ServiceId} for path prefix {PathPrefix}",
                    serviceId, request.PathPrefix);
                return Forbid();
            }
        }

        // Build query with path prefix filter
        var query = _dbContext.FileMetadata.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.PathPrefix))
        {
            query = query.Where(f => f.StoragePath.StartsWith(request.PathPrefix));
        }
        else
        {
            // If no path prefix specified, filter by service ID
            query = query.Where(f => f.ServiceId == serviceId);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var files = await query
            .OrderByDescending(f => f.UploadedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        // T101: Metrics instrumentation
        _logger.LogDebug(
            "Files queried. Service: {ServiceId}, PathPrefix: {PathPrefix}, Count: {Count}",
            serviceId, request.PathPrefix ?? "(all)", files.Count);

        return Ok(new QueryFilesResponse
        {
            Files = files.Select(f => f.ToResponse()).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// Generate signed URL for file download with caching
    /// </summary>
    [HttpPost("{uploadId}/signed-url")]
    [RequirePermission(UploadPermissions.FilesDownload, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(SignedUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateSignedUrl(
        string uploadId,
        [FromBody] GenerateSignedUrlRequest request,
        CancellationToken cancellationToken)
    {
        var serviceId = User.Identity?.Name ?? "unknown";

        // Retrieve file metadata
        var fileMetadata = await _dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == uploadId, cancellationToken);

        if (fileMetadata == null)
        {
            _logger.LogWarning("File not found for signed URL generation. UploadId: {UploadId}", uploadId);
            return NotFound(new { error = "File not found" });
        }

        // T099: Authorization check
        var canAccess = await _authorizationService.CanAccessPathAsync(
            serviceId,
            fileMetadata.StoragePath,
            cancellationToken);

        if (!canAccess)
        {
            _logger.LogWarning(
                "Unauthorized signed URL request by service {ServiceId} for file {UploadId}",
                serviceId, uploadId);

            // T100: Audit logging
            await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
                UploadEventType.SignedUrlGenerated, EventResult.Failure, cancellationToken);

            return Forbid();
        }

        // Check cache for existing signed URL
        var cacheKey = $"signed-url:{uploadId}:{request.ExpirationMinutes}";
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);

        string signedUrl;
        DateTime expiresAt;

        if (!string.IsNullOrEmpty(cachedData))
        {
            var parts = cachedData.Split('|');
            if (parts.Length == 2 && DateTime.TryParse(parts[1], out expiresAt))
            {
                signedUrl = parts[0];
                _logger.LogDebug("Using cached signed URL for UploadId: {UploadId}", uploadId);

                return Ok(new SignedUrlResponse
                {
                    SignedUrl = signedUrl,
                    ExpiresAt = expiresAt,
                    UploadId = uploadId,
                    StoragePath = fileMetadata.StoragePath
                });
            }
        }

        // Generate new signed URL
        var expiration = TimeSpan.FromMinutes(request.ExpirationMinutes);
        signedUrl = await _storageService.GenerateSignedUrlAsync(
            fileMetadata.StoragePath,
            expiration,
            cancellationToken);

        expiresAt = DateTime.UtcNow.Add(expiration);

        // Cache the signed URL and its absolute expiration for 5 minutes
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(
                Math.Min(SignedUrlCacheExpirationMinutes, request.ExpirationMinutes))
        };
        await _cache.SetStringAsync(cacheKey, $"{signedUrl}|{expiresAt:O}", cacheOptions, cancellationToken);

        // T100: Audit logging
        await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
            UploadEventType.SignedUrlGenerated, EventResult.Success, cancellationToken);

        // T101: Metrics instrumentation
        _logger.LogInformation(
            "Signed URL generated. UploadId: {UploadId}, Service: {ServiceId}, ExpirationMinutes: {ExpirationMinutes}",
            uploadId, serviceId, request.ExpirationMinutes);

        return Ok(new SignedUrlResponse
        {
            SignedUrl = signedUrl,
            ExpiresAt = expiresAt,
            UploadId = uploadId,
            StoragePath = fileMetadata.StoragePath
        });
    }

    /// <summary>
    /// Delete file with authorization and retention policy checks (User Story 5)
    /// </summary>
    [HttpDelete("{uploadId}")]
    [RequirePermission(UploadPermissions.FilesDelete, RequireLiveCheck = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFile(
        string uploadId,
        CancellationToken cancellationToken)
    {
        var serviceId = User.Identity?.Name ?? "unknown";

        // Retrieve file metadata
        var fileMetadata = await _dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == uploadId, cancellationToken);

        if (fileMetadata == null)
        {
            _logger.LogWarning("File not found for deletion. UploadId: {UploadId}", uploadId);
            return NotFound(new { error = "File not found" });
        }

        // T121: Authorization check
        var canAccess = await _authorizationService.CanAccessPathAsync(
            serviceId,
            fileMetadata.StoragePath,
            cancellationToken);

        if (!canAccess)
        {
            _logger.LogWarning(
                "Unauthorized delete attempt by service {ServiceId} for file {UploadId}",
                serviceId, uploadId);

            await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
                UploadEventType.FileDeleted, EventResult.Failure, cancellationToken);

            return Forbid();
        }

        // T119: Delete from GCS
        await _storageService.DeleteFileAsync(fileMetadata.StoragePath, cancellationToken);

        // T123: Delete FileMetadata and Upload entities
        var upload = await _dbContext.Uploads
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, cancellationToken);

        if (upload != null)
        {
            _dbContext.Uploads.Remove(upload);
        }

        _dbContext.FileMetadata.Remove(fileMetadata);

        // T124: Audit logging
        await LogFileEventAsync(uploadId, serviceId, fileMetadata.StoragePath,
            UploadEventType.FileDeleted, EventResult.Success, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // T125: Metrics instrumentation
        _logger.LogInformation(
            "File deleted. UploadId: {UploadId}, Service: {ServiceId}, Path: {StoragePath}",
            uploadId, serviceId, fileMetadata.StoragePath);

        // T160: Publish FileDeletedEvent (FR-025)
        await _publishEndpoint.Publish(new FileDeletedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "FileDeletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "UploadService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new FileDeletedEventPayload(
                FileId: fileMetadata.FileId,
                UploadId: uploadId,
                ServiceId: serviceId,
                StoragePath: fileMetadata.StoragePath,
                DeletedAt: DateTimeOffset.UtcNow,
                DeletedBy: serviceId,
                Reason: "User requested deletion"
            )
        ), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Log file-related events for audit trail
    /// </summary>
    private async Task LogFileEventAsync(
        string uploadId,
        string serviceId,
        string storagePath,
        UploadEventType eventType,
        EventResult eventResult,
        CancellationToken cancellationToken)
    {
        var uploadEvent = new UploadEvent
        {
            EventId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            EventType = eventType,
            EventTimestamp = DateTime.UtcNow,
            ServiceId = serviceId,
            StoragePath = storagePath,
            EventResult = eventResult,
            ErrorDetails = eventResult == EventResult.Failure
                ? $"{eventType} failed"
                : null
        };

        _dbContext.UploadEvents.Add(uploadEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
