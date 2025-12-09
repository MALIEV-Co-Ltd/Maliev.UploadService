// T180-T185: UploadMetrics class with OpenTelemetry custom metrics
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Maliev.UploadService.Api.Metrics;

/// <summary>
/// Provides OpenTelemetry metrics instrumentation for Upload Service operations.
/// Implements Constitution Principle XII: Business Metrics & Analytics.
/// </summary>
public class UploadMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _uploadSuccessCounter;
    private readonly Counter<long> _uploadFailureCounter;
    private readonly Histogram<double> _uploadDurationHistogram;
    private readonly Counter<long> _validationRejectionCounter;
    private readonly ObservableGauge<long> _activeUploadsGauge;
    private readonly ObservableGauge<double> _storageQuotaUtilizationGauge;
    private readonly Counter<long> _signedUrlGenerationCounter;
    private readonly Counter<long> _fileDeletionCounter;
    private readonly Counter<long> _bulkDeleteJobCounter;

    private long _activeUploadCount;
    private readonly object _activeUploadLock = new();

    public UploadMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Maliev.UploadService");

        // T181: Upload success/failure rate counters
        _uploadSuccessCounter = _meter.CreateCounter<long>(
            name: "upload.success",
            unit: "uploads",
            description: "Number of successful file uploads"
        );

        _uploadFailureCounter = _meter.CreateCounter<long>(
            name: "upload.failure",
            unit: "uploads",
            description: "Number of failed file uploads"
        );

        // T182: Upload duration histogram
        _uploadDurationHistogram = _meter.CreateHistogram<double>(
            name: "upload.duration",
            unit: "ms",
            description: "Upload duration distribution by file size bucket"
        );

        // T183: Active uploads gauge
        _activeUploadsGauge = _meter.CreateObservableGauge<long>(
            name: "upload.active",
            observeValue: () => GetActiveUploadCount(),
            unit: "uploads",
            description: "Current number of active uploads"
        );

        // T184: Storage quota utilization gauge
        _storageQuotaUtilizationGauge = _meter.CreateObservableGauge<double>(
            name: "storage.quota.utilization",
            observeValue: () => 0.0, // Placeholder - will be populated by service layer
            unit: "percent",
            description: "Storage quota utilization by service"
        );

        // T185: File validation rejection metrics
        _validationRejectionCounter = _meter.CreateCounter<long>(
            name: "upload.validation.rejection",
            unit: "rejections",
            description: "Number of file validation rejections by reason"
        );

        // Additional metrics for comprehensive observability
        _signedUrlGenerationCounter = _meter.CreateCounter<long>(
            name: "signed_url.generation",
            unit: "requests",
            description: "Number of signed URL generation requests"
        );

        _fileDeletionCounter = _meter.CreateCounter<long>(
            name: "file.deletion",
            unit: "deletions",
            description: "Number of file deletion operations"
        );

        _bulkDeleteJobCounter = _meter.CreateCounter<long>(
            name: "bulk_delete.job",
            unit: "jobs",
            description: "Number of bulk delete jobs initiated"
        );
    }

    /// <summary>
    /// Records a successful file upload with associated metadata.
    /// </summary>
    public void RecordUploadSuccess(string serviceId, string contentType, long fileSizeBytes, double durationMs)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "file.content_type", contentType },
            { "file.size_bucket", GetSizeBucket(fileSizeBytes) },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        _uploadSuccessCounter.Add(1, tags);
        _uploadDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a failed file upload with error reason.
    /// </summary>
    public void RecordUploadFailure(string serviceId, string contentType, string errorReason)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "file.content_type", contentType },
            { "error.reason", errorReason },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        _uploadFailureCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a file validation rejection with reason.
    /// </summary>
    public void RecordValidationRejection(string serviceId, string contentType, string rejectionReason)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "file.content_type", contentType },
            { "rejection.reason", rejectionReason },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        _validationRejectionCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a signed URL generation request.
    /// </summary>
    public void RecordSignedUrlGeneration(string serviceId, long expirationSeconds)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "expiration.seconds", expirationSeconds.ToString() },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        _signedUrlGenerationCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a file deletion operation.
    /// </summary>
    public void RecordFileDeletion(string serviceId, bool success, string? errorReason = null)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "result", success ? "success" : "failure" },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        if (!success && !string.IsNullOrEmpty(errorReason))
        {
            tags.Add("error.reason", errorReason);
        }

        _fileDeletionCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a bulk delete job initiation.
    /// </summary>
    public void RecordBulkDeleteJob(string serviceId, int totalFiles)
    {
        var tags = new TagList
        {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "total_files", totalFiles.ToString() },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" }
        };

        _bulkDeleteJobCounter.Add(1, tags);
    }

    /// <summary>
    /// Increments the active upload count when an upload starts.
    /// </summary>
    public void IncrementActiveUploads()
    {
        lock (_activeUploadLock)
        {
            _activeUploadCount++;
        }
    }

    /// <summary>
    /// Decrements the active upload count when an upload completes or fails.
    /// </summary>
    public void DecrementActiveUploads()
    {
        lock (_activeUploadLock)
        {
            if (_activeUploadCount > 0)
            {
                _activeUploadCount--;
            }
        }
    }

    /// <summary>
    /// Gets the current active upload count for the gauge.
    /// </summary>
    private long GetActiveUploadCount()
    {
        lock (_activeUploadLock)
        {
            return _activeUploadCount;
        }
    }

    /// <summary>
    /// Categorizes file size into buckets for metrics aggregation.
    /// </summary>
    private static string GetSizeBucket(long bytes) => bytes switch
    {
        < 1_048_576 => "<1MB",
        < 10_485_760 => "1-10MB",
        < 104_857_600 => "10-100MB",
        < 1_073_741_824 => "100MB-1GB",
        _ => ">1GB"
    };
}
