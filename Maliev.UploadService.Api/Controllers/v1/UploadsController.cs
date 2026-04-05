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
using System.Security.Cryptography;

namespace Maliev.UploadService.Api.Controllers.v1;

/// <summary>
/// Controller for handling file uploads and related operations.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("upload/v{version:apiVersion}/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IValidationService _validationService;
    private readonly IStorageService _storageService;
    private readonly ILifecycleManagementService _lifecycleService;
    private readonly UploadDbContext _dbContext;
    private readonly ILogger<UploadsController> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the UploadsController class.
    /// </summary>
    /// <param name="validationService">The validation service.</param>
    /// <param name="storageService">The storage service.</param>
    /// <param name="lifecycleService">The lifecycle management service.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger for this controller.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    public UploadsController(
        IValidationService validationService,
        IStorageService storageService,
        ILifecycleManagementService lifecycleService,
        UploadDbContext dbContext,
        ILogger<UploadsController> logger,
        IPublishEndpoint publishEndpoint)
    {
        _validationService = validationService;
        _storageService = storageService;
        _lifecycleService = lifecycleService;
        _dbContext = dbContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Uploads a file to the storage service and returns the file identifier.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequirePermission(UploadPermissions.FilesUpload, ResourcePathTemplate = "folders/{request.Path}")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadFile(
        [FromForm] UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        var uploadId = Guid.NewGuid();
        var serviceName = request.ServiceName ?? User.Identity?.Name ?? "unknown";

        try
        {
            // T107: Resolve path placeholders (FR-007, FR-009)
            var placeholders = new Dictionary<string, string>
            {
                { "id", uploadId.ToString("N") }, // Use compact GUID format (no hyphens)
                { "timestamp", DateTime.UtcNow.ToString("yyyyMMddHHmmss") },
                { "date", DateTime.UtcNow.ToString("yyyyMMdd") },
                { "year", DateTime.UtcNow.Year.ToString() },
                { "month", DateTime.UtcNow.Month.ToString("D2") },
                { "day", DateTime.UtcNow.Day.ToString("D2") },
                { "service", serviceName }
            };

            var resolvedPath = request.Path.ResolvePlaceholders(placeholders);

            // T113: Path sanitization with audit logging for traversal attempts (FR-008, FR-021)
            string sanitizedPath;
            try
            {
                sanitizedPath = resolvedPath.SanitizePath();
            }
            catch (ArgumentException ex)
            {
                // T113: Audit log path traversal attempt
                _logger.LogWarning("Path traversal attempt detected by service {ServiceName}: {OriginalPath} - {Error}",
                    serviceName, request.Path, ex.Message);
                await LogUploadEventAsync(uploadId, serviceName, request.Path, "PathTraversalAttempt", cancellationToken);
                return BadRequest(new { error = $"Invalid path: {ex.Message}" });
            }

            // T103: Check for path collision (FR-010)
            var existingUpload = await _dbContext.Uploads
                .FirstOrDefaultAsync(u => u.StoragePath == sanitizedPath, cancellationToken);

            if (existingUpload != null)
            {
                if (!request.Overwrite)
                {
                    _logger.LogWarning("File already exists at path {Path} and overwrite is disabled", sanitizedPath);
                    await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "PathCollision", cancellationToken);
                    return Conflict(new { error = $"File already exists at path '{sanitizedPath}'. Set Overwrite=true to replace it." });
                }

                // Delete existing upload and file metadata before overwriting
                var existingFileMetadata = await _dbContext.FileMetadata
                    .FirstOrDefaultAsync(f => f.UploadId == existingUpload.UploadId, cancellationToken);

                if (existingFileMetadata != null)
                {
                    _dbContext.FileMetadata.Remove(existingFileMetadata);
                }

                _dbContext.Uploads.Remove(existingUpload);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Overwriting existing file at path {Path}", sanitizedPath);
            }

            // T076: File validation
            using var fileStream = request.File.OpenReadStream();
            var validationResult = await _validationService.ValidateFileAsync(
                fileStream,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                cancellationToken);

            if (validationResult.Warnings.Count > 0)
            {
                _logger.LogWarning("File validation warnings for {FileName}: {Warnings}",
                    request.File.FileName, string.Join(", ", validationResult.Warnings));
            }

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("File validation failed for {FileName}: {Errors}",
                    request.File.FileName, string.Join(", ", validationResult.Errors));
                await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "ValidationFailed", cancellationToken);
                return BadRequest(new { errors = validationResult.Errors });
            }

            // T077: GCS upload
            fileStream.Position = 0; // Reset stream after validation

            var uploadResult = await _storageService.UploadFileAsync(
                fileStream,
                sanitizedPath,
                request.File.ContentType,
                request.Overwrite,
                cancellationToken);

            // Convert Base64 MD5 from GCS to Hex string format
            string? checksum = null;
            if (!string.IsNullOrEmpty(uploadResult.Md5Hash))
            {
                try
                {
                    checksum = BitConverter.ToString(Convert.FromBase64String(uploadResult.Md5Hash))
                                           .Replace("-", "").ToLowerInvariant();
                }
                catch
                {
                    // Fallback if decode fails
                }
            }
            checksum ??= "UNKNOWN";

            // T078: Upload entity persistence
            var upload = new Upload
            {
                UploadId = uploadId.ToString(),
                ServiceId = serviceName,
                FileName = request.File.FileName,
                StoragePath = uploadResult.StoragePath,
                ContentType = uploadResult.ContentType,
                FileSize = uploadResult.SizeBytes,
                BytesUploaded = uploadResult.SizeBytes,
                Status = UploadStatus.Completed,
                UploadedAt = uploadResult.UploadedAt,
                CompletedAt = DateTime.UtcNow
            };

            _dbContext.Uploads.Add(upload);

            // T079: FileMetadata entity creation
            var fileMetadata = new FileMetadata
            {
                FileId = Guid.NewGuid().ToString(),
                UploadId = uploadId.ToString(),
                ServiceId = serviceName,
                StoragePath = uploadResult.StoragePath,
                VersionETag = uploadResult.ETag,
                FileSize = uploadResult.SizeBytes,
                ContentType = uploadResult.ContentType,
                Checksum = checksum,
                UploadedAt = uploadResult.UploadedAt,
                Metadata = request.Metadata != null
                    ? new Dictionary<string, string> { { "custom", request.Metadata } }
                    : null
            };

            // T132: Apply retention policy if specified or find applicable policy
            string? retentionPolicyId = request.RetentionPolicyId;

            if (string.IsNullOrEmpty(retentionPolicyId))
            {
                // Try to find an applicable retention policy based on service and path
                var applicablePolicy = await _lifecycleService.GetActiveRetentionPolicyAsync(
                    serviceName,
                    sanitizedPath,
                    cancellationToken);

                if (applicablePolicy != null)
                {
                    retentionPolicyId = applicablePolicy.PolicyId;
                    _logger.LogInformation(
                        "Auto-applied retention policy {PolicyName} to upload {UploadId}",
                        applicablePolicy.PolicyName, uploadId);
                }
            }

            if (!string.IsNullOrEmpty(retentionPolicyId))
            {
                fileMetadata.RetentionPolicyId = retentionPolicyId;
                var expiresAt = await _lifecycleService.ApplyRetentionPolicyAsync(
                    fileMetadata,
                    retentionPolicyId,
                    cancellationToken);

                fileMetadata.ExpiresAt = expiresAt;
            }

            _dbContext.FileMetadata.Add(fileMetadata);

            // T080: Audit logging
            await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "Success", cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            // T081: Metrics instrumentation (using ILogger for now, can be enhanced with OpenTelemetry)
            _logger.LogInformation(
                "File uploaded successfully. UploadId: {UploadId}, Service: {ServiceName}, Size: {SizeBytes}",
                uploadId, serviceName, uploadResult.SizeBytes);

            // Generate signed URL for downstream services (like GeometryService)
            // Valid for 1 hour
            var downloadUrl = await _storageService.GenerateSignedUrlAsync(
                uploadResult.StoragePath,
                TimeSpan.FromHours(1),
                cancellationToken);

            // T158: Publish FileUploadedEvent (FR-025)
            await _publishEndpoint.Publish(new FileUploadedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "FileUploadedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0.0",
                PublishedBy: "UploadService",
                ConsumedBy: ["GeometryService", "NotificationService"],
                CorrelationId: Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new FileUploadedEventPayload(
                    UploadId: uploadId.ToString(),
                    ServiceId: serviceName,
                    FileName: request.File.FileName,
                    StoragePath: uploadResult.StoragePath,
                    ContentType: uploadResult.ContentType,
                    FileSize: (int)uploadResult.SizeBytes,
                    DownloadUrl: downloadUrl,
                    UploadedAt: new DateTimeOffset(uploadResult.UploadedAt, TimeSpan.Zero),
                    RetentionPolicyId: fileMetadata.RetentionPolicyId,
                    ExpiresAt: fileMetadata.ExpiresAt.HasValue ? new DateTimeOffset(fileMetadata.ExpiresAt.Value, TimeSpan.Zero) : null,
                    Metadata: fileMetadata.Metadata!
                )
            ), cancellationToken);

            return Ok(upload.ToResponse());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            // T113: Audit log path collision attempt (FR-010, FR-021)
            _logger.LogWarning(ex, "Path collision detected for {FileName} at {Path}: {Message}",
                request.File.FileName, request.Path, ex.Message);
            await LogUploadEventAsync(uploadId, serviceName, request.Path, "PathCollision", cancellationToken);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Upload failed for {FileName}: {Message}",
                request.File.FileName, ex.Message);
            await LogUploadEventAsync(uploadId, serviceName, request.Path, "Failed", cancellationToken);

            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file upload. UploadId: {UploadId}", uploadId);
            await LogUploadEventAsync(uploadId, serviceName, request.Path, "Error", cancellationToken);

            return StatusCode(500, new { error = "An error occurred during file upload" });
        }
    }

    /// <summary>
    /// POST /api/v1/uploads/resumable - Initiates a resumable upload session (FR-022)
    /// </summary>
    [HttpPost("resumable")]
    [RequirePermission(UploadPermissions.FilesUpload, ResourcePathTemplate = "folders/{request.Path}")]
    [ProducesResponseType(typeof(InitiateResumableUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InitiateResumableUpload(
        [FromBody] InitiateResumableUploadRequest request,
        CancellationToken cancellationToken)
    {
        var uploadId = Guid.NewGuid();
        var serviceName = request.ServiceName ?? User.Identity?.Name ?? "unknown";

        try
        {
            // Path sanitization
            var sanitizedPath = request.Path.SanitizePath();

            // Initiate resumable upload session with GCS
            var session = await _storageService.InitiateResumableUploadAsync(
                sanitizedPath,
                request.ContentType,
                request.TotalSize,
                cancellationToken);

            // T149: Create Upload entity with session URI tracking
            var upload = new Upload
            {
                UploadId = uploadId.ToString(),
                ServiceId = serviceName,
                FileName = System.IO.Path.GetFileName(sanitizedPath),
                StoragePath = sanitizedPath,
                ContentType = request.ContentType,
                FileSize = request.TotalSize,
                Checksum = request.Checksum, // Store client-provided checksum
                BytesUploaded = 0,
                Status = UploadStatus.InProgress,
                UploadedAt = DateTime.UtcNow,
                SessionUri = session.SessionUri // Store session URI for resumability
            };

            _dbContext.Uploads.Add(upload);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Resumable upload initiated. UploadId: {UploadId}, Service: {ServiceName}, TotalSize: {TotalSize}",
                uploadId, serviceName, request.TotalSize);

            return Ok(new InitiateResumableUploadResponse
            {
                UploadId = uploadId.ToString(),
                SessionUri = session.SessionUri,
                ExpiresAt = session.ExpiresAt,
                TotalSize = request.TotalSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate resumable upload. UploadId: {UploadId}", uploadId);
            return StatusCode(500, new { error = "Failed to initiate resumable upload" });
        }
    }

    /// <summary>
    /// PUT /api/v1/uploads/resumable/{uploadId} - Continues a resumable upload (FR-022)
    /// </summary>
    [HttpPut("resumable/{uploadId}")]
    [ProducesResponseType(typeof(ResumeUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)] // Resume Incomplete
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeUpload(
        [FromRoute] string uploadId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find upload session
            var upload = await _dbContext.Uploads.FindAsync(new object[] { uploadId }, cancellationToken);
            if (upload == null)
            {
                return NotFound(new { error = "Upload session not found" });
            }

            if (string.IsNullOrEmpty(upload.SessionUri))
            {
                return BadRequest(new { error = "Upload session is not resumable" });
            }

            // Read request body as stream
            var contentRange = Request.Headers.ContentRange.ToString();
            if (string.IsNullOrEmpty(contentRange))
            {
                return BadRequest(new { error = "Content-Range header is required" });
            }

            // Parse Content-Range header: "bytes 0-1048575/10485760"
            var rangeParts = contentRange.Replace("bytes ", "").Split('/');
            if (rangeParts.Length != 2)
            {
                return BadRequest(new { error = "Invalid Content-Range header format" });
            }

            var byteRange = rangeParts[0].Split('-');
            if (byteRange.Length != 2 ||
                !long.TryParse(byteRange[0], out var startByte) ||
                !long.TryParse(byteRange[1], out var endByte) ||
                !long.TryParse(rangeParts[1], out var totalSize))
            {
                return BadRequest(new { error = "Invalid Content-Range values" });
            }

            // Resume upload with chunk
            var progress = await _storageService.ResumeUploadAsync(
                upload.SessionUri,
                Request.Body,
                startByte,
                endByte,
                totalSize,
                cancellationToken);

            // Update upload entity
            upload.BytesUploaded = progress.BytesReceived;

            if (progress.IsComplete)
            {
                upload.Status = UploadStatus.Completed;
                upload.CompletedAt = DateTime.UtcNow;

                // Get actual ETag from storage if possible
                var gcsMetadata = await _storageService.GetFileMetadataAsync(upload.StoragePath, cancellationToken);

                // Convert Base64 MD5 from GCS to Hex string format matching SHA256 standard
                string? gcsMd5Hex = null;
                if (!string.IsNullOrEmpty(gcsMetadata?.Md5Hash))
                {
                    try
                    {
                        gcsMd5Hex = BitConverter.ToString(Convert.FromBase64String(gcsMetadata.Md5Hash))
                                                .Replace("-", "").ToLowerInvariant();
                    }
                    catch
                    {
                        // Fallback to original if decode fails
                    }
                }

                // Determine final checksum: Client provided -> GCS MD5 -> UNKNOWN
                var finalChecksum = upload.Checksum ?? gcsMd5Hex ?? "UNKNOWN";

                // Create FileMetadata entity
                var fileMetadata = new FileMetadata
                {
                    FileId = Guid.NewGuid().ToString(),
                    UploadId = uploadId,
                    ServiceId = upload.ServiceId,
                    StoragePath = upload.StoragePath,
                    VersionETag = gcsMetadata?.ETag ?? Guid.NewGuid().ToString(),
                    FileSize = upload.FileSize,
                    ContentType = upload.ContentType,
                    Checksum = finalChecksum,
                    UploadedAt = DateTime.UtcNow
                };

                _dbContext.FileMetadata.Add(fileMetadata);

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Resumable upload completed. UploadId: {UploadId}, TotalSize: {TotalSize}",
                    uploadId, totalSize);

                // Generate signed URL for downstream services
                var downloadUrl = await _storageService.GenerateSignedUrlAsync(
                    upload.StoragePath,
                    TimeSpan.FromHours(1),
                    cancellationToken);

                // T158: Publish FileUploadedEvent (FR-025)
                await _publishEndpoint.Publish(new FileUploadedEvent(
                    MessageId: Guid.NewGuid(),
                    MessageName: "FileUploadedEvent",
                    MessageType: MessageType.Event,
                    MessageVersion: "1.0.0",
                    PublishedBy: "UploadService",
                    ConsumedBy: ["GeometryService", "NotificationService"],
                    CorrelationId: Guid.NewGuid(),
                    CausationId: null,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    IsPublic: false,
                    Payload: new FileUploadedEventPayload(
                        UploadId: uploadId,
                        ServiceId: upload.ServiceId,
                        FileName: upload.FileName,
                        StoragePath: upload.StoragePath,
                        ContentType: upload.ContentType,
                        FileSize: (int)upload.FileSize,
                        DownloadUrl: downloadUrl,
                        UploadedAt: DateTimeOffset.UtcNow,
                        RetentionPolicyId: fileMetadata.RetentionPolicyId,
                        ExpiresAt: fileMetadata.ExpiresAt.HasValue ? new DateTimeOffset(fileMetadata.ExpiresAt.Value, TimeSpan.Zero) : null,
                        Metadata: fileMetadata.Metadata!
                    )
                ), cancellationToken);

                return Ok(new ResumeUploadResponse
                {
                    UploadId = uploadId,
                    BytesReceived = progress.BytesReceived,
                    TotalSize = progress.TotalSize,
                    IsComplete = true,
                    StoragePath = upload.StoragePath
                });
            }
            else
            {
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Resumable upload progress. UploadId: {UploadId}, BytesReceived: {BytesReceived}/{TotalSize}",
                    uploadId, progress.BytesReceived, progress.TotalSize);

                // Return 308 Resume Incomplete
                Response.StatusCode = 308;
                return new JsonResult(new ResumeUploadResponse
                {
                    UploadId = uploadId,
                    BytesReceived = progress.BytesReceived,
                    TotalSize = progress.TotalSize,
                    IsComplete = false,
                    NextByteRange = $"{progress.BytesReceived}-{progress.TotalSize - 1}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume upload. UploadId: {UploadId}", uploadId);
            return StatusCode(500, new { error = "Failed to resume upload" });
        }
    }

    private async Task LogUploadEventAsync(
        Guid uploadId,
        string serviceName,
        string path,
        string eventTypeString,
        CancellationToken cancellationToken)
    {
        // T113: Sanitize input strings for database (specifically handle null bytes for Postgres)
        serviceName = serviceName?.Replace("\0", "[NULL]") ?? "unknown";
        path = path?.Replace("\0", "[NULL]") ?? "";

        var eventType = eventTypeString switch
        {
            "Success" => UploadEventType.UploadCompleted,
            "Failed" => UploadEventType.UploadFailed,
            "ValidationFailed" => UploadEventType.ValidationFailed,
            "PathTraversalAttempt" or "Unauthorized" => UploadEventType.AuthorizationDenied,
            _ => UploadEventType.UploadInitiated
        };

        var eventResult = eventTypeString switch
        {
            "Success" => EventResult.Success,
            "Unauthorized" or "ValidationFailed" or "Failed" => EventResult.Failure,
            _ => EventResult.Warning
        };

        var uploadEvent = new UploadEvent
        {
            EventId = Guid.NewGuid().ToString(),
            UploadId = uploadId.ToString(),
            EventType = eventType,
            EventTimestamp = DateTime.UtcNow,
            ServiceId = serviceName,
            StoragePath = path,
            EventResult = eventResult,
            ErrorDetails = eventResult == EventResult.Failure ? $"Upload {eventTypeString}" : null
        };

        _dbContext.UploadEvents.Add(uploadEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Uploads a processed artifact (GLB, thumbnail, preview) to GCS.
    /// This endpoint is designed for internal service-to-service communication.
    /// </summary>
    [HttpPost("artifacts")]
    [Consumes("application/json")]
    [RequirePermission(UploadPermissions.FilesUpload, ResourcePathTemplate = "folders/{request.StoragePath}")]
    [ProducesResponseType(typeof(ArtifactUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadArtifact(
        [FromBody] UploadArtifactRequest request,
        CancellationToken cancellationToken)
    {
        var serviceName = User.Identity?.Name ?? "GeometryService";

        try
        {
            // Decode Base64 artifact data
            byte[] artifactBytes;
            try
            {
                artifactBytes = Convert.FromBase64String(request.ArtifactData);
            }
            catch (FormatException)
            {
                return BadRequest(new { error = "Invalid Base64-encoded artifact data" });
            }

            // Sanitize path
            string sanitizedPath;
            try
            {
                sanitizedPath = request.StoragePath.SanitizePath();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Path traversal attempt detected by service {ServiceName}: {OriginalPath}",
                    serviceName, request.StoragePath);
                return BadRequest(new { error = $"Invalid path: {ex.Message}" });
            }

            // Upload to GCS using the storage service
            using var stream = new MemoryStream(artifactBytes);
            await _storageService.UploadFileAsync(
                stream,
                sanitizedPath,
                request.ContentType,
                overwrite: true,
                cancellationToken);

            // Generate signed download URL
            var downloadUrl = await _storageService.GenerateSignedUrlAsync(
                sanitizedPath,
                TimeSpan.FromHours(1),
                cancellationToken);

            // Log the event
            await LogUploadEventAsync(
                request.ArtifactId,
                serviceName,
                sanitizedPath,
                "ArtifactUploaded",
                cancellationToken);

            _logger.LogInformation("Artifact uploaded successfully. ArtifactId: {ArtifactId}, Path: {Path}",
                request.ArtifactId, sanitizedPath);

            return Ok(new ArtifactUploadResponse
            {
                ArtifactId = request.ArtifactId,
                StoragePath = sanitizedPath,
                DownloadUrl = downloadUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload artifact. ArtifactId: {ArtifactId}", request.ArtifactId);
            return StatusCode(500, new { error = "Failed to upload artifact" });
        }
    }
}
