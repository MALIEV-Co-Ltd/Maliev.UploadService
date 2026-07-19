# Feature Specification: Upload Service

**Feature Branch**: `001-upload-service`
**Created**: 2025-12-05
**Status**: Draft
**Input**: User description: "The Upload Service is a core infrastructure microservice responsible for securely handling file uploads from all other services within the MALIEV microservices ecosystem. Its primary function is to provide a fast, safe, scalable file-ingestion pipeline into Google Cloud Storage (GCS), with the flexibility to support additional cloud storage providers in the future. The service abstracts all storage-related complexity away from domain services and offers a unified upload interface with strong guarantees around safety, throughput, metadata handling, and file organization. The Upload Service must support direct streaming uploads from other microservices. This enables large-file handling without loading entire files into memory and minimizes network latency. The upload pipeline must enforce strict, configurable file-size limits per requesting service. It must validate content-type, file extension, and file structure where applicable. Malicious or unsafe files (e.g., executable malware, invalid PDFs, corrupted STL models) must be rejected before any data is committed to the storage backend. The system must isolate file-parsing logic to reduce risk of injection vulnerabilities and ensure sandboxing for any deeper validation scans if needed. Each service must be able to specify its own upload path dynamically. This allows the Upload Service to categorize and organize files based on domain context, business workflow, and long-term maintainability. Examples include: pdf/receipts/{id}.pdf for the PDF Service, quotations/{qid}/attachments/ for Quotation Service, or scans/raw/{timestamp}/ for 3D Scan Service. The Upload Service should enforce naming conventions, avoid collisions, and provide deterministic path resolution rules. The service must support metadata tagging, retention policies, and automatic expiration rules for cold storage. Certain uploaded files—such as temporary artifacts, logs, scan raw data, or transient processing files—should be purged automatically after a configurable duration. Cold storage policies must be configurable per domain service, with clear API definitions allowing services to opt into short-lived or long-term storage classes. Lifecycle management must integrate with cloud-provider-native features such as GCS lifecycle rules. The Upload Service must expose basic CRUD operations for uploaded files. This includes retrieving metadata, generating signed URLs for controlled access, overwriting or deleting files, and querying for uploads by path or ID. Deletion requests must be safely handled and synchronized with retention policies. For retrieval, the service must ensure that sensitive files cannot be requested without proper authentication and access control enforcement. Upon successful upload, the service must return a standardized response containing references such as storage path, version or ETag, upload ID, file size, checksum, and URL references (signed or internal). This response is crucial for downstream services—for example, the PDF Service must capture the returned metadata to store artifact references in its own database. The service must also support asynchronous completion notifications if uploads occur in background streams or chunked operations. Notifications may be delivered through RabbitMQ or callback mechanisms. Performance is a top-level requirement. The Upload Service must be optimized for high concurrency, large throughput, low latency, and efficient streaming. It must support horizontal scaling, ideally stateless in its upload handling except for minimal metadata operations. It must rely on GCS or other cloud storage providers for durability and replication. Internal caching, connection pooling, and resumable uploads should be supported where appropriate. Security is the highest priority. The Upload Service must validate authentication tokens for all incoming requests, enforce strict authorization policies to ensure services can access only their designated storage paths, sanitize file names, and validate all user-provided metadata. It must protect against path traversal, cross-service data leaks, corrupted uploads, and denial-of-service vectors. It must log all upload events, rejections, and deletions for auditing and traceability. Overall, the Upload Service should function as the centralized, secure, high-performance file-ingestion backbone of the MALIEV microservice ecosystem, enabling consistent handling of files across all functional domains while ensuring long-term scalability, safety, and maintainability."

## User Scenarios & Testing

### User Story 1 - Secure File Upload with Validation (Priority: P1)

A microservice (e.g., PDF Service) needs to upload a receipt file for a customer order. The file must be validated for type, size, and safety before being stored in the designated location. The PDF Service receives back a storage reference that it can persist in its database for future retrieval.

**Why this priority**: This is the core value proposition of the Upload Service. Without secure upload and validation, the entire service purpose is unfulfilled. This story represents the fundamental file ingestion flow that all other services depend on.

**Independent Test**: Can be fully tested by sending a valid file upload request from a test client, verifying file validation occurs, and confirming a successful storage reference is returned.

**Acceptance Scenarios**:

1. **Given** the PDF Service has a valid authentication token and a PDF file to upload, **When** it sends an upload request to the Upload Service with target path "pdf/receipts/12345.pdf", **Then** the file is validated, stored in cloud storage at the specified path, and a response containing storage path, upload ID, file size, checksum, and signed URL is returned.

