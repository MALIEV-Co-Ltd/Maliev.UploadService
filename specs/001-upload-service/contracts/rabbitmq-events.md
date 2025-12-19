# RabbitMQ Event Contracts - Upload Service

**Date**: 2025-12-05
**Branch**: 001-upload-service
**Service**: Maliev.UploadService

## Overview

This document defines the asynchronous event contracts published by the Upload Service via RabbitMQ. These events enable other microservices to react to file lifecycle changes without polling.

### Routing Key Pattern

All Upload Service events follow the MALIEV routing key convention:

```
maliev.uploadservice.v1.{entity}.{action}
```

### Exchange Configuration

- **Exchange Name**: `maliev.events`
- **Exchange Type**: `topic`
- **Durable**: `true`

### Message Format

All messages use JSON serialization with the following envelope:

```json
{
  "eventId": "uuid",
  "eventType": "string",
  "timestamp": "ISO 8601 datetime",
  "serviceId": "string",
  "payload": { /* event-specific data */ }
}
```

---

## Event Definitions

### 1. Upload Completed

**Routing Key**: `maliev.uploadservice.v1.upload.completed`

**Description**: Published when a file upload completes successfully (after validation passes).

**When Triggered**:
- File uploaded via `/api/v1/uploads`
- File uploaded via resumable upload (`/api/v1/uploads/resumable/{uploadId}`)
- All validation checks passed (content type, malware scan, etc.)

**Payload Schema**:

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "upload.completed",
  "timestamp": "2025-12-05T10:30:00Z",
  "serviceId": "pdf-service",
  "payload": {
    "uploadId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "fileId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "storagePath": "pdf/receipts/12345.pdf",
    "fileName": "receipt_2025_001.pdf",
    "contentType": "application/pdf",
    "fileSize": 1048576,
    "checksum": "d41d8cd98f00b204e9800998ecf8427e",
    "versionETag": "\"33a64df551425fcc55e4d42a148795d9f25f89d4\"",
    "uploadedAt": "2025-12-05T10:30:00Z",
    "retentionPolicyId": "policy-001",
    "expiresAt": "2025-12-12T10:30:00Z",
    "metadata": {
      "orderId": "ORD-12345",
      "customerId": "CUST-67890"
    }
  }
}
```

**Consumer Example** (PDF Service):

```csharp
public class UploadCompletedConsumer : IConsumer<UploadCompletedEvent>
{
    private readonly PdfServiceDbContext _dbContext;

    public async Task Consume(ConsumeContext<UploadCompletedEvent> context)
    {
        var evt = context.Message;

        // Store file reference in PDF service database
        var pdfRecord = new PdfRecord {
            PdfId = Guid.NewGuid(),
            UploadId = evt.Payload.UploadId,
            FileId = evt.Payload.FileId,
            StoragePath = evt.Payload.StoragePath,
            OrderId = evt.Payload.Metadata["orderId"],
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.PdfRecords.AddAsync(pdfRecord);
        await _dbContext.SaveChangesAsync();
    }
}
```

---

### 2. Upload Failed

**Routing Key**: `maliev.uploadservice.v1.upload.failed`

**Description**: Published when a file upload fails validation or encounters an error.

**When Triggered**:
- File exceeds service quota
- Content type not allowed
- Malware detected
- Corrupted file structure
- GCS storage error
- Checksum mismatch

**Payload Schema**:

```json
{
  "eventId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "eventType": "upload.failed",
  "timestamp": "2025-12-05T10:35:00Z",
  "serviceId": "quotation-service",
  "payload": {
    "uploadId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "fileName": "suspicious_file.exe",
    "contentType": "application/x-msdownload",
    "fileSize": 2097152,
    "failureReason": "MALWARE_DETECTED",
    "errorMessage": "Malware scan detected Win32.Trojan.Generic",
    "attemptedAt": "2025-12-05T10:35:00Z",
    "metadata": {
      "quotationId": "Q-2025-001"
    }
  }
}
```

**Consumer Example** (Quotation Service):

```csharp
public class UploadFailedConsumer : IConsumer<UploadFailedEvent>
{
    private readonly ILogger<UploadFailedConsumer> _logger;
    private readonly INotificationService _notificationService;

