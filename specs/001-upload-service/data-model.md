# Data Model: Upload Service

**Date**: 2025-12-05
**Branch**: 001-upload-service

## Overview

This document defines the data model for the Upload Service. All entities are persisted in PostgreSQL except where noted. The model supports file metadata tracking, authorization policies, audit logging, lifecycle management, and background job tracking.

---

## Entity Definitions

### 1. Upload

Represents a completed or in-progress file upload operation.

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **UploadId** | string (GUID) | PK, Required, Unique | Unique identifier for the upload |
| **ServiceId** | string | Required, Indexed | Identifier of the requesting microservice |
| **UserId** | string | Optional | User identifier from JWT (if available) |
| **FileName** | string | Required | Original filename provided by client |
| **ContentType** | string | Required | MIME type of the uploaded file |
| **FileSize** | long | Required, Min(0) | Size in bytes |
| **Checksum** | string | Optional | Client-provided checksum (MD5/SHA256) |
| **StoragePath** | string | Required, Unique | Final resolved path in GCS |
| **SessionUri** | string | Optional | GCS resumable upload session URI (null if not resumable) |
| **BytesUploaded** | long | Required, Default(0) | Bytes uploaded so far (for resumable uploads) |
| **Status** | enum | Required | Upload status (see UploadStatus enum) |
| **UploadedAt** | datetime | Required, Default(UtcNow) | Timestamp when upload initiated |
| **CompletedAt** | datetime | Optional | Timestamp when upload completed or failed |
| **ErrorMessage** | string | Optional | Error description if Status = Failed |
| **RetentionPolicyId** | string | Optional, FK | Associated retention policy (if applicable) |
| **Metadata** | jsonb | Optional | Additional metadata tags (key-value pairs) |

#### UploadStatus Enum

```csharp
public enum UploadStatus
{
    Pending,      // Resumable upload initiated, not started
    InProgress,   // Upload in progress
    Validating,   // File uploaded, validation in progress
    Completed,    // Upload and validation successful
    Failed        // Upload or validation failed
}
```

#### Relationships

- Upload **belongs to** ServiceAuthorizationPolicy (via ServiceId)
- Upload **may have** RetentionPolicy (via RetentionPolicyId)
- Upload **has many** UploadEvent (audit trail)

#### Indexes

```sql
CREATE INDEX idx_uploads_service_id ON uploads(service_id);
CREATE INDEX idx_uploads_status ON uploads(status);
CREATE INDEX idx_uploads_uploaded_at ON uploads(uploaded_at);
CREATE INDEX idx_uploads_storage_path ON uploads(storage_path);
```

#### Validation Rules

- FileSize must be ≤ service quota (checked in business logic)
- ContentType must be in service allowlist (checked in business logic)
- StoragePath must pass sanitization (checked in business logic)
- SessionUri required if Status = InProgress or Pending

---

### 2. FileMetadata

Represents stored information about an uploaded file. Created when Upload transitions to Completed status.

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **FileId** | string (GUID) | PK, Required, Unique | Unique identifier for the file |
| **UploadId** | string (GUID) | FK, Required, Unique | Associated upload (one-to-one) |
| **ServiceId** | string | Required, Indexed | Owning service identifier |
| **StoragePath** | string | Required, Unique | Path in GCS |
| **VersionETag** | string | Required | GCS ETag for versioning |
| **FileSize** | long | Required | Size in bytes |
| **ContentType** | string | Required | MIME type |
| **Checksum** | string | Required | Actual checksum (computed after upload) |
| **UploadedAt** | datetime | Required | When file was uploaded |
| **LastAccessedAt** | datetime | Optional | Last time signed URL was generated (for analytics) |
| **RetentionPolicyId** | string | Optional, FK | Associated retention policy |
| **StorageClass** | string | Required, Default("STANDARD") | GCS storage class (STANDARD, NEARLINE, COLDLINE, ARCHIVE) |
| **ExpiresAt** | datetime | Optional | Calculated expiration date (if retention policy applied) |
| **Metadata** | jsonb | Optional | Additional metadata tags |

#### Relationships

- FileMetadata **belongs to** Upload (one-to-one via UploadId)
- FileMetadata **belongs to** ServiceAuthorizationPolicy (via ServiceId)
- FileMetadata **may have** RetentionPolicy (via RetentionPolicyId)