2. **Given** a service attempts to upload a file exceeding the configured size limit for that service, **When** the upload request is received, **Then** the upload is rejected before any data is written, and an error response indicating "file size exceeds limit" is returned.

3. **Given** a service attempts to upload a file with an invalid content type (e.g., executable when only PDFs are allowed), **When** the upload request is processed, **Then** the file is rejected during validation, no data is committed to storage, and an error response indicating "invalid file type" is returned.

4. **Given** a service sends a potentially malicious file (e.g., malware disguised as PDF), **When** the file validation runs, **Then** the malicious content is detected, the upload is rejected, and an audit log entry is created documenting the attempted upload.

---

### User Story 2 - Dynamic Path Organization and Collision Avoidance (Priority: P2)

Different microservices need to organize their files according to their domain needs. The Quotation Service uploads multiple attachments to "quotations/{qid}/attachments/", the 3D Scan Service uploads raw scan data to "scans/raw/{timestamp}/", and the system prevents path collisions and enforces naming conventions.

**Why this priority**: Path organization is critical for long-term maintainability and discoverability. Without this, files become disorganized and services risk overwriting each other's data. This is essential for multi-tenant service architecture but can be tested after core upload functionality exists.

**Independent Test**: Can be tested by submitting upload requests from different services with various path patterns, verifying paths are resolved correctly, and confirming no collisions occur for deterministic naming.

**Acceptance Scenarios**:

1. **Given** the Quotation Service uploads a file with path "quotations/Q-2025-001/attachments/invoice.pdf", **When** the upload is processed, **Then** the file is stored at the exact specified path with proper directory structure created.

2. **Given** two services attempt to upload files to the same path simultaneously, **When** the Upload Service processes these requests, **Then** a deterministic collision resolution rule applies (e.g., second upload fails with "path already exists" or auto-generates unique suffix based on configuration).

3. **Given** a service specifies an invalid path with special characters or path traversal attempts (e.g., "../../../etc/passwd"), **When** the path is validated, **Then** the upload is rejected with "invalid path" error before any processing occurs.

4. **Given** a service uploads a file with path "scans/raw/{timestamp}/scan-data.stl" where {timestamp} is dynamically resolved, **When** the upload completes, **Then** the actual timestamp value replaces the placeholder, and the response includes the final resolved path.

---

### User Story 3 - File Retrieval and Access Control (Priority: P1)

After uploading files, services need to retrieve them or provide controlled access to external clients. The Upload Service generates signed URLs for time-limited access and enforces authorization so services can only access files in their designated paths.

**Why this priority**: File retrieval is as fundamental as upload. Without the ability to retrieve uploaded files, the service is incomplete. Access control ensures security and prevents cross-service data leaks. This is a critical MVP component.

**Independent Test**: Can be tested by uploading a file, then requesting its metadata and a signed URL, verifying the URL works for the specified duration, and confirming unauthorized access attempts are blocked.

**Acceptance Scenarios**:

1. **Given** a file has been successfully uploaded to "pdf/receipts/12345.pdf", **When** the PDF Service requests file metadata by path or upload ID, **Then** metadata including file size, upload timestamp, checksum, and storage path is returned.

2. **Given** a service requests a signed URL for a file it owns, **When** the Upload Service generates the URL with a 1-hour expiration, **Then** the URL allows download access for 1 hour and returns "expired" error after that time.

3. **Given** the Quotation Service attempts to retrieve a file from the PDF Service's path (e.g., "pdf/receipts/12345.pdf"), **When** the authorization check runs, **Then** the request is denied with "unauthorized access" error.

4. **Given** a file exists at a specified path, **When** an authenticated service queries for uploads by path prefix (e.g., "quotations/Q-2025-001/"), **Then** a list of all files under that path is returned with metadata for each file.

---

### User Story 4 - Lifecycle Management and Retention Policies (Priority: P2)

Services need to configure automatic deletion of temporary or transient files. The 3D Scan Service sets a 7-day retention for raw scan data, while the PDF Service keeps receipts indefinitely. The Upload Service applies these policies automatically using cloud-native lifecycle rules.

**Why this priority**: Lifecycle management reduces storage costs and ensures compliance with data retention policies. While important, the service can function without this initially, as manual deletion can serve as a workaround. This is valuable for production but not blocking for MVP.

**Independent Test**: Can be tested by uploading files with different retention policies, fast-forwarding time (or adjusting cloud lifecycle rules), and verifying files are purged according to policy.

**Acceptance Scenarios**:

