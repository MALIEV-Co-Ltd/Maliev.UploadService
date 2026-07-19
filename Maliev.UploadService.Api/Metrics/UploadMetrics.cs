using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Maliev.UploadService.Api.Metrics;

/// <summary>
/// Provides OpenTelemetry metrics instrumentation for Upload Service operations.
/// Implements Constitution Principle XII: Business Metrics &amp; Analytics.
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
    private readonly Counter<long> _authSuccessCounter;
    private readonly Counter<long> _authFailureCounter;
    private readonly KeyValuePair<string, object?>[] _defaultTags;

    private long _activeUploadCount;
    private readonly object _activeUploadLock = new();

    /// <summary>
    /// Initializes a new instance of the UploadMetrics class.
    /// </summary>
    /// <param name="meterFactory">The OpenTelemetry meter factory.</param>
    /// <param name="configuration">The application configuration.</param>
    public UploadMetrics(IMeterFactory meterFactory, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceName = configuration["Service:Name"] ?? "UploadService";
        _meter = meterFactory.Create($"{serviceName.ToLower()}-meter")
                 ?? new Meter($"{serviceName.ToLower()}-meter");

        _defaultTags = new[]
        {
            new KeyValuePair<string, object?>("service_name", serviceName),
            new KeyValuePair<string, object?>("version", configuration["Service:Version"] ?? "1.0.0"),
            new KeyValuePair<string, object?>("region", configuration["Service:Region"] ?? "global"),
            new KeyValuePair<string, object?>("environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production")
        };

        // T010: Authorization metrics
        _authSuccessCounter = _meter.CreateCounter<long>(
            name: "auth.success",
            unit: "authorizations",
            description: "Number of successful IAM authorization checks"
        );

        _authFailureCounter = _meter.CreateCounter<long>(
            name: "auth.failure",
            unit: "authorizations",
            description: "Number of failed IAM authorization checks"
        );

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
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("file.content_type", contentType);
        tags.Add("file.size_bucket", GetSizeBucket(fileSizeBytes));

        _uploadSuccessCounter.Add(1, tags);
        _uploadDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a failed file upload with error reason.
    /// </summary>
    public void RecordUploadFailure(string serviceId, string contentType, string errorReason)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("file.content_type", contentType);
        tags.Add("error.reason", errorReason);

        _uploadFailureCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a file validation rejection with reason.
    /// </summary>
    public void RecordValidationRejection(string serviceId, string contentType, string rejectionReason)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("file.content_type", contentType);
        tags.Add("rejection.reason", rejectionReason);

        _validationRejectionCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a signed URL generation request.
    /// </summary>
    public void RecordSignedUrlGeneration(string serviceId, long expirationSeconds)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("expiration.seconds", expirationSeconds.ToString());

        _signedUrlGenerationCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a file deletion operation.
    /// </summary>
    public void RecordFileDeletion(string serviceId, bool success, string? errorReason = null)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("result", success ? "success" : "failure");

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
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("service.id", serviceId);
        tags.Add("total_files", totalFiles.ToString());

        _bulkDeleteJobCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a successful IAM authorization check.
    /// </summary>
    public void RecordAuthSuccess(string permission, string? resourcePath)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("iam.permission", permission);
        tags.Add("iam.resource", resourcePath ?? "root");

        _authSuccessCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a failed IAM authorization check.
    /// </summary>
    public void RecordAuthFailure(string permission, string? resourcePath, string reason)
    {
        var tags = new TagList();
        foreach (var tag in _defaultTags) tags.Add(tag);
        tags.Add("iam.permission", permission);
        tags.Add("iam.resource", resourcePath ?? "root");
        tags.Add("error.reason", reason);

        _authFailureCounter.Add(1, tags);
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