#### Indexes

```sql
CREATE INDEX idx_filemetadata_service_id ON file_metadata(service_id);
CREATE INDEX idx_filemetadata_storage_path ON file_metadata(storage_path);
CREATE INDEX idx_filemetadata_expires_at ON file_metadata(expires_at);
CREATE INDEX idx_filemetadata_uploaded_at ON file_metadata(uploaded_at);
```

#### Validation Rules

- UploadId must reference a completed Upload
- StoragePath must be unique across all files
- ExpiresAt must be > UploadedAt (if set)

---

### 3. ServiceAuthorizationPolicy

Defines which paths and operations each microservice is permitted to access.

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **PolicyId** | string (GUID) | PK, Required, Unique | Unique policy identifier |
| **ServiceId** | string | Required, Unique | Microservice identifier (from JWT claims) |
| **ServiceName** | string | Required | Human-readable service name |
| **AllowedPathPrefixes** | string[] | Required, Min(1) | Allowed path prefixes (e.g., ["pdf/", "quotations/"]) |
| **AllowedContentTypes** | string[] | Required | Whitelist of MIME types (e.g., ["application/pdf"]) |
| **MaxFileSizeBytes** | long | Required, Min(1) | Maximum file size for this service |
| **StorageQuotaBytes** | long | Required, Min(0) | Total storage quota for service |
| **AllowOverwrite** | bool | Required, Default(false) | Whether service can overwrite existing files |
| **AllowResumableUpload** | bool | Required, Default(true) | Whether service can use resumable uploads |
| **CreatedAt** | datetime | Required | When policy was created |
| **UpdatedAt** | datetime | Required | When policy was last modified |
| **IsActive** | bool | Required, Default(true) | Whether policy is currently active |

#### Relationships

- ServiceAuthorizationPolicy **has many** Upload (via ServiceId)
- ServiceAuthorizationPolicy **has many** FileMetadata (via ServiceId)

#### Indexes

```sql
CREATE UNIQUE INDEX idx_authz_policy_service_id ON service_authorization_policies(service_id);
CREATE INDEX idx_authz_policy_is_active ON service_authorization_policies(is_active);
```

#### Validation Rules

- AllowedPathPrefixes must not overlap with other services (business rule)
- AllowedContentTypes must contain at least one value
- MaxFileSizeBytes must be ≤ 10GB (system limit)
- StorageQuotaBytes must be > 0

---

### 4. RetentionPolicy

Defines lifecycle rules for uploaded files.

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **PolicyId** | string (GUID) | PK, Required, Unique | Unique policy identifier |
| **PolicyName** | string | Required | Human-readable name (e.g., "7-day-transient") |
| **ServiceId** | string | Optional, Indexed | Service this policy applies to (null = global) |
| **RetentionDays** | int | Required, Min(0) | Days to retain files (0 = indefinite) |
| **StorageClassTransitions** | jsonb | Optional | Storage class transition rules (see below) |
| **ApplyToPathPrefix** | string | Optional | Path prefix this policy applies to |
| **IsActive** | bool | Required, Default(true) | Whether policy is currently active |
| **CreatedAt** | datetime | Required | When policy was created |
| **UpdatedAt** | datetime | Required | When policy was last modified |

#### StorageClassTransitions Schema (JSONB)

```json
[
  {
    "days": 30,
    "storageClass": "NEARLINE"
  },
  {
    "days": 90,
    "storageClass": "COLDLINE"
  }
]
```

#### Relationships

- RetentionPolicy **has many** Upload (via RetentionPolicyId)
- RetentionPolicy **has many** FileMetadata (via RetentionPolicyId)

#### Indexes

```sql
CREATE INDEX idx_retention_policy_service_id ON retention_policies(service_id);
CREATE INDEX idx_retention_policy_is_active ON retention_policies(is_active);
```

#### Validation Rules

- RetentionDays = 0 means indefinite retention
- StorageClassTransitions days must be in ascending order
- ApplyToPathPrefix must match service's allowed prefixes (if ServiceId set)

---

### 5. UploadEvent (Audit Log)

