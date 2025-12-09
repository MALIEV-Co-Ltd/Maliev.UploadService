# Implementation Plan: Upload Service

**Branch**: `001-upload-service` | **Date**: 2025-12-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-upload-service/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

The Upload Service is a core infrastructure microservice providing secure file upload capabilities to all MALIEV microservices. It abstracts Google Cloud Storage complexity, enforces validation and security policies, supports streaming uploads, manages file lifecycle and retention, provides access control and signed URLs, and enables high-performance concurrent operations. The service ensures files are validated for content type, size, and malware before storage, organized by service-specific paths, and managed according to configurable retention policies.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**:
- Maliev.Aspire.ServiceDefaults (observability, health checks, resilience)
- Npgsql.EntityFrameworkCore.PostgreSQL (metadata persistence)
- MassTransit.RabbitMQ (asynchronous notifications)
- Google.Cloud.Storage.V1 (GCS integration)
- Microsoft.AspNetCore.OpenApi / Scalar.AspNetCore (API documentation)
- AspNetCore.HealthChecks.UI.Client (health check UI)

**Storage**:
- PostgreSQL (upload metadata, authorization policies, audit logs, bulk delete job tracking)
- Google Cloud Storage (file storage backend)
- Redis (distributed caching for signed URLs, authorization policy cache)

**Testing**:
- xUnit (test framework)
- Moq (mocking framework)
- Testcontainers.PostgreSql (real PostgreSQL instances for integration tests)
- Testcontainers.RabbitMq (real RabbitMQ instances for messaging tests)
- Testcontainers.Redis (real Redis instances for caching tests)
- Custom MockHttpMessageHandler (HTTP mocking for GCS client)

**Target Platform**: Linux containers (Docker) deployed on Google Kubernetes Engine

**Project Type**: Web API microservice (ASP.NET Core)

**Performance Goals**:
- 500+ concurrent uploads without degradation
- <2s upload completion for files up to 10MB
- <500ms metadata retrieval
- Streaming support for multi-GB files with constant memory (<500MB)
- Resumable uploads with checkpoint tracking

**Constraints**:
- Stateless design for horizontal scaling
- 99.9% uptime (excluding cloud provider outages)
- Zero data loss on interruption (resumable uploads)
- 100% authorization enforcement
- All operations logged for audit compliance

**Scale/Scope**:
- Support 20+ consumer microservices
- Per-service configuration (quotas, size limits, allowed content types, path prefixes)
- Multi-tenant path isolation
- Background job processing for bulk operations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: Service Autonomy ✅ COMPLIANT
- Upload Service owns its PostgreSQL database for metadata, policies, and audit logs
- Interacts with other services via REST APIs only
- No direct database access to other services
- Publishes events via RabbitMQ for async notifications

### Principle II: Explicit Contracts ✅ COMPLIANT
- OpenAPI/Scalar documentation for all REST endpoints
- Versioned API (v1)
- Contract definitions in Phase 1 output (`contracts/`)

### Principle III: Test-First Development ✅ COMPLIANT
- Tests will be authored immediately after this plan approval
- Red-Green-Refactor workflow enforced
- Unit, integration, and contract tests planned
- 80%+ coverage target for business logic (validation, authorization, lifecycle)

### Principle IV: Real Infrastructure Testing ✅ COMPLIANT
- Testcontainers.PostgreSql for database tests (no InMemoryDatabase)
- Testcontainers.RabbitMq for messaging tests
- Testcontainers.Redis for caching tests
- Custom MockHttpMessageHandler for GCS client mocking (approved pattern)

### Principle V: Auditability & Observability ✅ COMPLIANT
- Structured JSON logging via standard .NET ILogger
- All upload/delete/retrieval operations logged with user/service identifiers
- Health checks for liveness/readiness
- OpenTelemetry metrics via ServiceDefaults

### Principle VI: Security & Compliance ✅ COMPLIANT
- JWT authentication for all endpoints
- Service-level authorization (path-based access control)
- File validation (malware scanning, content type enforcement)
- Sensitive data encrypted in transit (HTTPS) and at rest (GCS encryption)

### Principle VII: Secrets Management ✅ COMPLIANT
- Google Secret Manager via `AddGoogleSecretManagerVolume()`
- No secrets in source code
- GCS credentials injected at runtime

### Principle VIII: Zero Warnings Policy ✅ COMPLIANT
- Build configured to treat warnings as errors
- No compiler warnings allowed

### Principle IX: Clean Project Artifacts ✅ COMPLIANT
- `.gitignore` excludes bin/, obj/, temporary files
- `.dockerignore` excludes specs/, tests/, IDE files