1. **Given** the 3D Scan Service uploads a file with metadata tag "retention: 7-days", **When** 7 days elapse, **Then** the cloud storage lifecycle rule automatically deletes the file.

2. **Given** the PDF Service uploads a file with metadata tag "retention: indefinite", **When** any lifecycle evaluation occurs, **Then** the file is never automatically deleted.

3. **Given** a service configures a custom storage class for cold storage (e.g., "archive" for files older than 90 days), **When** files reach 90 days old, **Then** they are transitioned to the archive storage class automatically.

4. **Given** a service requests deletion of a file before its retention period expires, **When** the deletion request is validated against retention policy, **Then** the deletion is allowed with a warning logged, enabling services to revoke incorrectly uploaded files while maintaining audit trail.

---

### User Story 5 - File Deletion with Safety Checks (Priority: P2)

Services need the ability to delete files they no longer need. Deletions must be synchronized with retention policies, logged for audit purposes, and prevented for files that shouldn't be deleted based on business rules.

**Why this priority**: Deletion capability is important for data hygiene and compliance, but it's secondary to upload/retrieval. The service can be useful without deletion initially (files just accumulate). This is needed for production readiness but not for initial validation.

**Independent Test**: Can be tested by uploading a file, issuing a delete request, and verifying the file is removed from storage and an audit log entry is created.

**Acceptance Scenarios**:

1. **Given** a file exists at "quotations/Q-2025-001/attachments/draft.pdf" with no retention restrictions, **When** the Quotation Service sends a delete request, **Then** the file is removed from storage and a deletion audit log is created.

2. **Given** a file has a retention policy preventing deletion before 30 days, **When** a delete request is issued within that period, **Then** the deletion is blocked and an error "cannot delete: retention policy active" is returned.

3. **Given** a service attempts to delete a file in another service's path, **When** authorization is checked, **Then** the deletion is denied with "unauthorized" error.

4. **Given** a file is successfully deleted, **When** the same service attempts to retrieve that file, **Then** a "file not found" response is returned.

---

### User Story 6 - Large File Streaming and Resumable Uploads (Priority: P3)

Services need to upload very large files (e.g., multi-gigabyte 3D scan files) efficiently. The Upload Service supports streaming to avoid memory exhaustion and allows resumable uploads if network interruptions occur.

**Why this priority**: While important for handling large files, most initial use cases involve smaller documents. The service can provide value with basic upload first, then add streaming optimization. This is an enhancement rather than a blocker.

**Independent Test**: Can be tested by uploading a large file (e.g., 1GB+) via streaming, monitoring memory usage to confirm it stays constant, and interrupting/resuming an upload to verify resumability.

**Acceptance Scenarios**:

1. **Given** a service uploads a 2GB STL file via streaming, **When** the upload is in progress, **Then** the Upload Service memory consumption remains constant regardless of file size.

2. **Given** a resumable upload is initiated and partially completed, **When** the connection is interrupted at 60% progress, **Then** the service can resume from 60% rather than restarting from 0%.

3. **Given** a streaming upload is in progress, **When** the upload completes successfully, **Then** the same standardized response (path, upload ID, checksum, etc.) is returned as with non-streaming uploads.

---

### User Story 7 - Asynchronous Upload Notifications (Priority: P3)

For background or long-running uploads, services need asynchronous notification when uploads complete. The Upload Service sends completion events via RabbitMQ or callback webhooks so services don't need to poll for status.

**Why this priority**: Async notifications improve system efficiency but are not required for basic functionality. Services can poll for upload status or wait synchronously for small files. This is a nice-to-have optimization for production systems handling many concurrent uploads.

**Independent Test**: Can be tested by initiating a background upload, subscribing to the notification channel, and verifying a completion message is received with full upload metadata.

**Acceptance Scenarios**:

1. **Given** a service initiates a background upload and provides a callback URL, **When** the upload completes successfully, **Then** the Upload Service sends an HTTP POST to the callback URL with upload metadata.

2. **Given** a service subscribes to upload events via RabbitMQ queue "upload-service.events", **When** any upload completes, **Then** an event message containing upload ID, status, and metadata is published to that queue.

3. **Given** a background upload fails due to validation error, **When** the failure occurs, **Then** a failure notification is sent with error details.

---

### Edge Cases

- **What happens when the cloud storage provider is temporarily unavailable?** The Upload Service should return a "service temporarily unavailable" error and allow the client to retry. Uploads should not be partially committed.

- **What happens when a file upload is interrupted mid-stream?** If resumable uploads are enabled, partial data is preserved for resumption. Otherwise, the partial upload is discarded and marked as failed.