Represents an auditable event (upload initiated, completed, failed, file retrieved, file deleted).

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **EventId** | string (GUID) | PK, Required, Unique | Unique event identifier |
| **EventType** | enum | Required | Type of event (see UploadEventType enum) |
| **ServiceId** | string | Required, Indexed | Service that triggered the event |
| **UserId** | string | Optional | User identifier from JWT (if available) |
| **UploadId** | string (GUID) | Optional, FK, Indexed | Associated upload (if applicable) |
| **FileId** | string (GUID) | Optional, FK, Indexed | Associated file (if applicable) |
| **StoragePath** | string | Optional | File path (if applicable) |
| **EventTimestamp** | datetime | Required, Default(UtcNow) | When event occurred |
| **EventResult** | enum | Required | Success, Failure, or Warning |
| **ErrorDetails** | string | Optional | Error description if EventResult = Failure |
| **IpAddress** | string | Optional | Client IP address (for security auditing) |
| **Metadata** | jsonb | Optional | Additional context (e.g., validation failure reason) |

#### UploadEventType Enum

```csharp
public enum UploadEventType
{
    UploadInitiated,
    UploadCompleted,
    UploadFailed,
    FileRetrieved,
    FileDeleted,
    SignedUrlGenerated,
    ValidationFailed,
    AuthorizationDenied,
    BulkDeleteInitiated
}
```

#### EventResult Enum

```csharp
public enum EventResult
{
    Success,
    Failure,
    Warning
}
```

#### Relationships

- UploadEvent **may belong to** Upload (via UploadId)
- UploadEvent **may belong to** FileMetadata (via FileId)

#### Indexes

```sql
CREATE INDEX idx_upload_events_service_id ON upload_events(service_id);
CREATE INDEX idx_upload_events_event_type ON upload_events(event_type);
CREATE INDEX idx_upload_events_timestamp ON upload_events(event_timestamp);
CREATE INDEX idx_upload_events_upload_id ON upload_events(upload_id);
CREATE INDEX idx_upload_events_file_id ON upload_events(file_id);
```

#### Validation Rules

- EventTimestamp cannot be in the future
- ErrorDetails required if EventResult = Failure
- At least one of UploadId, FileId, or StoragePath should be set (business rule)

---

### 6. BulkDeleteJob

Represents a long-running background operation to delete multiple files.

#### Attributes

| Attribute | Type | Constraints | Description |
|-----------|------|-------------|-------------|
| **JobId** | string (GUID) | PK, Required, Unique | Unique job identifier |
| **ServiceId** | string | Required, Indexed | Service whose files are being deleted |
| **PathPrefix** | string | Required | Path prefix for bulk deletion |
| **InitiatedBy** | string | Required | Admin user who initiated the job |
| **Status** | enum | Required | Job status (see BulkDeleteStatus enum) |
| **FilesTotal** | int | Required, Min(0) | Total number of files to delete |
| **FilesProcessed** | int | Required, Default(0) | Number of files processed so far |
| **FilesDeleted** | int | Required, Default(0) | Number of files successfully deleted |
| **ErrorCount** | int | Required, Default(0) | Number of deletion failures |
| **CreatedAt** | datetime | Required | When job was created |
| **StartedAt** | datetime | Optional | When job processing started |
| **CompletedAt** | datetime | Optional | When job finished (success or failure) |
| **ErrorDetails** | jsonb | Optional | Array of error messages |

#### BulkDeleteStatus Enum

```csharp
public enum BulkDeleteStatus
{
    Queued,               // Job created, waiting for worker
    InProgress,           // Worker is processing
    Completed,            // All files deleted successfully
    CompletedWithErrors,  // Job completed but some deletions failed
    Failed,               // Job failed entirely
    Cancelled             // Job was cancelled by admin
}
```

#### Relationships

- BulkDeleteJob **belongs to** ServiceAuthorizationPolicy (via ServiceId)

#### Indexes

```sql
CREATE INDEX idx_bulk_delete_service_id ON bulk_delete_jobs(service_id);
CREATE INDEX idx_bulk_delete_status ON bulk_delete_jobs(status);
CREATE INDEX idx_bulk_delete_created_at ON bulk_delete_jobs(created_at);
```

#### Validation Rules

- FilesProcessed ≤ FilesTotal
- FilesDeleted ≤ FilesProcessed
- ErrorCount = FilesProcessed - FilesDeleted
- CompletedAt must be after StartedAt (if both set)

