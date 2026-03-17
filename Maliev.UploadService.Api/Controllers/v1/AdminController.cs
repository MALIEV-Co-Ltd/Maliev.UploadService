using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.UploadService.Api.Consumers;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IBulkDeleteService _bulkDeleteService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminController class.
    /// </summary>
    /// <param name="bulkDeleteService">The bulk delete service.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    /// <param name="logger">The logger for this controller.</param>
    public AdminController(
        IBulkDeleteService bulkDeleteService,
        IPublishEndpoint publishEndpoint,
        ILogger<AdminController> logger)
    {
        _bulkDeleteService = bulkDeleteService;
        _publishEndpoint = publishEndpoint;
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
}
