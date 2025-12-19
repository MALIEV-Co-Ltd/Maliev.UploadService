# Phase 0: Research Findings - Upload Service

**Date**: 2025-12-05
**Branch**: 001-upload-service

## Overview

This document consolidates research findings for the Upload Service implementation. Each section addresses a technical unknown identified in the planning phase, providing the chosen solution, rationale, and alternatives considered.

---

## 1. GCS Client Library Integration

### Decision

Use `Google.Cloud.Storage.V1` NuGet package with:
- Singleton registration via dependency injection
- Default credential provider (`GoogleCredential.GetApplicationDefault()`)
- Built-in retry policies via gRPC configuration
- Streaming via `StorageClient.UploadObjectAsync` with Stream parameter

### Rationale

- Official Google-maintained library with active support
- Native gRPC transport for performance
- Built-in authentication, retry, and timeout handling
- Streaming support prevents memory exhaustion for large files
- Integrates well with ASP.NET Core DI container

### Implementation Pattern

```csharp
// Program.cs
builder.Services.AddSingleton(provider => {
    var credential = GoogleCredential.GetApplicationDefault();
    return new StorageClientBuilder {
        Credential = credential,
        GrpcAdapter = GrpcAdapter.Default
    }.Build();
});

// GcsStorageService.cs
public async Task<string> UploadStreamAsync(
    string bucketName,
    string objectName,
    Stream content,
    string contentType,
    CancellationToken ct)
{
    var obj = await _storageClient.UploadObjectAsync(
        bucketName,
        objectName,
        contentType,
        content,
        cancellationToken: ct
    );
    return obj.MediaLink;
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| REST API via HttpClient | Less efficient than gRPC; more manual retry/auth handling required |
| Direct gRPC proto generation | Unnecessary complexity; official client already uses gRPC |
| AWS SDK S3-compatible mode | Not native to GCS; missing GCS-specific features (signed URLs V4, lifecycle management) |

---

## 2. File Validation Libraries

### Decision

Use multi-layered validation approach:
- **Content-Type Detection**: `MimeDetective` NuGet package (magic number inspection)
- **File Extension Validation**: Custom whitelist/blacklist per service configuration
- **Malware Scanning**: `nClam` NuGet package (ClamAV client) for async malware scanning
- **File Structure Validation**: Format-specific libraries (e.g., `PdfSharp` for PDF validation, `AssimpNet` for 3D models) on-demand

### Rationale

- MimeDetective performs magic number inspection (more reliable than file extension alone)
- nClam integrates with industry-standard ClamAV antivirus engine
- Async scanning prevents blocking upload pipeline
- Format-specific validation catches corrupted files early
- Layered approach provides defense-in-depth

### Implementation Pattern

```csharp
public class FileValidationService : IValidationService
{
    private readonly MimeDetective.FileTypeDetector _mimeDetector;
    private readonly ClamClient _clamClient;