### Principle X: Docker Best Practices ✅ COMPLIANT
- Multi-stage build (SDK for build, ASP.NET runtime for final image)
- Built-in `app` user (no custom user creation)
- BuildKit secrets for NuGet credentials
- Health check endpoint: `/uploadservice/liveness`
- Port 8080 exposed

### Principle XI: Simplicity & Maintainability ✅ COMPLIANT
- YAGNI applied (no unnecessary abstractions)
- Extension methods for mapping (no AutoMapper)
- Data Annotations for validation (no FluentValidation)

### Principle XII: Business Metrics & Analytics ✅ COMPLIANT
- Metrics for business outcomes:
  - Upload success/failure rates by service
  - File validation rejection rates by type
  - Storage quota utilization by service
  - Bulk delete job progress and completion rates
  - Signed URL generation frequency
  - Average upload duration by file size bucket
- Tagged with service_name, version, environment

### Principle XIII: .NET Aspire Integration ✅ COMPLIANT
- Consumes `Maliev.Aspire.ServiceDefaults` as NuGet package from GitHub Packages
- `nuget.config` with GitHub Packages source
- `builder.AddServiceDefaults()` called first in Program.cs
- `app.MapDefaultEndpoints(servicePrefix: "uploadservice")`

### Principle XIV: Code Quality & Library Standards ✅ COMPLIANT
- NO AutoMapper (explicit mapping via extension methods)
- NO FluentValidation (Data Annotations + manual validation)
- NO FluentAssertions (standard xUnit Assert.*)

### Gate Status: ✅ ALL GATES PASSED

No constitutional violations. Service design fully compliant with MALIEV standards.

## Project Structure

### Documentation (this feature)

```text
specs/001-upload-service/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── openapi.yaml     # OpenAPI 3.0 specification
│   └── rabbitmq-events.md  # RabbitMQ event contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
Maliev.UploadService.Api/
├── Controllers/
│   ├── v1/
│   │   ├── UploadsController.cs
│   │   ├── FilesController.cs
│   │   └── AdminController.cs
├── Models/
│   ├── Entities/
│   │   ├── Upload.cs
│   │   ├── FileMetadata.cs
│   │   ├── ServiceAuthorizationPolicy.cs
│   │   ├── RetentionPolicy.cs
│   │   ├── BulkDeleteJob.cs
│   │   └── UploadEvent.cs
│   ├── Requests/
│   │   ├── UploadFileRequest.cs
│   │   ├── GenerateSignedUrlRequest.cs
│   │   ├── DeleteFileRequest.cs
│   │   ├── BulkDeleteRequest.cs
│   │   └── QueryFilesRequest.cs
│   └── Responses/
│       ├── UploadResponse.cs
│       ├── FileMetadataResponse.cs
│       ├── SignedUrlResponse.cs
│       ├── BulkDeleteJobResponse.cs
│       └── QueryFilesResponse.cs
├── Services/
│   ├── IStorageService.cs
│   ├── GcsStorageService.cs
│   ├── IValidationService.cs
│   ├── FileValidationService.cs
│   ├── IAuthorizationPolicyService.cs
│   ├── AuthorizationPolicyService.cs
│   ├── ILifecycleManagementService.cs
│   ├── LifecycleManagementService.cs
│   ├── IBulkDeleteService.cs
│   └── BulkDeleteService.cs
├── Data/
│   ├── UploadServiceDbContext.cs
│   └── Migrations/
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Extensions/
│   ├── UploadMappingExtensions.cs
│   ├── FileMetadataMappingExtensions.cs
│   └── ValidationExtensions.cs
├── Consumers/  (MassTransit message consumers)
│   └── BulkDeleteJobConsumer.cs
├── BackgroundServices/
│   └── LifecyclePolicyWorker.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile
└── nuget.config

Maliev.UploadService.Tests/
├── Integration/
│   ├── UploadsControllerTests.cs
│   ├── FilesControllerTests.cs
│   ├── AdminControllerTests.cs
│   ├── FileValidationTests.cs
│   ├── AuthorizationTests.cs
│   ├── LifecycleManagementTests.cs
│   └── BulkDeleteTests.cs
├── Unit/
│   ├── Services/
│   │   ├── FileValidationServiceTests.cs
│   │   ├── AuthorizationPolicyServiceTests.cs
│   │   ├── LifecycleManagementServiceTests.cs
│   │   └── BulkDeleteServiceTests.cs
│   └── Extensions/
│       └── MappingExtensionsTests.cs
├── Fixtures/
│   ├── TestWebApplicationFactory.cs
│   ├── TestDatabaseFixture.cs
│   └── MockHttpMessageHandler.cs
└── Maliev.UploadService.Tests.csproj
```

