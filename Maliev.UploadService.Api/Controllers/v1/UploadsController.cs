using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Contracts.Uploads;
using Maliev.UploadService.Api.Extensions;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly IAuthorizationPolicyService _authorizationService;
    private readonly UploadDbContext _dbContext;
    private readonly ILogger<UploadsController> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the UploadsController class.
    /// </summary>
    public UploadsController(
        IValidationService validationService,
        IStorageService storageService,
        ILifecycleManagementService lifecycleService,
        IAuthorizationPolicyService authorizationService,
        UploadDbContext dbContext,
        ILogger<UploadsController> logger,
        IPublishEndpoint publishEndpoint)
    {
        _validationService = validationService;
        _storageService = storageService;
        _lifecycleService = lifecycleService;
        _authorizationService = authorizationService;
        _dbContext = dbContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Initiates a direct-to-GCS resumable upload session.
    /// </summary>
    [HttpPost("resumable")]
    [RequirePermission(UploadPermissions.FilesUpload, ResourcePathTemplate = "folders/{request.Path}")]
    [ProducesResponseType(typeof(InitiateResumableUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateResumableUpload(
        [FromBody] InitiateResumableUploadRequest request,
        CancellationToken cancellationToken)
    {
        var uploadId = Guid.NewGuid().ToString();
        var callerServiceId = GetCallerServiceId();
        var serviceName = request.ServiceName.Trim();

        try
        {
            var sanitizedPath = ResolveUploadPath(request.Path, serviceName, uploadId);

            if (!await _authorizationService.CanUploadToPathAsync(serviceName, sanitizedPath, cancellationToken))
            {
                _logger.LogWarning(
                    "Unauthorized resumable upload initiation by caller {CallerServiceId} as service {ServiceName} for path {StoragePath}",
                    callerServiceId,
                    serviceName,
                    sanitizedPath);
                await LogUploadEventAsync(uploadId, callerServiceId, sanitizedPath, "Unauthorized", cancellationToken);
                return Forbid();
            }

            var validationResult = await _validationService.ValidateFileAsync(
                Stream.Null,
                request.FileName,
                request.ContentType,
                request.TotalSize,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Upload validation failed for {FileName}: {Errors}",
                    request.FileName,
                    string.Join(", ", validationResult.Errors));
                await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "ValidationFailed", cancellationToken);
                return BadRequest(new { errors = validationResult.Errors });
            }

            var existingUpload = await _dbContext.Uploads
                .FirstOrDefaultAsync(u => u.StoragePath == sanitizedPath, cancellationToken);

            if (existingUpload != null)
            {
                if (!request.Overwrite)
                {
                    await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "PathCollision", cancellationToken);
                    return Conflict(new { error = $"File already exists at path '{sanitizedPath}'. Set overwrite=true to replace it." });
                }

                await RemoveExistingUploadAsync(existingUpload, cancellationToken);
            }

            var session = await _storageService.InitiateResumableUploadAsync(
                sanitizedPath,
                request.ContentType,
                request.TotalSize,
                cancellationToken);

            var upload = new Upload
            {
                UploadId = uploadId,
                ServiceId = serviceName,
                FileName = request.FileName,
                StoragePath = sanitizedPath,
                ContentType = request.ContentType,
                FileSize = request.TotalSize,
                Checksum = request.Checksum,
                BytesUploaded = 0,
                Status = UploadStatus.InProgress,
                UploadedAt = DateTime.UtcNow,
                SessionUri = session.SessionUri,
                RetentionPolicyId = request.RetentionPolicyId,
                Metadata = request.Metadata != null
                    ? new Dictionary<string, string> { { "custom", request.Metadata } }
                    : null
            };

            _dbContext.Uploads.Add(upload);
            await LogUploadEventAsync(uploadId, serviceName, sanitizedPath, "Initiated", cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Direct GCS upload session initiated. UploadId: {UploadId}, Service: {ServiceName}, TotalSize: {TotalSize}",
                uploadId,
                serviceName,
                request.TotalSize);

            return Ok(new InitiateResumableUploadResponse
            {
                UploadId = uploadId,
                SessionUri = session.SessionUri,
                ExpiresAt = session.ExpiresAt,
                TotalSize = request.TotalSize
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid upload path for service {ServiceName}: {Path}", serviceName, request.Path);
            await LogUploadEventAsync(uploadId, serviceName, request.Path, "PathTraversalAttempt", cancellationToken);
            return BadRequest(new { error = $"Invalid path: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate direct GCS upload. UploadId: {UploadId}", uploadId);
            return StatusCode(500, new { error = "Failed to initiate upload" });
        }
    }

    /// <summary>
    /// Completes a direct-to-GCS resumable upload after the client has uploaded to GCS.
    /// </summary>
    [HttpPost("resumable/{uploadId}/complete")]
    [RequirePermission(UploadPermissions.FilesUpload)]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteResumableUpload(
        [FromRoute] string uploadId,
        [FromBody] CompleteResumableUploadRequest? request,
        CancellationToken cancellationToken)
    {
        var upload = await _dbContext.Uploads
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, cancellationToken);

        if (upload == null)
        {
            return NotFound(new { error = "Upload session not found" });
        }

        if (!await CanAccessUploadAsync(upload, cancellationToken))
        {
            await LogUploadEventAsync(uploadId, User.Identity?.Name ?? "unknown", upload.StoragePath, "Unauthorized", cancellationToken);
            return Forbid();
        }

        if (upload.Status == UploadStatus.Completed)
        {
            var existingUrl = await _storageService.GenerateSignedUrlAsync(
                upload.StoragePath,
                TimeSpan.FromHours(1),
                cancellationToken);
            return Ok(upload.ToResponse(existingUrl));
        }

        var gcsMetadata = await _storageService.GetFileMetadataAsync(upload.StoragePath, cancellationToken);
        if (gcsMetadata == null)
        {
            await LogUploadEventAsync(uploadId, upload.ServiceId, upload.StoragePath, "Failed", cancellationToken);
            return BadRequest(new { error = "GCS object was not found. Upload the file to the session URI before completing the upload." });
        }

        if (gcsMetadata.SizeBytes != upload.FileSize)
        {
            await LogUploadEventAsync(uploadId, upload.ServiceId, upload.StoragePath, "Failed", cancellationToken);
            return BadRequest(new
            {
                error = "GCS object size does not match the initiated upload size.",
                expectedSize = upload.FileSize,
                actualSize = gcsMetadata.SizeBytes
            });
        }

        var checksum = request?.Checksum ?? upload.Checksum ?? ConvertGcsMd5ToHex(gcsMetadata.Md5Hash) ?? "UNKNOWN";

        upload.Status = UploadStatus.Completed;
        upload.BytesUploaded = gcsMetadata.SizeBytes;
        upload.CompletedAt = DateTime.UtcNow;
        upload.Checksum = checksum;

        var fileMetadata = await _dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == uploadId, cancellationToken);

        if (fileMetadata == null)
        {
            fileMetadata = new FileMetadata
            {
                FileId = Guid.NewGuid().ToString(),
                UploadId = uploadId,
                ServiceId = upload.ServiceId,
                StoragePath = upload.StoragePath,
                VersionETag = gcsMetadata.ETag,
                FileSize = gcsMetadata.SizeBytes,
                ContentType = gcsMetadata.ContentType,
                Checksum = checksum,
                UploadedAt = gcsMetadata.CreatedAt,
                RetentionPolicyId = upload.RetentionPolicyId,
                Metadata = upload.Metadata
            };

            if (string.IsNullOrEmpty(fileMetadata.RetentionPolicyId))
            {
                var applicablePolicy = await _lifecycleService.GetActiveRetentionPolicyAsync(
                    upload.ServiceId,
                    upload.StoragePath,
                    cancellationToken);
                fileMetadata.RetentionPolicyId = applicablePolicy?.PolicyId;
            }

            if (!string.IsNullOrEmpty(fileMetadata.RetentionPolicyId))
            {
                fileMetadata.ExpiresAt = await _lifecycleService.ApplyRetentionPolicyAsync(
                    fileMetadata,
                    fileMetadata.RetentionPolicyId,
                    cancellationToken);
            }

            _dbContext.FileMetadata.Add(fileMetadata);
        }

        await LogUploadEventAsync(uploadId, upload.ServiceId, upload.StoragePath, "Success", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var downloadUrl = await _storageService.GenerateSignedUrlAsync(
            upload.StoragePath,
            TimeSpan.FromHours(1),
            cancellationToken);

        await PublishFileUploadedEventAsync(upload, fileMetadata, downloadUrl, cancellationToken);

        _logger.LogInformation(
            "Direct GCS upload completed. UploadId: {UploadId}, Service: {ServiceName}, Size: {SizeBytes}",
            upload.UploadId,
            upload.ServiceId,
            upload.FileSize);

        return Ok(upload.ToResponse(downloadUrl));
    }

    /// <summary>
    /// Proxies a resumable upload chunk to GCS for clients that cannot reach GCS directly.
    /// Prefer direct PUT to the session URI returned by <see cref="InitiateResumableUpload"/>.
    /// </summary>
    [HttpPut("resumable/{uploadId}")]
    [RequirePermission(UploadPermissions.FilesUpload)]
    [ProducesResponseType(typeof(ResumeUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeUpload(
        [FromRoute] string uploadId,
        CancellationToken cancellationToken)
    {
        try
        {
            var upload = await _dbContext.Uploads.FindAsync([uploadId], cancellationToken);
            if (upload == null)
            {
                return NotFound(new { error = "Upload session not found" });
            }

            if (!await CanAccessUploadAsync(upload, cancellationToken))
            {
                await LogUploadEventAsync(uploadId, User.Identity?.Name ?? "unknown", upload.StoragePath, "Unauthorized", cancellationToken);
                return Forbid();
            }

            if (string.IsNullOrEmpty(upload.SessionUri))
            {
                return BadRequest(new { error = "Upload session is not resumable" });
            }

            var contentRange = Request.Headers.ContentRange.ToString();
            if (string.IsNullOrEmpty(contentRange))
            {
                return BadRequest(new { error = "Content-Range header is required" });
            }

            var rangeParts = contentRange.Replace("bytes ", "", StringComparison.OrdinalIgnoreCase).Split('/');
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

            if (totalSize != upload.FileSize)
            {
                return BadRequest(new
                {
                    error = $"Content-Range total size ({totalSize}) does not match initiated upload size ({upload.FileSize})"
                });
            }

            if (startByte != upload.BytesUploaded)
            {
                return BadRequest(new
                {
                    error = $"Unexpected upload range start. Expected {upload.BytesUploaded}, received {startByte}"
                });
            }

            if (endByte < startByte || endByte >= totalSize)
            {
                return BadRequest(new { error = "Content-Range byte range is outside the initiated upload size" });
            }

            var expectedContentLength = endByte - startByte + 1;
            if (Request.ContentLength.HasValue && Request.ContentLength.Value != expectedContentLength)
            {
                return BadRequest(new
                {
                    error = $"Content-Length ({Request.ContentLength.Value}) does not match Content-Range length ({expectedContentLength})"
                });
            }

            var progress = await _storageService.ResumeUploadAsync(
                upload.SessionUri,
                Request.Body,
                startByte,
                endByte,
                totalSize,
                cancellationToken);

            upload.BytesUploaded = progress.BytesReceived;

            if (!progress.IsComplete)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
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

            return await CompleteResumableUpload(uploadId, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume upload. UploadId: {UploadId}", uploadId);
            return StatusCode(500, new { error = "Failed to resume upload" });
        }
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
        var serviceName = GetCallerServiceId();

        try
        {
            byte[] artifactBytes;
            try
            {
                artifactBytes = Convert.FromBase64String(request.ArtifactData);
            }
            catch (FormatException)
            {
                return BadRequest(new { error = "Invalid Base64-encoded artifact data" });
            }

            string sanitizedPath;
            try
            {
                sanitizedPath = request.StoragePath.SanitizePath();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    "Path traversal attempt detected by service {ServiceName}: {OriginalPath}",
                    serviceName,
                    request.StoragePath);
                return BadRequest(new { error = $"Invalid path: {ex.Message}" });
            }

            if (!await _authorizationService.CanUploadToPathAsync(serviceName, sanitizedPath, cancellationToken))
            {
                _logger.LogWarning(
                    "Unauthorized artifact upload by service {ServiceName} for path {StoragePath}",
                    serviceName,
                    sanitizedPath);
                await LogUploadEventAsync(request.ArtifactId.ToString(), serviceName, sanitizedPath, "Unauthorized", cancellationToken);
                return Forbid();
            }

            using var stream = new MemoryStream(artifactBytes);
            await _storageService.UploadFileAsync(
                stream,
                sanitizedPath,
                request.ContentType,
                overwrite: true,
                cancellationToken);

            var downloadUrl = await _storageService.GenerateSignedUrlAsync(
                sanitizedPath,
                TimeSpan.FromHours(1),
                cancellationToken);

            await LogUploadEventAsync(request.ArtifactId.ToString(), serviceName, sanitizedPath, "ArtifactUploaded", cancellationToken);

            _logger.LogInformation(
                "Artifact uploaded successfully. ArtifactId: {ArtifactId}, Path: {Path}",
                request.ArtifactId,
                sanitizedPath);

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

    private static string ResolveUploadPath(string path, string serviceName, string uploadId)
    {
        var now = DateTime.UtcNow;
        var placeholders = new Dictionary<string, string>
        {
            { "id", uploadId.Replace("-", "", StringComparison.Ordinal) },
            { "timestamp", now.ToString("yyyyMMddHHmmss") },
            { "date", now.ToString("yyyyMMdd") },
            { "year", now.Year.ToString() },
            { "month", now.Month.ToString("D2") },
            { "day", now.Day.ToString("D2") },
            { "service", serviceName }
        };

        return path.ResolvePlaceholders(placeholders).SanitizePath();
    }

    private async Task RemoveExistingUploadAsync(Upload existingUpload, CancellationToken cancellationToken)
    {
        var existingFileMetadata = await _dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == existingUpload.UploadId, cancellationToken);

        if (existingFileMetadata != null)
        {
            _dbContext.FileMetadata.Remove(existingFileMetadata);
        }

        _dbContext.Uploads.Remove(existingUpload);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishFileUploadedEventAsync(
        Upload upload,
        FileMetadata fileMetadata,
        string downloadUrl,
        CancellationToken cancellationToken)
    {
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
                UploadId: upload.UploadId,
                ServiceId: upload.ServiceId,
                FileName: upload.FileName,
                StoragePath: upload.StoragePath,
                ContentType: upload.ContentType,
                FileSize: upload.FileSize,
                DownloadUrl: downloadUrl,
                UploadedAt: new DateTimeOffset(upload.UploadedAt, TimeSpan.Zero),
                RetentionPolicyId: fileMetadata.RetentionPolicyId,
                ExpiresAt: fileMetadata.ExpiresAt.HasValue ? new DateTimeOffset(fileMetadata.ExpiresAt.Value, TimeSpan.Zero) : null,
                Metadata: fileMetadata.Metadata!
            )
        ), cancellationToken);
    }

    private async Task<bool> CanAccessUploadAsync(Upload upload, CancellationToken cancellationToken)
    {
        var serviceId = GetCallerServiceId();
        return await _authorizationService.CanAccessPathAsync(
            serviceId,
            upload.StoragePath,
            cancellationToken);
    }

    private string GetCallerServiceId()
    {
        return User.FindFirst("service_name")?.Value
            ?? User.Identity?.Name
            ?? "unknown";
    }

    private async Task LogUploadEventAsync(
        string uploadId,
        string serviceName,
        string path,
        string eventTypeString,
        CancellationToken cancellationToken)
    {
        serviceName = serviceName?.Replace("\0", "[NULL]", StringComparison.Ordinal) ?? "unknown";
        path = path?.Replace("\0", "[NULL]", StringComparison.Ordinal) ?? "";

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
            UploadId = uploadId,
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

    private static string? ConvertGcsMd5ToHex(string? md5Hash)
    {
        if (string.IsNullOrEmpty(md5Hash))
        {
            return null;
        }

        try
        {
            return BitConverter.ToString(Convert.FromBase64String(md5Hash))
                .Replace("-", "", StringComparison.Ordinal)
                .ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