    public async Task<ValidationResult> ValidateAsync(Stream fileStream, FileUploadContext ctx)
    {
        // Layer 1: MIME type detection
        fileStream.Position = 0;
        var detectedType = _mimeDetector.DetectMimeType(fileStream);
        if (!ctx.AllowedMimeTypes.Contains(detectedType))
            return ValidationResult.Fail("Invalid file type");

        // Layer 2: Extension check
        if (!IsExtensionAllowed(ctx.FileName, ctx.AllowedExtensions))
            return ValidationResult.Fail("Invalid file extension");

        // Layer 3: Malware scan
        fileStream.Position = 0;
        var scanResult = await _clamClient.SendAndScanFileAsync(fileStream);
        if (scanResult.Result != ClamScanResults.Clean)
            return ValidationResult.Fail("Malware detected");

        // Layer 4: Format-specific validation (if configured)
        if (ctx.ValidateStructure)
        {
            fileStream.Position = 0;
            var structureResult = await ValidateFileStructure(fileStream, detectedType);
            if (!structureResult.IsValid)
                return ValidationResult.Fail($"Corrupted file: {structureResult.Message}");
        }

        return ValidationResult.Success();
    }
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Windows Defender API | Linux deployment target; not cross-platform |
| VirusTotal API | External dependency; rate limits; latency; cost |
| In-process scanning library | Security risk (runs in same process); potential DoS vector |
| File extension only | Trivially bypassed; insufficient security |

### Dependencies

- `MimeDetective` (NuGet)
- `nClam` (NuGet)
- ClamAV daemon (external service, runs in separate container)

---

## 3. Streaming Upload Patterns

### Decision

Use `IFormFile` with streaming configuration and request body size limits:
- `DisableRequestSizeLimit` attribute for upload endpoints
- `MultipartBodyLengthLimit` configured per endpoint
- Stream directly from request to GCS without buffering
- Memory-efficient chunked reading (16KB buffer)

### Rationale

- ASP.NET Core's `IFormFile` provides streaming access to multipart form data
- Direct stream-to-GCS pipeline prevents memory accumulation
- Chunked reading maintains constant memory footprint
- Built-in ASP.NET Core primitives (no custom multipart parsing)
- Backpressure handled automatically by HTTP/2 flow control

### Implementation Pattern

```csharp
[HttpPost("uploads")]
[DisableRequestSizeLimit]
[RequestFormLimits(MultipartBodyLengthLimit = 10_737_418_240)] // 10GB
public async Task<IActionResult> UploadFile(
    [FromForm] IFormFile file,
    [FromForm] UploadFileRequest request,
    CancellationToken ct)
{
    // Validate size before streaming
    if (file.Length > GetServiceQuota(request.ServiceId))
        return BadRequest("File exceeds service quota");

    // Stream directly to GCS
    using var stream = file.OpenReadStream();
    var result = await _storageService.UploadStreamAsync(
        bucketName: _config.GcsBucketName,
        objectName: ResolvePath(request.TargetPath),
        content: stream,
        contentType: file.ContentType,
        ct: ct
    );

    return Ok(result);
}
```

### Configuration

```csharp
// Program.cs
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10_737_418_240; // 10GB global limit
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10_737_418_240; // 10GB
});
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Custom multipart parser | Unnecessary complexity; ASP.NET Core provides robust implementation |
| In-memory buffering | Memory exhaustion risk for large files |
| Temporary file storage | Slower; I/O overhead; cleanup complexity |
| SignalR/WebSockets | Overkill; HTTP multipart sufficient for file uploads |

---

## 4. Resumable Upload Implementation

### Decision

Use GCS Resumable Upload API with session-based checkpoint tracking:
- Initiate resumable session via `StorageClient.StartResumableUploadAsync`
- Track session URI in database (`Upload` entity)
- Resume via `StorageClient.ResumeResumableUploadAsync` with byte offset
- Client provides `Content-Range` header for resume requests

### Rationale

- GCS native resumable upload protocol handles complexity
- Session URI provides durable resume token
- Byte-level precision for resume (no data loss)
- Automatic checksum validation by GCS
- Built into Google.Cloud.Storage.V1 client library

### Implementation Pattern

```csharp
// Initiate resumable upload
[HttpPost("uploads/resumable")]
public async Task<IActionResult> InitiateResumableUpload(
    [FromBody] InitiateUploadRequest request,
    CancellationToken ct)
{
    var uploadId = Guid.NewGuid().ToString();
    var sessionUri = await _storageClient.InitiateUploadSessionAsync(
        _config.GcsBucketName,
        ResolvePath(request.TargetPath),
        request.ContentType,
        ct
    );

    // Persist session for resume
    await _dbContext.Uploads.AddAsync(new Upload {
        UploadId = uploadId,
        SessionUri = sessionUri,
        ServiceId = request.ServiceId,
        TargetPath = request.TargetPath,
        Status = UploadStatus.InProgress,
        BytesUploaded = 0,
        TotalBytes = request.FileSize
    }, ct);

    await _dbContext.SaveChangesAsync(ct);

    return Ok(new { UploadId = uploadId, SessionUri = sessionUri });
}

// Resume upload
[HttpPut("uploads/resumable/{uploadId}")]
public async Task<IActionResult> ResumeUpload(
    string uploadId,
    [FromHeader(Name = "Content-Range")] string contentRange,
    CancellationToken ct)
{
    var upload = await _dbContext.Uploads.FindAsync(uploadId);
    if (upload == null) return NotFound();

    // Parse Content-Range: bytes 0-1023/2048
    var (startByte, endByte, totalBytes) = ParseContentRange(contentRange);

    using var requestStream = Request.Body;
    var result = await _storageClient.UploadObjectAsync(
        upload.SessionUri,
        requestStream,
        startByte,
        ct
    );

    upload.BytesUploaded = endByte + 1;
    if (upload.BytesUploaded >= upload.TotalBytes)
        upload.Status = UploadStatus.Completed;

    await _dbContext.SaveChangesAsync(ct);

    return Ok(new { BytesUploaded = upload.BytesUploaded });
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Custom chunking without GCS resumable API | Reinventing the wheel; error-prone |
| Azure Blob block upload pattern | Not compatible with GCS |
| TUS protocol | Requires additional library; GCS native API sufficient |

---

## 5. Path Sanitization

### Decision

Multi-stage path validation and sanitization:
1. Reject absolute paths (starting with `/` or drive letters)
2. Reject path traversal sequences (`..`, `./`, `\\`)
3. Whitelist allowed characters: alphanumeric, hyphens, underscores, slashes, periods, curly braces (for templates)
4. Normalize slashes to forward slashes
5. Trim leading/trailing slashes
6. Validate against service-specific path prefix whitelist

### Rationale

- Defense in depth against path traversal attacks
- Prevents access to unauthorized storage locations
- Template support preserves dynamic path functionality
- Service-level prefix isolation enforces multi-tenancy
- Explicit whitelist approach (deny by default)

### Implementation Pattern

```csharp
public class PathSanitizer
{
    private static readonly Regex AllowedPathPattern =
        new(@"^[a-zA-Z0-9\-_/.{} ]+$", RegexOptions.Compiled);

    private static readonly string[] DisallowedSequences = { "..", "./", ".\\" };

    public static (bool IsValid, string SanitizedPath, string Error) Sanitize(
        string rawPath,
        string serviceId,
        IEnumerable<string> allowedPrefixes)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return (false, null, "Path cannot be empty");

        // Reject absolute paths
        if (Path.IsPathFullyQualified(rawPath))
            return (false, null, "Absolute paths not allowed");

        // Check for traversal sequences
        foreach (var sequence in DisallowedSequences)
        {
            if (rawPath.Contains(sequence, StringComparison.OrdinalIgnoreCase))
                return (false, null, $"Path traversal detected: {sequence}");
        }

        // Whitelist character validation
        if (!AllowedPathPattern.IsMatch(rawPath))
            return (false, null, "Path contains invalid characters");

        // Normalize slashes
        var normalized = rawPath.Replace("\\", "/").Trim('/');

        // Validate service prefix
        var hasValidPrefix = allowedPrefixes.Any(prefix =>
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!hasValidPrefix)
            return (false, null, $"Path must start with allowed prefix for service {serviceId}");

        return (true, normalized, null);
    }
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Path.GetFullPath + Contains check | Insufficient; doesn't prevent all traversal attacks |
| URL encoding | Doesn't prevent traversal; adds complexity |
| Regex only | Misses contextual validation (service prefixes) |
| Blacklist approach | Incomplete; whitelisting is more secure |

---

## 6. Bulk Delete Background Jobs

### Decision

Use MassTransit consumer pattern with database-backed progress tracking:
- Publish `BulkDeleteJobRequest` message to queue
- Consumer processes in batches (100 files per batch)
- Update `BulkDeleteJob` entity in database after each batch
- Support cancellation via `CancellationToken`
- Idempotent processing (check if file already deleted)

### Rationale

- MassTransit provides reliable message delivery and retry
- Database persistence enables progress monitoring
- Batch processing prevents overwhelming GCS API
- Cancellation support allows admin abort
- Idempotency prevents duplicate deletion errors

### Implementation Pattern

```csharp
// Publish job
public async Task<string> InitiateBulkDelete(string serviceId, string pathPrefix)
{
    var jobId = Guid.NewGuid().ToString();

    var job = new BulkDeleteJob {
        JobId = jobId,
        ServiceId = serviceId,
        PathPrefix = pathPrefix,
        Status = BulkDeleteStatus.Queued,
        FilesProcessed = 0,
        FilesTotal = await CountFilesAsync(serviceId, pathPrefix),
        CreatedAt = DateTime.UtcNow
    };

    await _dbContext.BulkDeleteJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    await _publishEndpoint.Publish(new BulkDeleteJobRequest {
        JobId = jobId,
        ServiceId = serviceId,
        PathPrefix = pathPrefix
    });

    return jobId;
}

// Consumer
public class BulkDeleteJobConsumer : IConsumer<BulkDeleteJobRequest>
{
    private const int BatchSize = 100;

    public async Task Consume(ConsumeContext<BulkDeleteJobRequest> context)
    {
        var job = await _dbContext.BulkDeleteJobs.FindAsync(context.Message.JobId);
        job.Status = BulkDeleteStatus.InProgress;
        await _dbContext.SaveChangesAsync();

        var files = await GetFilesToDelete(
            context.Message.ServiceId,
            context.Message.PathPrefix
        );

        foreach (var batch in files.Chunk(BatchSize))
        {
            foreach (var file in batch)
            {
                try
                {
                    await _storageService.DeleteAsync(file.StoragePath, context.CancellationToken);
                    await _dbContext.FileMetadata.Where(f => f.Id == file.Id).ExecuteDeleteAsync();
                    job.FilesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete file {Path}", file.StoragePath);
                    job.ErrorCount++;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        job.Status = job.ErrorCount > 0
            ? BulkDeleteStatus.CompletedWithErrors
            : BulkDeleteStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Synchronous loop in API endpoint | Blocks request; no progress tracking; timeout risk |
| Hosted BackgroundService with polling | Less reliable than message queue; harder to scale |
| Azure Durable Functions pattern | Not applicable to GKE deployment; vendor lock-in |
| Immediate synchronous deletion | Timeout for large datasets; poor user experience |

---

## 7. GCS Lifecycle Rules Management

### Decision

Use `Google.Cloud.Storage.V1` Bucket Lifecycle API:
- Define lifecycle rules in configuration (JSON or C# objects)
- Apply rules programmatically via `StorageClient.PatchBucket`
- Tag objects with metadata for rule matching
- Use GCS's native lifecycle engine for automatic deletion

### Rationale

- GCS handles deletion scheduling and execution
- Serverless (no background worker needed for deletions)
- Highly reliable (GCS guarantees execution)
- Cost-effective (no compute resources for deletion)
- Metadata-based targeting provides flexibility

### Implementation Pattern

```csharp
public async Task ApplyRetentionPolicy(string bucketName, RetentionPolicy policy)
{
    var bucket = await _storageClient.GetBucketAsync(bucketName);

    bucket.Lifecycle = new Bucket.LifecycleData {
        Rule = new List<Bucket.LifecycleData.RuleData> {
            new() {
                Action = new() { Type = "Delete" },
                Condition = new() {
                    Age = policy.RetentionDays,
                    MatchesPrefix = new[] { policy.PathPrefix },
                    MatchesMetadata = new Dictionary<string, string> {
                        ["retention-policy"] = policy.PolicyId
                    }
                }
            }
        }
    };

    await _storageClient.PatchBucketAsync(bucket);
}

// Tag objects during upload
public async Task UploadWithRetention(Stream content, string path, string policyId)
{
    var obj = new Google.Apis.Storage.v1.Data.Object {
        Name = path,
        Metadata = new Dictionary<string, string> {
            ["retention-policy"] = policyId,
            ["retention-applied-at"] = DateTime.UtcNow.ToString("o")
        }
    };

    await _storageClient.UploadObjectAsync(obj, content);
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Background worker with deletion logic | Less reliable; requires compute resources; reinvents GCS functionality |
| Manual deletion via scheduled job | Error-prone; less efficient than GCS native |
| File timestamps only | Less flexible; doesn't support policy-based retention |

### Documentation Reference

- [GCS Lifecycle Management](https://cloud.google.com/storage/docs/lifecycle)

---

## 8. Signed URL Generation

### Decision

Use GCS Signed URL V4 with:
- `StorageClient.CreateV4SignedUrl` method
- Configurable expiration (default 1 hour, max 7 days)
- Read-only permission scope
- Service account credentials for signing

### Rationale

- V4 is current standard (V2 deprecated)
- Built into Google.Cloud.Storage.V1
- Supports longer expiration periods than V2
- More secure signature algorithm (SHA-256)
- Consistent with GCS best practices

### Implementation Pattern

```csharp
public async Task<string> GenerateSignedUrl(
    string objectPath,
    TimeSpan expiration,
    CancellationToken ct)
{
    var urlSigner = UrlSigner.FromCredential(await GoogleCredential.GetApplicationDefaultAsync());

    var signedUrl = await urlSigner.SignAsync(
        bucket: _config.GcsBucketName,
        objectName: objectPath,
        duration: expiration,
        signingVersion: SigningVersion.V4,
        httpMethod: HttpMethod.Get
    );

    // Cache signed URL in Redis (key: objectPath, TTL: expiration)
    await _cache.SetStringAsync(
        key: $"signed-url:{objectPath}",
        value: signedUrl,
        options: new DistributedCacheEntryOptions {
            AbsoluteExpirationRelativeToNow = expiration
        },
        token: ct
    );

    return signedUrl;
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| Signed URL V2 | Deprecated; shorter max expiration; less secure |
| Public object access | Security risk; no expiration; inappropriate for sensitive data |
| Proxy through Upload Service | Unnecessary bandwidth cost; adds latency |
| Pre-authenticated requests (Oracle Cloud pattern) | Not applicable to GCS |

### Best Practices

- Use shortest practical expiration time
- Invalidate URLs on file deletion (cache eviction)
- Log URL generation for audit trail
- Consider rate limiting to prevent abuse

---

## 9. Authorization Policy Caching

### Decision

Use Redis distributed cache with:
- Cache key pattern: `authz:policy:{serviceId}`
- TTL: 5 minutes (aggressive refresh for security)
- Cache-aside pattern (check cache, fallback to database)
- Invalidation on policy update via cache key delete

### Rationale

- Authorization checks occur on every request (hot path)
- Database roundtrip adds latency
- Redis provides sub-millisecond reads
- Short TTL balances performance and security
- Distributed cache enables multi-instance deployment

### Implementation Pattern

```csharp
public class AuthorizationPolicyService : IAuthorizationPolicyService
{
    private const int CacheTtlMinutes = 5;

    public async Task<ServiceAuthorizationPolicy> GetPolicyAsync(string serviceId, CancellationToken ct)
    {
        var cacheKey = $"authz:policy:{serviceId}";

        // Check cache
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<ServiceAuthorizationPolicy>(cached);
        }

        // Fallback to database
        var policy = await _dbContext.ServiceAuthorizationPolicies
            .FirstOrDefaultAsync(p => p.ServiceId == serviceId, ct);

        if (policy == null)
            throw new UnauthorizedAccessException($"No policy found for service {serviceId}");

        // Cache for 5 minutes
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(policy),
            new DistributedCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTtlMinutes)
            },
            ct
        );

        return policy;
    }

    public async Task InvalidatePolicyCache(string serviceId, CancellationToken ct)
    {
        await _cache.RemoveAsync($"authz:policy:{serviceId}", ct);
    }
}
```

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| In-memory cache (IMemoryCache) | Doesn't scale across instances; invalidation issues |
| Longer TTL (30+ minutes) | Security risk; policy changes take too long to propagate |
| No caching | Unacceptable latency for high-throughput service |
| Database query cache | Less control; not distributed |

### Configuration

```json
{
  "ConnectionStrings": {
    "redis": "localhost:6379"
  },
  "RedisCache": {
    "InstanceName": "UploadService:"
  }
}
```

---

## 10. Metrics Instrumentation

### Decision

Use OpenTelemetry custom metrics via `System.Diagnostics.Metrics` API:
- Counters for upload success/failure rates
- Histograms for upload duration distribution
- Gauges for active uploads, quota utilization
- Tag dimensions: service_name, file_type, size_bucket, environment

### Rationale

- OpenTelemetry is CNCF standard
- Built into .NET 8+ via System.Diagnostics.Metrics
- Maliev.Aspire.ServiceDefaults configures exporters
- Supports Prometheus, OTLP, and other backends
- No additional libraries required (avoid prometheus-net per constitution)

### Implementation Pattern

```csharp
public class UploadMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _uploadSuccessCounter;
    private readonly Counter<long> _uploadFailureCounter;
    private readonly Histogram<double> _uploadDurationHistogram;
    private readonly ObservableGauge<long> _activeUploadsGauge;
    private readonly ObservableGauge<double> _quotaUtilizationGauge;

    public UploadMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Maliev.UploadService");

        _uploadSuccessCounter = _meter.CreateCounter<long>(
            "upload.success",
            unit: "uploads",
            description: "Number of successful uploads"
        );

        _uploadFailureCounter = _meter.CreateCounter<long>(
            "upload.failure",
            unit: "uploads",
            description: "Number of failed uploads"
        );

        _uploadDurationHistogram = _meter.CreateHistogram<double>(
            "upload.duration",
            unit: "ms",
            description: "Upload duration distribution"
        );

        _activeUploadsGauge = _meter.CreateObservableGauge<long>(
            "upload.active",
            observeValue: () => GetActiveUploadCount(),
            unit: "uploads",
            description: "Current number of active uploads"
        );

        _quotaUtilizationGauge = _meter.CreateObservableGauge<double>(
            "storage.quota.utilization",
            observeValues: () => GetQuotaUtilization(),
            unit: "percent",
            description: "Storage quota utilization by service"
        );
    }

    public void RecordUploadSuccess(string serviceId, string fileType, long sizeBytes, double durationMs)
    {
        var tags = new TagList {
            { "service.name", "upload-service" },
            { "service.id", serviceId },
            { "file.type", fileType },
            { "file.size.bucket", GetSizeBucket(sizeBytes) },
            { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") }
        };

        _uploadSuccessCounter.Add(1, tags);
        _uploadDurationHistogram.Record(durationMs, tags);
    }

    private static string GetSizeBucket(long bytes) => bytes switch {
        < 1_048_576 => "<1MB",
        < 10_485_760 => "1-10MB",
        < 104_857_600 => "10-100MB",
        < 1_073_741_824 => "100MB-1GB",
        _ => ">1GB"
    };
}
```

### Business Metrics to Track

1. **Upload success/failure rates** (tagged by service, file type)
2. **Upload duration** (histogram by size bucket)
3. **File validation rejection rates** (by rejection reason)
4. **Storage quota utilization** (percentage by service)
5. **Active upload count** (current in-flight uploads)
6. **Bulk delete job progress** (files processed vs total)
7. **Signed URL generation rate** (requests per minute)
8. **Authorization policy cache hit rate**
9. **GCS API call latency** (integration performance)
10. **Resumable upload resumption rate** (interrupted uploads resumed successfully)

### Alternatives Considered

| Alternative | Reason Not Chosen |
|-------------|-------------------|
| prometheus-net library | Violates Constitution Principle XII (use OpenTelemetry) |
| Application Insights SDK | Vendor lock-in; OpenTelemetry is vendor-neutral |
| Custom metrics endpoint | Reinventing standards; poor interoperability |
| EventCounters only | Less flexible than OpenTelemetry; limited tagging |

### Configuration

```csharp
// Program.cs - already configured by ServiceDefaults
builder.AddServiceDefaults(); // Configures OpenTelemetry, Prometheus exporter

// Metrics available at:
// - /uploadservice/metrics (Prometheus format)
// - OTLP export to telemetry gateway (configured via ServiceDefaults)
```

---

## Summary

All 10 technical unknowns have been researched and resolved with concrete implementation decisions. The chosen solutions align with:

- MALIEV Constitution requirements (Aspire, OpenTelemetry, Real Infrastructure Testing)
- .NET 10 best practices
- Google Cloud Platform standards
- Security and performance requirements from the feature specification

**Next Phase**: Proceed to Phase 1 design artifacts (data-model.md, contracts/, quickstart.md).