    public async Task Consume(ConsumeContext<UploadFailedEvent> context)
    {
        var evt = context.Message;

        _logger.LogWarning(
            "Upload failed for quotation {QuotationId}: {Reason}",
            evt.Payload.Metadata["quotationId"],
            evt.Payload.FailureReason
        );

        // Notify user of upload failure
        await _notificationService.SendUploadFailureNotification(
            quotationId: evt.Payload.Metadata["quotationId"],
            reason: evt.Payload.ErrorMessage
        );
    }
}
```

---

### 3. File Deleted

**Routing Key**: `maliev.uploadservice.v1.file.deleted`

**Description**: Published when a file is deleted from storage.

**When Triggered**:
- Manual deletion via `/api/v1/files/{uploadId}` DELETE
- Automatic deletion via retention policy expiration
- Deletion as part of bulk delete job

**Payload Schema**:

```json
{
  "eventId": "b5c6d7e8-9012-34fg-hijk-5678901234lm",
  "eventType": "file.deleted",
  "timestamp": "2025-12-05T10:40:00Z",
  "serviceId": "scan-service",
  "payload": {
    "fileId": "e4d909c2-90d0-4e7f-8f3a-3b0e7c2d3e4f",
    "uploadId": "c8a9b0c1-d2e3-4f5a-6b7c-8d9e0f1a2b3c",
    "storagePath": "scans/raw/2025-12-01/scan-data.stl",
    "deletedAt": "2025-12-05T10:40:00Z",
    "deletedBy": "system",
    "deletionReason": "RETENTION_POLICY_EXPIRED",
    "retentionPolicyId": "policy-002"
  }
}
```

**Consumer Example** (3D Scan Service):

```csharp
public class FileDeletedConsumer : IConsumer<FileDeletedEvent>
{
    private readonly ScanServiceDbContext _dbContext;

    public async Task Consume(ConsumeContext<FileDeletedEvent> context)
    {
        var evt = context.Message;

        // Mark file as deleted in local database
        var scanRecord = await _dbContext.ScanFiles
            .FirstOrDefaultAsync(s => s.UploadId == evt.Payload.UploadId);

        if (scanRecord != null)
        {
            scanRecord.Status = ScanFileStatus.Deleted;
            scanRecord.DeletedAt = evt.Payload.DeletedAt;
            await _dbContext.SaveChangesAsync();
        }
    }
}
```

---

### 4. Bulk Delete Completed

**Routing Key**: `maliev.uploadservice.v1.bulkdelete.completed`

**Description**: Published when a bulk delete job finishes (successfully or with errors).

**When Triggered**:
- Bulk delete job completes via `/api/v1/admin/bulk-delete`
- All files processed (successfully deleted or failed)

**Payload Schema**:

```json
{
  "eventId": "c9d0e1f2-3456-78gh-ijkl-9012345678mn",
  "eventType": "bulkdelete.completed",
  "timestamp": "2025-12-05T11:00:00Z",
  "serviceId": "legacy-service",
  "payload": {
    "jobId": "job-12345",
    "serviceId": "legacy-service",
    "pathPrefix": "legacy/",
    "status": "CompletedWithErrors",
    "filesTotal": 1000,
    "filesProcessed": 1000,
    "filesDeleted": 987,
    "errorCount": 13,
    "createdAt": "2025-12-05T10:00:00Z",
    "startedAt": "2025-12-05T10:05:00Z",
    "completedAt": "2025-12-05T11:00:00Z",
    "durationSeconds": 3300,
    "errorSummary": [
      "File not found: legacy/file1.dat",
      "File not found: legacy/file2.dat",
      "Permission denied: legacy/file3.dat"
    ]
  }
}
```

**Consumer Example** (Admin Dashboard):

```csharp
public class BulkDeleteCompletedConsumer : IConsumer<BulkDeleteCompletedEvent>
{
    private readonly INotificationService _notificationService;