- **What happens when a service's storage quota is exceeded?** The upload is rejected with "quota exceeded" error before writing data. Configuration determines per-service quotas.

- **What happens when multiple versions of the same file path are uploaded?** By default, subsequent uploads to an existing path fail with "file already exists" error. Services must either delete the existing file first or explicitly set an "overwrite" flag in the upload request to replace the file. This prevents accidental overwrites while supporting intentional updates.

- **What happens when a signed URL is used after its expiration time?** Access is denied with a "URL expired" error. The service must request a new signed URL.

- **What happens when a service is decommissioned and its uploaded files need bulk deletion?** The Upload Service provides an admin bulk-delete API endpoint that accepts a service identifier or path prefix and initiates a background deletion job. The job runs asynchronously with progress tracking and status reporting, allowing administrators to monitor completion without blocking other operations.

- **What happens when validation scanning detects a file that is corrupted but not malicious?** The upload is rejected with "file validation failed: corrupted file" error. The requesting service can retry or handle the error according to its business logic.

- **What happens when two services have overlapping path permissions?** The authorization model must ensure no overlaps exist in configuration. If overlaps are detected, system returns configuration error.

- **What happens when the checksum provided by the client doesn't match the received file?** The upload is rejected with "checksum mismatch" error, indicating potential data corruption during transmission.

## Requirements

### Functional Requirements

- **FR-001**: System MUST accept file uploads via HTTP streaming from authenticated microservices.
- **FR-002**: System MUST validate authentication tokens for all incoming requests and reject unauthenticated requests.
- **FR-003**: System MUST enforce configurable file-size limits per requesting service and reject files exceeding those limits before committing data.
- **FR-004**: System MUST validate file content-type and extension against service-specific allowlists.
- **FR-005**: System MUST scan uploaded files for malicious content (e.g., malware, exploits) and reject unsafe files before storage commitment.
- **FR-006**: System MUST isolate file validation logic to prevent injection vulnerabilities and contain potential security risks.
- **FR-007**: System MUST allow each service to specify dynamic upload paths with variable placeholders (e.g., {id}, {timestamp}).
- **FR-008**: System MUST enforce naming conventions, sanitize file names, and validate paths to prevent path traversal attacks.
- **FR-009**: System MUST resolve path placeholders deterministically and provide the final resolved path in upload responses.
- **FR-010**: System MUST prevent path collisions according to configurable rules (fail on collision or auto-generate unique names).
- **FR-011**: System MUST support metadata tagging on uploaded files (e.g., retention policy, storage class, service identifier).
- **FR-012**: System MUST integrate with cloud provider lifecycle management (e.g., GCS lifecycle rules) to apply retention policies.
- **FR-013**: System MUST automatically purge files according to configured retention periods.
- **FR-014**: System MUST support configurable storage classes (e.g., standard, cold, archive) per service or per file.
- **FR-015**: System MUST return a standardized response upon successful upload containing: storage path, version/ETag, upload ID, file size, checksum, and signed URL.
- **FR-016**: System MUST retrieve file metadata by upload ID or storage path.
- **FR-017**: System MUST generate time-limited signed URLs for file access with configurable expiration periods.
- **FR-018**: System MUST enforce authorization policies ensuring services can only access files in their designated paths.
- **FR-019**: System MUST support querying uploaded files by path prefix with pagination.
- **FR-020**: System MUST support file deletion requests with authorization and retention policy checks.
- **FR-021**: System MUST log all upload, retrieval, and deletion events for auditing and traceability.
- **FR-022**: System MUST support resumable uploads for large files to handle network interruptions.
- **FR-023**: System MUST maintain stateless operation for upload handling to enable horizontal scaling.
- **FR-024**: System MUST integrate with cloud storage (initially GCS) for durable file persistence and replication.
- **FR-025**: System MUST support asynchronous upload completion notifications via message queue (e.g., RabbitMQ) or HTTP callbacks.
- **FR-026**: System MUST validate client-provided checksums against received file data and reject mismatches.
- **FR-027**: System MUST sanitize all user-provided metadata to prevent injection attacks.
- **FR-028**: System MUST enforce per-service storage quotas and reject uploads exceeding quota limits.
- **FR-029**: System MUST handle cloud storage provider unavailability gracefully with appropriate error responses and retry guidance.
- **FR-030**: System MUST support file overwrites when explicitly requested via overwrite flag and authorized, otherwise fail with "file already exists" error.
- **FR-031**: System MUST allow deletion of files before retention period expiration with warning-level audit log entry to support revocation of incorrectly uploaded files.
- **FR-032**: System MUST provide admin bulk-delete API endpoint accepting service identifier or path prefix for decommissioned service cleanup.
- **FR-033**: System MUST execute bulk-delete operations as asynchronous background jobs with progress tracking and status reporting.