**Structure Decision**: Single microservice with clear separation of concerns. Web API project contains all application logic organized by function (Controllers, Services, Models, Data). Test project mirrors the API structure with Integration and Unit test folders. This follows standard ASP.NET Core microservice patterns and aligns with MALIEV constitution requirements.

## Complexity Tracking

No constitutional violations requiring justification.

## Phase 0: Research Findings

### Research Areas

The following technical unknowns require investigation before design:

1. **GCS Client Library Integration**: Best practices for Google.Cloud.Storage.V1 client in ASP.NET Core (connection pooling, retry policies, streaming patterns)

2. **File Validation Libraries**: Industry-standard libraries for malware scanning and content validation in .NET (ClamAV integration, file type detection libraries)

3. **Streaming Upload Patterns**: ASP.NET Core patterns for handling large file uploads without memory exhaustion (IFormFile streaming, chunked encoding, backpressure handling)

4. **Resumable Upload Implementation**: GCS resumable upload API integration and checkpoint management

5. **Path Sanitization**: Best practices for preventing path traversal and injection attacks in file paths

6. **Bulk Delete Background Jobs**: MassTransit patterns for long-running background jobs with progress tracking and cancellation support

7. **GCS Lifecycle Rules Management**: Programmatic lifecycle rule configuration via Google.Cloud.Storage.V1 API

8. **Signed URL Generation**: GCS signed URL best practices (V2 vs V4 signatures, expiration handling, permission scopes)

9. **Authorization Policy Caching**: Redis caching strategies for authorization policies (cache invalidation, TTL configuration)

10. **Metrics Instrumentation**: OpenTelemetry custom metrics for business outcomes (upload rates, validation metrics, quota tracking)

**Output**: `research.md` will consolidate findings with decisions, rationale, and alternatives considered.

## Phase 1: Design Artifacts

### Data Model (`data-model.md`)

Entities extracted from spec.md Key Entities section:
- Upload
- FileMetadata
- ServiceAuthorizationPolicy
- RetentionPolicy
- SignedAccessUrl (ephemeral, not persisted - generated on demand)
- UploadEvent (audit log)
- BulkDeleteJob

### API Contracts (`contracts/`)

**REST Endpoints** (from functional requirements):

- `POST /api/v1/uploads` - Upload file with validation
- `POST /api/v1/uploads/resumable` - Initiate resumable upload
- `PUT /api/v1/uploads/resumable/{uploadId}` - Continue resumable upload
- `GET /api/v1/files/{uploadId}` - Get file metadata by upload ID
- `GET /api/v1/files` - Query files by path prefix (paginated)
- `POST /api/v1/files/{uploadId}/signed-url` - Generate signed URL
- `DELETE /api/v1/files/{uploadId}` - Delete file
- `POST /api/v1/admin/bulk-delete` - Initiate bulk delete job
- `GET /api/v1/admin/bulk-delete/{jobId}` - Get bulk delete job status

**RabbitMQ Events**:
- `maliev.uploadservice.v1.upload.completed`
- `maliev.uploadservice.v1.upload.failed`
- `maliev.uploadservice.v1.file.deleted`
- `maliev.uploadservice.v1.bulkdelete.completed`

### Quickstart Guide (`quickstart.md`)

Developer onboarding guide covering:
- Local development setup
- Running with Docker Compose (PostgreSQL, Redis, RabbitMQ)
- Authentication token generation for testing
- Example upload requests
- Testing file validation
- Monitoring metrics endpoints

### Agent Context Update

After design completion, run:
```bash
.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude
```

This will update `.specify/memory/agent-context.claude.md` with technology decisions from this plan.

## Phase 2: Task Generation (Deferred)

Task breakdown will be generated by `/speckit.tasks` command after this plan is approved. That command produces `tasks.md` with dependency-ordered implementation tasks.

## Notes

- GCS client mocking will use custom MockHttpMessageHandler following MALIEV testing standards (no WireMock.Net)
- File validation service will need investigation of .NET-compatible malware scanning libraries
- Bulk delete jobs will use MassTransit's consumer pattern with progress tracking stored in PostgreSQL
- Authorization policies will be cached in Redis with aggressive TTL (5 minutes) for performance
- All streaming operations will use ASP.NET Core's streaming primitives to maintain constant memory footprint