---

## Relationships Diagram

```
ServiceAuthorizationPolicy
  ├── 1:N → Upload
  ├── 1:N → FileMetadata
  └── 1:N → BulkDeleteJob

Upload
  ├── 1:1 → FileMetadata
  ├── 1:N → UploadEvent
  └── N:1 → RetentionPolicy (optional)

FileMetadata
  ├── 1:1 → Upload
  ├── N:1 → RetentionPolicy (optional)
  └── 1:N → UploadEvent

RetentionPolicy
  ├── 1:N → Upload
  └── 1:N → FileMetadata

UploadEvent
  ├── N:1 → Upload (optional)
  └── N:1 → FileMetadata (optional)

BulkDeleteJob
  └── N:1 → ServiceAuthorizationPolicy
```

---

## Entity Framework Core Configuration

### DbContext Definition

```csharp
public class UploadServiceDbContext : DbContext
{
    public DbSet<Upload> Uploads { get; set; }
    public DbSet<FileMetadata> FileMetadata { get; set; }
    public DbSet<ServiceAuthorizationPolicy> ServiceAuthorizationPolicies { get; set; }
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
    public DbSet<UploadEvent> UploadEvents { get; set; }
    public DbSet<BulkDeleteJob> BulkDeleteJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Upload configuration
        modelBuilder.Entity<Upload>(entity =>
        {
            entity.HasKey(e => e.UploadId);
            entity.Property(e => e.UploadId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.StoragePath).IsUnique();
        });

        // FileMetadata configuration
        modelBuilder.Entity<FileMetadata>(entity =>
        {
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.FileId).ValueGeneratedNever();
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.StoragePath).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasOne<Upload>()
                .WithOne()
                .HasForeignKey<FileMetadata>(e => e.UploadId);
        });

        // ServiceAuthorizationPolicy configuration
        modelBuilder.Entity<ServiceAuthorizationPolicy>(entity =>
        {
            entity.HasKey(e => e.PolicyId);
            entity.Property(e => e.PolicyId).ValueGeneratedNever();
            entity.Property(e => e.AllowedPathPrefixes).HasColumnType("jsonb");
            entity.Property(e => e.AllowedContentTypes).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).IsUnique();
        });

        // RetentionPolicy configuration
        modelBuilder.Entity<RetentionPolicy>(entity =>
        {
            entity.HasKey(e => e.PolicyId);
            entity.Property(e => e.PolicyId).ValueGeneratedNever();
            entity.Property(e => e.StorageClassTransitions).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId);
        });

        // UploadEvent configuration
        modelBuilder.Entity<UploadEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).ValueGeneratedNever();
            entity.Property(e => e.EventType).HasConversion<string>();
            entity.Property(e => e.EventResult).HasConversion<string>();
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.EventTimestamp);
            entity.HasIndex(e => e.UploadId);
            entity.HasIndex(e => e.FileId);
        });

        // BulkDeleteJob configuration
        modelBuilder.Entity<BulkDeleteJob>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.ErrorDetails).HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
```

---

## Data Validation Summary

| Entity | Key Validations |
|--------|----------------|
| **Upload** | FileSize ≤ service quota; ContentType in allowlist; StoragePath sanitized |
| **FileMetadata** | UploadId references completed Upload; StoragePath unique; ExpiresAt > UploadedAt |
| **ServiceAuthorizationPolicy** | AllowedPathPrefixes non-overlapping; MaxFileSizeBytes ≤ 10GB; StorageQuotaBytes > 0 |
| **RetentionPolicy** | RetentionDays ≥ 0; StorageClassTransitions ascending order |
| **UploadEvent** | EventTimestamp not in future; ErrorDetails if Failure |
| **BulkDeleteJob** | FilesProcessed ≤ FilesTotal; ErrorCount = FilesProcessed - FilesDeleted |

---

## Notes

- All string primary keys (UploadId, FileId, etc.) are GUIDs for global uniqueness
- JSONB columns used for flexible metadata and array storage (PostgreSQL-specific)
- All datetime fields use UTC timezone
- Enums stored as strings for readability in database
- Indexes optimized for common query patterns (by ServiceId, by Status, by Timestamp)
- Foreign key relationships configured for referential integrity
- Soft delete not used (hard delete on file removal as per specification)

