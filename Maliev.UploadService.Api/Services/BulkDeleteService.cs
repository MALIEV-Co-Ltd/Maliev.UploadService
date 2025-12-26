using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Implementation of bulk delete service (FR-032, FR-033)
/// </summary>
public class BulkDeleteService : IBulkDeleteService
{
    private readonly UploadDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly ILogger<BulkDeleteService> _logger;

    public BulkDeleteService(
        UploadDbContext dbContext,
        IStorageService storageService,
        ILogger<BulkDeleteService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<string> InitiateBulkDeleteAsync(
        string serviceId,
        string? pathPrefix,
        List<string>? uploadIds,
        DateTime? deleteFilesOlderThan,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();

        var job = new BulkDeleteJob
        {
            JobId = jobId,
            ServiceId = serviceId,
            PathPrefix = pathPrefix ?? string.Empty,
            InitiatedBy = "System", // Will be updated to use actual user context
            UploadIds = uploadIds,
            DeleteFilesOlderThan = deleteFilesOlderThan,
            Reason = reason,
            Status = BulkDeleteStatus.Queued,
            FilesTotal = 0,
            FilesDeleted = 0,
            ErrorCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.BulkDeleteJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk delete job initiated. JobId: {JobId}, ServiceId: {ServiceId}", jobId, serviceId);

        return jobId;
    }

    public async Task<BulkDeleteJob?> GetBulkDeleteJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BulkDeleteJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
    }

    public async Task ProcessBulkDeleteJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BulkDeleteJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Bulk delete job not found. JobId: {JobId}", jobId);
            return;
        }

        job.Status = BulkDeleteStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Find files to delete
            var query = _dbContext.FileMetadata.AsQueryable();

            query = query.Where(f => f.ServiceId == job.ServiceId);

            if (!string.IsNullOrEmpty(job.PathPrefix))
            {
                query = query.Where(f => f.StoragePath.StartsWith(job.PathPrefix));
            }

            if (job.UploadIds != null && job.UploadIds.Count > 0)
            {
                query = query.Where(f => job.UploadIds.Contains(f.UploadId));
            }

            if (job.DeleteFilesOlderThan.HasValue)
            {
                query = query.Where(f => f.UploadedAt < job.DeleteFilesOlderThan.Value);
            }

            var filesToDelete = await query.ToListAsync(cancellationToken);
            job.FilesTotal = filesToDelete.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var errors = new List<string>();

            // Delete files in batches
            foreach (var fileMetadata in filesToDelete)
            {
                try
                {
                    // Delete from GCS
                    await _storageService.DeleteFileAsync(fileMetadata.StoragePath, cancellationToken);

                    // Delete from database
                    var upload = await _dbContext.Uploads
                        .FirstOrDefaultAsync(u => u.UploadId == fileMetadata.UploadId, cancellationToken);

                    if (upload != null)
                    {
                        _dbContext.Uploads.Remove(upload);
                    }

                    _dbContext.FileMetadata.Remove(fileMetadata);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    job.FilesDeleted++;

                    _logger.LogInformation(
                        "File deleted in bulk job. JobId: {JobId}, UploadId: {UploadId}, Path: {StoragePath}",
                        jobId, fileMetadata.UploadId, fileMetadata.StoragePath);
                }
                catch (Exception ex)
                {
                    job.ErrorCount++;
                    var errorMsg = $"Failed to delete {fileMetadata.StoragePath}: {ex.Message}";
                    errors.Add(errorMsg);
                    _logger.LogError(ex, "Failed to delete file in bulk job. JobId: {JobId}, Path: {StoragePath}",
                        jobId, fileMetadata.StoragePath);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Determine final status based on results
            if (errors.Count > 0)
            {
                job.Status = errors.Count == filesToDelete.Count
                    ? BulkDeleteStatus.Failed
                    : BulkDeleteStatus.CompletedWithErrors;
            }
            else
            {
                job.Status = BulkDeleteStatus.Completed;
            }

            job.CompletedAt = DateTime.UtcNow;
            job.ErrorDetails = errors.Count > 0 ? errors : null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Bulk delete job completed. JobId: {JobId}, Total: {Total}, Deleted: {Deleted}, Failed: {Failed}",
                jobId, job.FilesTotal, job.FilesDeleted, job.ErrorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk delete job failed. JobId: {JobId}", jobId);
            job.Status = BulkDeleteStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorDetails = new List<string> { ex.Message };
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

