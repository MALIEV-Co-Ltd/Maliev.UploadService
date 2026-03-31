using System.Text.RegularExpressions;
using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.UploadService.Api.Consumers;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Application.Interfaces;
using Maliev.UploadService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.UploadService.Api.Controllers.v1;

/// <summary>
/// Admin controller for bulk operations (FR-032, FR-033)
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("upload/v{version:apiVersion}/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly Application.Interfaces.IBulkDeleteService _bulkDeleteService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly Application.Interfaces.IStorageService _storageService;
    private readonly UploadDbContext _dbContext;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminController class.
    /// </summary>
    /// <param name="bulkDeleteService">The bulk delete service.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    /// <param name="storageService">The GCS storage service.</param>
    /// <param name="dbContext">The upload database context.</param>
    /// <param name="logger">The logger for this controller.</param>
    public AdminController(
        Application.Interfaces.IBulkDeleteService bulkDeleteService,
        IPublishEndpoint publishEndpoint,
        Application.Interfaces.IStorageService storageService,
        UploadDbContext dbContext,
        ILogger<AdminController> logger)
    {
        _bulkDeleteService = bulkDeleteService;
        _publishEndpoint = publishEndpoint;
        _storageService = storageService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/v1/admin/metrics - Returns service telemetry metrics
    /// </summary>
    [HttpGet("metrics")]
    [RequirePermission(UploadPermissions.AdminViewMetrics, RequireLiveCheck = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetMetrics()
    {
        // Placeholder for metrics summary
        return Ok(new { status = "Healthy", activeUploads = 0, storageUsedBytes = 0 });
    }

    /// <summary>
    /// POST /api/v1/admin/bulk-delete - Initiates a bulk delete job (FR-032)
    /// </summary>
    [HttpPost("bulk-delete")]
    [RequirePermission(UploadPermissions.AdminBulkDelete, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(BulkDeleteJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InitiateBulkDelete(
        [FromBody] BulkDeleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Initiating bulk delete. ServiceId: {ServiceId}, PathPrefix: {PathPrefix}",
                request.ServiceId, request.PathPrefix);

            var jobId = await _bulkDeleteService.InitiateBulkDeleteAsync(
                request.ServiceId,
                request.PathPrefix,
                request.UploadIds,
                request.DeleteFilesOlderThan,
                request.Reason,
                cancellationToken);

            // Publish message to queue for background processing
            await _publishEndpoint.Publish(new BulkDeleteJobMessage
            {
                JobId = jobId
            }, cancellationToken);

            return Accepted(new BulkDeleteJobResponse
            {
                JobId = jobId,
                Status = "Pending",
                TotalFiles = 0,
                FilesDeleted = 0,
                FilesFailed = 0,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate bulk delete");
            return StatusCode(500, new { error = "Failed to initiate bulk delete" });
        }
    }

    /// <summary>
    /// GET /api/v1/admin/bulk-delete/{jobId} - Gets bulk delete job status (FR-033)
    /// </summary>
    [HttpGet("bulk-delete/{jobId}")]
    [RequirePermission(UploadPermissions.AdminBulkDelete, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(BulkDeleteJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBulkDeleteStatus(
        string jobId,
        CancellationToken cancellationToken)
    {
        var job = await _bulkDeleteService.GetBulkDeleteJobStatusAsync(jobId, cancellationToken);

        if (job == null)
        {
            return NotFound(new { error = "Bulk delete job not found" });
        }

        return Ok(new BulkDeleteJobResponse
        {
            JobId = job.JobId,
            Status = job.Status.ToString(),
            TotalFiles = job.TotalFiles,
            FilesDeleted = job.FilesDeleted,
            FilesFailed = job.FilesFailed,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            Errors = job.Errors
        });
    }

    /// <summary>
    /// POST /api/v1/admin/migrate-project-files - Migrates project files from maliev-temp
    /// to maliev-customers bucket by rewriting paths from <c>projects/{projectId}/...</c>
    /// to <c>customers/{customerId}/projects/{projectId}/...</c>.
    /// </summary>
    /// <param name="request">Mapping of projectId → customerId for files to migrate.</param>
    /// <param name="dryRun">If true, only reports what would be migrated without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("migrate-project-files")]
    [RequirePermission(UploadPermissions.AdminAll, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(MigrateProjectFilesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MigrateProjectFiles(
        [FromBody] MigrateProjectFilesRequest request,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectCustomerMap == null || request.ProjectCustomerMap.Count == 0)
            return BadRequest(new { error = "projectCustomerMap is required and must not be empty." });

        var migrated = new List<MigratedFileEntry>();
        var errors = new List<string>();

        // Find all FileMetadata records with paths starting with "projects/"
        var filesToMigrate = await _dbContext.FileMetadata
            .Where(f => f.StoragePath.StartsWith("projects/"))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} files with 'projects/' prefix to evaluate for migration", filesToMigrate.Count);

        // Regex to extract projectId from path: projects/{projectId}/...
        var projectIdPattern = new Regex(@"^projects/([0-9a-fA-F\-]{36})/", RegexOptions.Compiled);

        foreach (var file in filesToMigrate)
        {
            var match = projectIdPattern.Match(file.StoragePath);
            if (!match.Success) continue;

            var projectId = match.Groups[1].Value;
            if (!request.ProjectCustomerMap.TryGetValue(projectId, out var customerId))
            {
                _logger.LogDebug("Skipping file {Path} — projectId {ProjectId} not in migration map", file.StoragePath, projectId);
                continue;
            }

            // Rewrite path: projects/{projectId}/... → customers/{customerId}/projects/{projectId}/...
            var newPath = $"customers/{customerId}/{file.StoragePath}";

            if (dryRun)
            {
                migrated.Add(new MigratedFileEntry
                {
                    FileId = file.FileId,
                    OldPath = file.StoragePath,
                    NewPath = newPath
                });
                continue;
            }

            try
            {
                await _storageService.CopyFileAsync(file.StoragePath, newPath, cancellationToken);
                var oldPath = file.StoragePath;
                file.StoragePath = newPath;
                await _storageService.DeleteFileAsync(oldPath, cancellationToken);

                migrated.Add(new MigratedFileEntry
                {
                    FileId = file.FileId,
                    OldPath = oldPath,
                    NewPath = newPath
                });

                _logger.LogInformation("Migrated file {FileId}: {OldPath} → {NewPath}", file.FileId, oldPath, newPath);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to migrate {file.FileId} ({file.StoragePath}): {ex.Message}";
                errors.Add(msg);
                _logger.LogError(ex, "Migration failed for file {FileId}", file.FileId);
            }
        }

        if (!dryRun && migrated.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Persisted {Count} path updates to database", migrated.Count);
        }

        return Ok(new MigrateProjectFilesResponse
        {
            DryRun = dryRun,
            TotalEvaluated = filesToMigrate.Count,
            TotalMigrated = migrated.Count,
            MigratedFiles = migrated,
            Errors = errors
        });
    }

    /// <summary>
    /// POST /api/v1/admin/migrate-project/{projectId}?customerId={guid} -
    /// Migrates all files for a single project from the temp bucket to the customer bucket.
    /// Files are copied from <c>projects/{projectId}/...</c> to
    /// <c>customers/{customerId}/projects/{projectId}/...</c>.
    /// </summary>
    /// <param name="projectId">The project GUID whose files should be migrated.</param>
    /// <param name="customerId">The target customer GUID.</param>
    /// <param name="dryRun">If true, only reports what would be migrated without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("migrate-project/{projectId:guid}")]
    [RequirePermission(UploadPermissions.StorageManage, RequireLiveCheck = true)]
    [ProducesResponseType(typeof(MigrateProjectFilesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MigrateProject(
        [FromRoute] Guid projectId,
        [FromQuery] Guid customerId,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { error = "projectId is required." });

        if (customerId == Guid.Empty)
            return BadRequest(new { error = "customerId is required." });

        var projectIdPrefix = projectId.ToString();

        var filesToMigrate = await _dbContext.FileMetadata
            .Where(f => f.StoragePath.StartsWith($"projects/{projectIdPrefix}/"))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} files to migrate for project {ProjectId}", filesToMigrate.Count, projectId);

        var migrated = new List<MigratedFileEntry>();
        var errors = new List<string>();

        foreach (var file in filesToMigrate)
        {
            var newPath = $"customers/{customerId}/{file.StoragePath}";

            if (dryRun)
            {
                migrated.Add(new MigratedFileEntry
                {
                    FileId = file.FileId,
                    OldPath = file.StoragePath,
                    NewPath = newPath
                });
                continue;
            }

            try
            {
                await _storageService.CopyFileAsync(file.StoragePath, newPath, cancellationToken);
                var oldPath = file.StoragePath;
                file.StoragePath = newPath;
                await _storageService.DeleteFileAsync(oldPath, cancellationToken);

                migrated.Add(new MigratedFileEntry
                {
                    FileId = file.FileId,
                    OldPath = oldPath,
                    NewPath = newPath
                });

                _logger.LogInformation("Migrated file {FileId}: {OldPath} → {NewPath}", file.FileId, oldPath, newPath);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to migrate {file.FileId} ({file.StoragePath}): {ex.Message}";
                errors.Add(msg);
                _logger.LogError(ex, "Migration failed for file {FileId}", file.FileId);
            }
        }

        if (!dryRun && migrated.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Persisted {Count} path updates to database for project {ProjectId}", migrated.Count, projectId);
        }

        return Ok(new MigrateProjectFilesResponse
        {
            DryRun = dryRun,
            TotalEvaluated = filesToMigrate.Count,
            TotalMigrated = migrated.Count,
            MigratedFiles = migrated,
            Errors = errors
        });
    }
}

/// <summary>
/// Request body for the project file migration endpoint.
/// </summary>
public class MigrateProjectFilesRequest
{
    /// <summary>
    /// Maps projectId (string GUID) → customerId (string GUID) for files to migrate.
    /// </summary>
    public Dictionary<string, string> ProjectCustomerMap { get; set; } = new();
}

/// <summary>
/// Response from the project file migration endpoint.
/// </summary>
public class MigrateProjectFilesResponse
{
    /// <summary>Gets or sets whether this was a dry run with no actual changes.</summary>
    public bool DryRun { get; set; }

    /// <summary>Gets or sets the total number of files evaluated for migration.</summary>
    public int TotalEvaluated { get; set; }

    /// <summary>Gets or sets the total number of files successfully migrated.</summary>
    public int TotalMigrated { get; set; }

    /// <summary>Gets or sets the list of individual file migration results.</summary>
    public List<MigratedFileEntry> MigratedFiles { get; set; } = new();

    /// <summary>Gets or sets any errors encountered during migration.</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Represents a single migrated file with old and new paths.
/// </summary>
public class MigratedFileEntry
{
    /// <summary>Gets or sets the unique identifier of the migrated file.</summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>Gets or sets the original storage path before migration.</summary>
    public string OldPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the new storage path after migration.</summary>
    public string NewPath { get; set; } = string.Empty;
}