    public async Task Consume(ConsumeContext<BulkDeleteCompletedEvent> context)
    {
        var evt = context.Message;

        // Notify admin of bulk delete completion
        await _notificationService.SendAdminNotification(
            subject: $"Bulk Delete Job {evt.Payload.JobId} Completed",
            message: $"Deleted {evt.Payload.FilesDeleted}/{evt.Payload.FilesTotal} files. " +
                     $"Errors: {evt.Payload.ErrorCount}. Duration: {evt.Payload.DurationSeconds}s."
        );
    }
}
```

---

## Message Properties

All messages include standard MassTransit headers:

| Header | Type | Description |
|--------|------|-------------|
| `MessageId` | GUID | Unique message identifier (idempotency key) |
| `CorrelationId` | GUID | Request correlation ID (if available) |
| `SourceAddress` | URI | Queue address of sender |
| `DestinationAddress` | URI | Queue address of receiver |
| `MessageType` | string[] | Full CLR type names |
| `SentTime` | datetime | When message was sent (ISO 8601) |

---

## Consumer Configuration

### MassTransit Registration

```csharp
// Consumer service (e.g., PDF Service)
services.AddMassTransit(x =>
{
    x.AddConsumer<UploadCompletedConsumer>();
    x.AddConsumer<UploadFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("pdf-service-upload-events", e =>
        {
            e.ConfigureConsumer<UploadCompletedConsumer>(context);
            e.ConfigureConsumer<UploadFailedConsumer>(context);

            // Bind to routing keys
            e.Bind("maliev.events", s =>
            {
                s.RoutingKey = "maliev.uploadservice.v1.upload.completed";
                s.ExchangeType = "topic";
            });

            e.Bind("maliev.events", s =>
            {
                s.RoutingKey = "maliev.uploadservice.v1.upload.failed";
                s.ExchangeType = "topic";
            });
        });
    });
});
```

---

## Retry & Error Handling

### Retry Policy

All events use exponential backoff retry:

- Initial interval: 5 seconds
- Interval increment: 2x (exponential)
- Max attempts: 5
- Max interval: 60 seconds

### Dead Letter Queue

Failed messages (after 5 retries) are routed to:

```
Queue: maliev.uploadservice.events.error
Exchange: maliev.events.error
```

Consumers should implement idempotency using `MessageId` header to prevent duplicate processing.

---

## Event Ordering Guarantees

- Events for the **same uploadId** are published in order
- Events for **different uploadIds** may arrive out of order
- Use `timestamp` field for event ordering if needed
- `CorrelationId` links related events (e.g., upload.failed after upload.completed indicates race condition)

---

## Security & Access Control

- All messages are published to authenticated RabbitMQ exchange
- Consumer services must authenticate with valid credentials
- No sensitive data (file contents, passwords) included in messages
- Metadata fields may contain business identifiers (order IDs, customer IDs) - consumers are responsible for authorization

---

## Monitoring & Observability

### Metrics to Track (Consumer Side)

- **Message processing rate** (messages/second)
- **Message processing duration** (histogram)
- **Message processing errors** (count by error type)
- **Queue depth** (gauge)

### Logging

All consumers should log:

```csharp
_logger.LogInformation(
    "Processing {EventType} for UploadId {UploadId}",
    context.Message.EventType,
    context.Message.Payload.UploadId
);
```

---

## Testing

### Integration Test Example

```csharp
public class UploadCompletedConsumerTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rabbitMq;

    public UploadCompletedConsumerTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    [Fact]
    public async Task Consume_UploadCompletedEvent_StoresFileReference()
    {
        // Arrange
        var evt = new UploadCompletedEvent {
            EventId = Guid.NewGuid(),
            EventType = "upload.completed",
            Timestamp = DateTime.UtcNow,
            ServiceId = "pdf-service",
            Payload = new UploadCompletedPayload {
                UploadId = Guid.NewGuid(),
                FileId = Guid.NewGuid(),
                StoragePath = "pdf/test.pdf",
                FileSize = 1024
            }
        };

        // Act
        await _rabbitMq.Publish("maliev.uploadservice.v1.upload.completed", evt);
        await Task.Delay(1000); // Wait for processing

        // Assert
        var record = await _dbContext.PdfRecords
            .FirstOrDefaultAsync(p => p.UploadId == evt.Payload.UploadId);

        Assert.NotNull(record);
        Assert.Equal(evt.Payload.FileId, record.FileId);
    }
}
```

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-05 | Initial event contract definition |

---

## Contact

For questions or proposed changes to event contracts, contact:

- **Team**: MALIEV Platform Engineering
- **Email**: platform-eng@maliev.com
- **Slack**: #maliev-platform-eng