### Key Entities

- **Upload**: Represents a completed or in-progress file upload operation. Key attributes include upload ID, requesting service identifier, file size, content type, checksum, storage path, upload timestamp, status (pending/completed/failed), and associated metadata tags.

- **File Metadata**: Represents stored information about an uploaded file including storage path, version/ETag, file size, content type, checksum, upload timestamp, retention policy, storage class, and access control rules.

- **Service Authorization Policy**: Defines which paths and operations each microservice is permitted to access. Includes service identifier, allowed path prefixes, allowed operations (upload/read/delete), file size limits, allowed content types, and storage quotas.

- **Retention Policy**: Defines lifecycle rules for uploaded files including retention duration, storage class transitions, and automatic deletion triggers. Associated with service or individual files.

- **Signed Access URL**: Time-limited URL providing temporary access to a file. Includes file path, expiration timestamp, access permissions (read/write), and cryptographic signature.

- **Upload Event**: Represents an auditable event (upload initiated, completed, failed, file retrieved, file deleted). Includes event type, timestamp, requesting service, user/system identifier, file path, operation result, and any error details.

- **Bulk Delete Job**: Represents a long-running background operation to delete multiple files. Includes job ID, target service identifier or path prefix, initiating admin user, start timestamp, current status (queued/in-progress/completed/failed), progress metrics (files processed/total), and completion timestamp.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Services can successfully upload files and receive storage references in under 2 seconds for files up to 10MB.
- **SC-002**: System handles at least 500 concurrent upload operations without performance degradation or failures.
- **SC-003**: Malicious file uploads are detected and rejected with 99%+ accuracy before any data is committed to storage.
- **SC-004**: Authorization violations (cross-service access attempts) are blocked 100% of the time with appropriate audit logging.
- **SC-005**: 95% of file retrievals via signed URLs complete successfully within 500ms.
- **SC-006**: Large file uploads (1GB+) complete via streaming without exceeding 500MB memory consumption on the Upload Service.
- **SC-007**: Files subject to retention policies are automatically deleted within 24 hours of policy expiration with zero manual intervention.
- **SC-008**: Upload service achieves 99.9% uptime excluding cloud storage provider outages.
- **SC-009**: All upload, retrieval, and deletion operations are logged with complete audit trail including service identity and timestamps.
- **SC-010**: Services can successfully resume interrupted uploads from last checkpoint with zero data loss.
- **SC-011**: 90% of services report the upload interface as easy to integrate and use (measured via integration developer feedback).
- **SC-012**: Path collision conflicts are detected and prevented 100% of the time according to configured rules.

## Assumptions

- Cloud storage (GCS initially) provides the durability, replication, and availability guarantees. The Upload Service relies on GCS for data persistence.
- Microservices authenticate using standard token-based authentication (e.g., JWT, OAuth2). The Upload Service validates tokens but does not issue them.
- Network connectivity between microservices and the Upload Service is reliable, but the service handles transient failures gracefully.
- File validation (malware scanning, content verification) uses industry-standard tools and libraries appropriate for each file type.
- Services are configured with appropriate file size limits, allowed content types, and path prefixes before they begin uploading files.
- The message queue infrastructure (RabbitMQ) is available and operational for asynchronous notifications.
- Storage costs are acceptable for the expected volume and retention policies configured by services.
- Services are responsible for managing their own metadata references to uploaded files (e.g., storing upload IDs in their databases).

## Dependencies

- Cloud storage provider (Google Cloud Storage initially) must be provisioned and accessible.
- Authentication and authorization service must provide valid tokens for microservices.
- Message queue infrastructure (RabbitMQ) must be available for asynchronous notifications (optional for MVP).
- File validation tools and libraries (malware scanners, content validators) must be integrated and kept up to date.
- Networking infrastructure must support high-throughput streaming data transfers between services.

## Out of Scope

- The Upload Service does not provide file transformation, processing, or format conversion. Services handle their own business logic on uploaded files.
- The Upload Service does not implement its own authentication system; it validates tokens issued by an external authentication service.
- The Upload Service does not provide a user-facing UI; it is a backend service with APIs consumed by other microservices.
- The Upload Service does not store business-domain metadata (e.g., "this receipt belongs to customer X"); services manage their own domain relationships.
- The Upload Service does not provide full-text search or content indexing of uploaded files; retrieval is by path or upload ID only.
- The Upload Service initially supports GCS; multi-cloud abstraction may be added in future iterations but is not part of the initial scope.
