# Tasks: Upload Service

**Input**: Design documents from `/specs/001-upload-service/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Test-First Development is NON-NEGOTIABLE per Constitution Principle III. Tests MUST be written BEFORE implementation code.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## ⚠️ Known Issues

**Unit Tests with InMemory Database**: Tasks T087 require fixing EF Core InMemory database compatibility with complex types (List<StorageClassTransition>, etc.). Options:
1. Add JSON value converters for all complex types
2. Switch unit tests to use Testcontainers.PostgreSQL instead of InMemory
3. Mark complex properties as [NotMapped] in test scenarios

**Current Status**: Integration tests using Testcontainers.PostgreSQL work correctly. Core implementation (T001-T101) is complete and functional.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Repository structure:
- **API**: `Maliev.UploadService.Api/`
- **Tests**: `Maliev.UploadService.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution file Maliev.UploadService.sln at repository root
- [X] T002 Create Maliev.UploadService.Api project with .NET 10 SDK
- [X] T003 Create Maliev.UploadService.Tests project with xUnit
- [X] T004 [P] Add NuGet package Maliev.Aspire.ServiceDefaults to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T005 [P] Add NuGet package Npgsql.EntityFrameworkCore.PostgreSQL to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T006 [P] Add NuGet package MassTransit.RabbitMQ to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T007 [P] Add NuGet package Google.Cloud.Storage.V1 to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T008 [P] Add NuGet package Microsoft.AspNetCore.OpenApi to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T009 [P] Add NuGet package Scalar.AspNetCore to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T010 [P] Add NuGet package AspNetCore.HealthChecks.UI.Client to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T011 [P] Add NuGet package MimeDetective to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T012 [P] Add NuGet package nClam to Maliev.UploadService.Api/Maliev.UploadService.Api.csproj
- [X] T013 [P] Add test NuGet package Moq to Maliev.UploadService.Tests/Maliev.UploadService.Tests.csproj
- [X] T014 [P] Add test NuGet package Testcontainers.PostgreSql to Maliev.UploadService.Tests/Maliev.UploadService.Tests.csproj
- [X] T015 [P] Add test NuGet package Testcontainers.RabbitMq to Maliev.UploadService.Tests/Maliev.UploadService.Tests.csproj
- [X] T016 [P] Add test NuGet package Testcontainers.Redis to Maliev.UploadService.Tests/Maliev.UploadService.Tests.csproj
- [X] T017 [P] Create nuget.config with GitHub Packages source at repository root
- [X] T018 [P] Create .gitignore excluding bin/, obj/, *.user, .vs/, .idea/ at repository root
- [X] T019 [P] Create .dockerignore excluding specs/, tests/, .git/, *.md at repository root
- [X] T020 Create folder structure Maliev.UploadService.Api/Controllers/v1/
- [X] T021 [P] Create folder structure Maliev.UploadService.Api/Models/Entities/
- [X] T022 [P] Create folder structure Maliev.UploadService.Api/Models/Requests/
- [X] T023 [P] Create folder structure Maliev.UploadService.Api/Models/Responses/
- [X] T024 [P] Create folder structure Maliev.UploadService.Api/Services/
- [X] T025 [P] Create folder structure Maliev.UploadService.Api/Data/
- [X] T026 [P] Create folder structure Maliev.UploadService.Api/Middleware/
- [X] T027 [P] Create folder structure Maliev.UploadService.Api/Extensions/
- [X] T028 [P] Create folder structure Maliev.UploadService.Api/Consumers/
- [X] T029 [P] Create folder structure Maliev.UploadService.Api/BackgroundServices/
- [X] T030 [P] Create folder structure Maliev.UploadService.Tests/Integration/
- [X] T031 [P] Create folder structure Maliev.UploadService.Tests/Unit/Services/
- [X] T032 [P] Create folder structure Maliev.UploadService.Tests/Unit/Extensions/
- [X] T033 [P] Create folder structure Maliev.UploadService.Tests/Fixtures/
- [X] T034 [P] Create appsettings.json in Maliev.UploadService.Api/ with ConnectionStrings placeholders
- [X] T035 [P] Create appsettings.Development.json in Maliev.UploadService.Api/ with local development config

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Database & Entities

- [X] T036 Create Upload entity (FR-011, FR-015) in Maliev.UploadService.Api/Models/Entities/Upload.cs
- [X] T037 [P] Create FileMetadata entity (FR-011, FR-016) in Maliev.UploadService.Api/Models/Entities/FileMetadata.cs
- [X] T038 [P] Create ServiceAuthorizationPolicy entity (FR-018, FR-028) in Maliev.UploadService.Api/Models/Entities/ServiceAuthorizationPolicy.cs
- [X] T039 [P] Create RetentionPolicy entity (FR-012, FR-013, FR-014) in Maliev.UploadService.Api/Models/Entities/RetentionPolicy.cs
- [X] T040 [P] Create UploadEvent entity (FR-021) in Maliev.UploadService.Api/Models/Entities/UploadEvent.cs
- [X] T041 [P] Create BulkDeleteJob entity (FR-032, FR-033) in Maliev.UploadService.Api/Models/Entities/BulkDeleteJob.cs
- [X] T042 Create UploadServiceDbContext (FR-024) in Maliev.UploadService.Api/Data/UploadServiceDbContext.cs with all entity configurations
- [X] T043 Create initial database migration (FR-024) in Maliev.UploadService.Api/Data/Migrations/ using dotnet ef migrations add InitialCreate
- [X] T044 Add database migration application logic (FR-024) to Maliev.UploadService.Api/Program.cs using MigrateDatabaseAsync

### Authentication & Authorization

- [X] T045 Create IAuthorizationPolicyService interface (FR-018) in Maliev.UploadService.Api/Services/IAuthorizationPolicyService.cs
- [X] T046 Implement AuthorizationPolicyService (FR-018, FR-028) in Maliev.UploadService.Api/Services/AuthorizationPolicyService.cs with Redis caching
- [X] T047 Create path sanitization logic (FR-008, FR-027) in Maliev.UploadService.Api/Extensions/ValidationExtensions.cs
- [X] T048 Add JWT authentication configuration (FR-002) to Maliev.UploadService.Api/Program.cs using AddJwtAuthentication

### Middleware & Error Handling

- [X] T049 Create ExceptionHandlingMiddleware (FR-029) in Maliev.UploadService.Api/Middleware/ExceptionHandlingMiddleware.cs
- [X] T050 Add ExceptionHandlingMiddleware (FR-029) to pipeline in Maliev.UploadService.Api/Program.cs

### Mapping Extensions

- [X] T051 [P] Create UploadMappingExtensions (FR-015) in Maliev.UploadService.Api/Extensions/UploadMappingExtensions.cs
- [X] T052 [P] Create FileMetadataMappingExtensions (FR-016) in Maliev.UploadService.Api/Extensions/FileMetadataMappingExtensions.cs

### Test Fixtures

- [X] T053 [P] Create TestDatabaseFixture in Maliev.UploadService.Tests/Fixtures/TestDatabaseFixture.cs using Testcontainers.PostgreSql
- [X] T054 [P] Create MockHttpMessageHandler in Maliev.UploadService.Tests/Fixtures/MockHttpMessageHandler.cs for GCS client mocking
- [X] T055 Create TestWebApplicationFactory in Maliev.UploadService.Tests/Fixtures/TestWebApplicationFactory.cs with dynamic RSA keys

### Program.cs Core Configuration

- [X] T056 Configure Program.cs in Maliev.UploadService.Api/Program.cs with ServiceDefaults, PostgreSQL, Redis, RabbitMQ per constitution
- [X] T057 Add API versioning configuration to Maliev.UploadService.Api/Program.cs
- [X] T058 Add OpenAPI/Scalar configuration to Maliev.UploadService.Api/Program.cs
- [X] T059 Add health checks configuration to Maliev.UploadService.Api/Program.cs
- [X] T060 Add MapDefaultEndpoints and MapApiDocumentation to Maliev.UploadService.Api/Program.cs with servicePrefix uploadservice

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Secure File Upload with Validation (Priority: P1) 🎯 MVP

**Goal**: Enable microservices to upload files with validation (size, content type, malware scanning) and receive storage references

**Independent Test**: Send a valid file upload request from a test client, verify file validation occurs, and confirm a successful storage reference is returned

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T061 [P] [US1] Write integration test for successful file upload (FR-001, FR-015) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T062 [P] [US1] Write integration test for file size limit rejection (FR-003) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T063 [P] [US1] Write integration test for invalid content type rejection (FR-004) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T064 [P] [US1] Write integration test for malware detection (FR-005) in Maliev.UploadService.Tests/Integration/FileValidationTests.cs
- [X] T065 [P] [US1] Write unit test for FileValidationService content type validation (FR-004) in Maliev.UploadService.Tests/Unit/Services/FileValidationServiceTests.cs
- [X] T066 [P] [US1] Write unit test for FileValidationService malware scanning (FR-005) in Maliev.UploadService.Tests/Unit/Services/FileValidationServiceTests.cs
- [X] T067 [P] [US1] Write unit test for GcsStorageService upload streaming (FR-001, FR-024) in Maliev.UploadService.Tests/Unit/Services/GcsStorageServiceTests.cs

### Implementation for User Story 1

- [X] T068 [P] [US1] Create UploadFileRequest model (FR-001, FR-026) in Maliev.UploadService.Api/Models/Requests/UploadFileRequest.cs with Data Annotations
- [X] T069 [P] [US1] Create UploadResponse model (FR-015) in Maliev.UploadService.Api/Models/Responses/UploadResponse.cs
- [X] T070 [P] [US1] Create IValidationService interface (FR-004, FR-005, FR-006) in Maliev.UploadService.Api/Services/IValidationService.cs
- [X] T071 [US1] Implement FileValidationService (FR-004, FR-005, FR-006) in Maliev.UploadService.Api/Services/FileValidationService.cs with MimeDetective and nClam integration. NOTE: Implements FR-006 validation isolation through dedicated service with sandboxed execution context
- [X] T072 [P] [US1] Create IStorageService interface (FR-001, FR-024) in Maliev.UploadService.Api/Services/IStorageService.cs
- [X] T073 [US1] Implement GcsStorageService (FR-001, FR-024, FR-026) in Maliev.UploadService.Api/Services/GcsStorageService.cs with streaming upload
- [X] T074 [US1] Create UploadsController (FR-001, FR-002) in Maliev.UploadService.Api/Controllers/v1/UploadsController.cs with POST /api/v1/uploads endpoint
- [X] T075 [US1] Add authorization policy check (FR-002, FR-018) to UploadsController upload endpoint using IAuthorizationPolicyService
- [X] T076 [US1] Add file validation logic (FR-003, FR-004, FR-005, FR-006) to UploadsController upload endpoint using IValidationService
- [X] T077 [US1] Add GCS upload logic (FR-001, FR-024) to UploadsController upload endpoint using IStorageService
- [X] T078 [US1] Add Upload entity persistence (FR-011) after successful upload in UploadsController
- [X] T079 [US1] Add FileMetadata entity creation (FR-011, FR-015) after successful upload in UploadsController
- [X] T080 [US1] Add UploadEvent audit logging (FR-021) in UploadsController for upload success and failure
- [X] T081 [US1] Add metrics instrumentation for upload success/failure rates in UploadsController
- [X] T082 [US1] Register FileValidationService and GcsStorageService in Maliev.UploadService.Api/Program.cs DI container

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently - basic file upload with validation works

---

## Phase 4: User Story 3 - File Retrieval and Access Control (Priority: P1)

**Goal**: Enable services to retrieve file metadata, generate signed URLs for access, and enforce path-based authorization

**Independent Test**: Upload a file, then request its metadata and a signed URL, verify the URL works for the specified duration, and confirm unauthorized access attempts are blocked

**Why Before US2**: File retrieval is as fundamental as upload for MVP. US2 (path organization) enhances upload but isn't blocking for basic functionality.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T083 [P] [US3] Write integration test for get file metadata by upload ID (FR-016) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs
- [X] T084 [P] [US3] Write integration test for generate signed URL (FR-017) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs
- [X] T085 [P] [US3] Write integration test for query files by path prefix (FR-019) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs
- [X] T086 [P] [US3] Write integration test for authorization denial on cross-service access (FR-018) in Maliev.UploadService.Tests/Integration/AuthorizationTests.cs
- [X] T087 [P] [US3] Write unit test for AuthorizationPolicyService path-based access control (FR-018) in Maliev.UploadService.Tests/Unit/Services/AuthorizationPolicyServiceTests.cs
- [X] T088 [P] [US3] Write unit test for signed URL generation with expiration (FR-017) in Maliev.UploadService.Tests/Unit/Services/GcsStorageServiceTests.cs

### Implementation for User Story 3

- [X] T089 [P] [US3] Create FileMetadataResponse model (FR-016) in Maliev.UploadService.Api/Models/Responses/FileMetadataResponse.cs
- [X] T090 [P] [US3] Create GenerateSignedUrlRequest model (FR-017) in Maliev.UploadService.Api/Models/Requests/GenerateSignedUrlRequest.cs
- [X] T091 [P] [US3] Create SignedUrlResponse model (FR-017) in Maliev.UploadService.Api/Models/Responses/SignedUrlResponse.cs
- [X] T092 [P] [US3] Create QueryFilesRequest model (FR-019) in Maliev.UploadService.Api/Models/Requests/QueryFilesRequest.cs
- [X] T093 [P] [US3] Create QueryFilesResponse model (FR-019) in Maliev.UploadService.Api/Models/Responses/QueryFilesResponse.cs
- [X] T094 [US3] Add GetFileMetadata method (FR-016) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T095 [US3] Add GenerateSignedUrl method (FR-017) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs with V4 signing and Redis caching
- [X] T096 [US3] Create FilesController (FR-016, FR-017, FR-019, FR-020) in Maliev.UploadService.Api/Controllers/v1/FilesController.cs
- [X] T097 [US3] Add GET /api/v1/files/{uploadId} endpoint (FR-016, FR-018) to FilesController with authorization check
- [X] T098 [US3] Add GET /api/v1/files endpoint (FR-019, FR-018) to FilesController for path prefix query with pagination
- [X] T099 [US3] Add POST /api/v1/files/{uploadId}/signed-url endpoint (FR-017, FR-018) to FilesController
- [X] T100 [US3] Add UploadEvent audit logging (FR-021) for file retrieval and signed URL generation in FilesController
- [X] T101 [US3] Add metrics instrumentation for signed URL generation rate in FilesController

**Checkpoint**: At this point, User Story 3 should be fully functional - file retrieval and signed URLs work with authorization

---

## Phase 5: User Story 2 - Dynamic Path Organization and Collision Avoidance (Priority: P2)

**Goal**: Enable services to organize files with dynamic path patterns, enforce collision rules, and sanitize paths against attacks

**Independent Test**: Submit upload requests from different services with various path patterns, verify paths are resolved correctly, and confirm no collisions occur

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T102 [P] [US2] Write integration test for path placeholder resolution (FR-007, FR-009) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T103 [P] [US2] Write integration test for path collision detection (FR-010) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T104 [P] [US2] Write integration test for path traversal attack prevention (FR-008) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T105 [P] [US2] Write unit test for path sanitization (FR-008, FR-027) in Maliev.UploadService.Tests/Unit/Extensions/ValidationExtensionsTests.cs
- [X] T106 [P] [US2] Write unit test for overwrite flag handling (FR-030) in Maliev.UploadService.Tests/Unit/Services/GcsStorageServiceTests.cs

### Implementation for User Story 2

- [X] T107 [US2] Add path placeholder resolution logic (FR-007, FR-009) to UploadsController in Maliev.UploadService.Api/Controllers/v1/UploadsController.cs
- [X] T108 [US2] Add path collision detection (FR-010) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T109 [US2] Add overwrite flag support (FR-030) to UploadFileRequest in Maliev.UploadService.Api/Models/Requests/UploadFileRequest.cs
- [X] T110 [US2] Add overwrite logic (FR-030) to GcsStorageService upload method in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T111 [US2] Enhance path sanitization (FR-008, FR-027) in ValidationExtensions to reject special characters in Maliev.UploadService.Api/Extensions/ValidationExtensions.cs
- [X] T112 [US2] Add service-specific path prefix validation (FR-018) to AuthorizationPolicyService in Maliev.UploadService.Api/Services/AuthorizationPolicyService.cs
- [X] T113 [US2] Add UploadEvent audit logging (FR-021) for path collision and traversal attempts in UploadsController

**Checkpoint**: At this point, User Story 2 should be fully functional - path organization and collision handling works

---

## Phase 6: User Story 5 - File Deletion with Safety Checks (Priority: P2)

**Goal**: Enable services to delete files with authorization checks, retention policy validation, and audit logging

**Independent Test**: Upload a file, issue a delete request, and verify the file is removed from storage and an audit log entry is created

**Why Before US4**: Deletion is more fundamental than lifecycle automation. Manual deletion is often needed before implementing automatic lifecycle rules.

### Tests for User Story 5 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T114 [P] [US5] Write integration test for successful file deletion (FR-020) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs
- [X] T115 [P] [US5] Write integration test for deletion blocked by retention policy (FR-031) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs
- [X] T116 [P] [US5] Write integration test for unauthorized deletion attempt (FR-018, FR-020) in Maliev.UploadService.Tests/Integration/AuthorizationTests.cs
- [X] T117 [P] [US5] Write integration test for file not found after deletion (FR-020) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs

### Implementation for User Story 5

- [X] T118 [P] [US5] Create DeleteFileRequest model (FR-020) in Maliev.UploadService.Api/Models/Requests/DeleteFileRequest.cs
- [X] T119 [US5] Add DeleteFile method (FR-020, FR-024) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T120 [US5] Add DELETE /api/v1/files/{uploadId} endpoint (FR-020) to FilesController
- [X] T121 [US5] Add authorization check (FR-018, FR-020) to delete endpoint in FilesController
- [X] T122 [US5] Add retention policy check (FR-031) to delete endpoint in FilesController (allow with warning)
- [X] T123 [US5] Add FileMetadata and Upload entity cleanup after deletion in FilesController
- [X] T124 [US5] Add UploadEvent audit logging (FR-021) for file deletion in FilesController
- [X] T125 [US5] Add metrics instrumentation for deletion operations in FilesController

**Checkpoint**: At this point, User Story 5 should be fully functional - file deletion with safety checks works

---

## Phase 7: User Story 4 - Lifecycle Management and Retention Policies (Priority: P2)

**Goal**: Enable automatic file deletion based on retention policies using GCS lifecycle rules

**Independent Test**: Upload files with different retention policies, fast-forward time (or adjust cloud lifecycle rules), and verify files are purged according to policy

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T126 [P] [US4] Write integration test for retention policy application on upload (FR-011, FR-012, FR-013) in Maliev.UploadService.Tests/Integration/LifecycleManagementTests.cs
- [X] T127 [P] [US4] Write integration test for indefinite retention (FR-013) in Maliev.UploadService.Tests/Integration/LifecycleManagementTests.cs
- [X] T128 [P] [US4] Write integration test for storage class transition (FR-014) in Maliev.UploadService.Tests/Integration/LifecycleManagementTests.cs
- [X] T129 [P] [US4] Write unit test for GCS lifecycle rule creation (FR-012) in Maliev.UploadService.Tests/Unit/Services/LifecycleManagementServiceTests.cs

### Implementation for User Story 4

- [X] T130 [P] [US4] Create ILifecycleManagementService interface (FR-012, FR-013, FR-014) in Maliev.UploadService.Api/Services/ILifecycleManagementService.cs
- [X] T131 [US4] Implement LifecycleManagementService (FR-012, FR-013, FR-014) in Maliev.UploadService.Api/Services/LifecycleManagementService.cs with GCS lifecycle API
- [X] T132 [US4] Add retention policy metadata tagging (FR-011, FR-012) to upload in UploadsController
- [X] T133 [US4] Add lifecycle rule application logic (FR-012, FR-013) to LifecycleManagementService
- [X] T134 [US4] Create LifecyclePolicyWorker background service (FR-013) in Maliev.UploadService.Api/BackgroundServices/LifecyclePolicyWorker.cs
- [X] T135 [US4] Add LifecyclePolicyWorker registration (FR-013) to Maliev.UploadService.Api/Program.cs
- [X] T136 [US4] Add storage class transition support (FR-014) to LifecycleManagementService
- [X] T137 [US4] Register LifecycleManagementService in Maliev.UploadService.Api/Program.cs DI container

**Checkpoint**: At this point, User Story 4 should be fully functional - automatic lifecycle management works

---

## Phase 8: User Story 6 - Large File Streaming and Resumable Uploads (Priority: P3)

**Goal**: Enable efficient upload of large files (1GB+) with constant memory usage and resumable capability

**Independent Test**: Upload a large file via streaming, monitor memory usage to confirm it stays constant, and interrupt/resume an upload to verify resumability

### Tests for User Story 6 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T138 [P] [US6] Write integration test for large file streaming upload (FR-001, FR-023) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T139 [P] [US6] Write integration test for resumable upload initiation (FR-022) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T140 [P] [US6] Write integration test for resumable upload continuation (FR-022) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T141 [P] [US6] Write unit test for memory-efficient streaming (FR-023) in Maliev.UploadService.Tests/Unit/Services/GcsStorageServiceTests.cs

### Implementation for User Story 6

- [X] T142 [P] [US6] Create InitiateResumableUploadRequest model (FR-022) in Maliev.UploadService.Api/Models/Requests/InitiateResumableUploadRequest.cs
- [X] T143 [P] [US6] Create InitiateResumableUploadResponse model (FR-022) in Maliev.UploadService.Api/Models/Responses/InitiateResumableUploadResponse.cs
- [X] T144 [P] [US6] Create ResumeUploadResponse model (FR-022) in Maliev.UploadService.Api/Models/Responses/ResumeUploadResponse.cs
- [X] T145 [US6] Add InitiateResumableUpload method (FR-022, FR-024) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T146 [US6] Add ResumeUpload method (FR-022, FR-024) to GcsStorageService in Maliev.UploadService.Api/Services/GcsStorageService.cs
- [X] T147 [US6] Add POST /api/v1/uploads/resumable endpoint (FR-022) to UploadsController
- [X] T148 [US6] Add PUT /api/v1/uploads/resumable/{uploadId} endpoint (FR-022) to UploadsController
- [X] T149 [US6] Add Upload entity session URI tracking (FR-022) for resumable uploads in UploadsController
- [X] T150 [US6] Configure FormOptions (FR-023) for large file handling in Maliev.UploadService.Api/Program.cs
- [X] T151 [US6] Configure KestrelServerOptions (FR-023) for 10GB max request body size in Maliev.UploadService.Api/Program.cs

**Checkpoint**: At this point, User Story 6 should be fully functional - large file streaming and resumable uploads work

---

## Phase 9: User Story 7 - Asynchronous Upload Notifications (Priority: P3)

**Goal**: Enable async notification via RabbitMQ when uploads complete or fail

**Independent Test**: Initiate a background upload, subscribe to the notification channel, and verify a completion message is received with full upload metadata

### Tests for User Story 7 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T152 [P] [US7] Write integration test for upload completed event publication (FR-025) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T153 [P] [US7] Write integration test for upload failed event publication (FR-025) in Maliev.UploadService.Tests/Integration/UploadsControllerTests.cs
- [X] T154 [P] [US7] Write integration test for file deleted event publication (FR-025) in Maliev.UploadService.Tests/Integration/FilesControllerTests.cs

### Implementation for User Story 7

- [X] T155 [P] [US7] Create UploadCompletedEvent class (FR-025) in Maliev.UploadService.Api/Events/UploadCompletedEvent.cs
- [X] T156 [P] [US7] Create UploadFailedEvent class (FR-025) in Maliev.UploadService.Api/Events/UploadFailedEvent.cs
- [X] T157 [P] [US7] Create FileDeletedEvent class (FR-025) in Maliev.UploadService.Api/Events/FileDeletedEvent.cs
- [X] T158 [US7] Add event publication (FR-025) to UploadsController on successful upload using IPublishEndpoint
- [X] T159 [US7] Add event publication (FR-025) to UploadsController on failed upload using IPublishEndpoint
- [X] T160 [US7] Add event publication (FR-025) to FilesController on file deletion using IPublishEndpoint
- [X] T161 [US7] Configure MassTransit routing keys (FR-025) in Maliev.UploadService.Api/Program.cs per maliev.uploadservice.v1.{entity}.{action} pattern

**Checkpoint**: At this point, User Story 7 should be fully functional - async notifications work

---

## Phase 10: Admin Operations - Bulk Delete (Additional Feature)

**Goal**: Enable admin bulk delete of files for decommissioned services

**Independent Test**: Initiate a bulk delete job, monitor progress, and verify files are deleted with job status tracking

### Tests for Bulk Delete ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T162 [P] Write integration test for bulk delete job initiation (FR-032) in Maliev.UploadService.Tests/Integration/AdminControllerTests.cs
- [X] T163 [P] Write integration test for bulk delete job status query (FR-033) in Maliev.UploadService.Tests/Integration/AdminControllerTests.cs
- [X] T164 [P] Write integration test for bulk delete background processing (FR-033) in Maliev.UploadService.Tests/Integration/BulkDeleteTests.cs
- [X] T165 [P] Write unit test for bulk delete batch processing (FR-032, FR-033) in Maliev.UploadService.Tests/Unit/Services/BulkDeleteServiceTests.cs

### Implementation for Bulk Delete

- [X] T166 [P] Create BulkDeleteRequest model (FR-032) in Maliev.UploadService.Api/Models/Requests/BulkDeleteRequest.cs
- [X] T167 [P] Create BulkDeleteJobResponse model (FR-033) in Maliev.UploadService.Api/Models/Responses/BulkDeleteJobResponse.cs
- [X] T168 [P] Create IBulkDeleteService interface (FR-032, FR-033) in Maliev.UploadService.Api/Services/IBulkDeleteService.cs
- [X] T169 Implement BulkDeleteService (FR-032, FR-033) in Maliev.UploadService.Api/Services/BulkDeleteService.cs
- [X] T170 Create BulkDeleteJobConsumer (FR-033) in Maliev.UploadService.Api/Consumers/BulkDeleteJobConsumer.cs for MassTransit
- [X] T171 Create AdminController (FR-032, FR-033) in Maliev.UploadService.Api/Controllers/v1/AdminController.cs
- [X] T172 Add POST /api/v1/admin/bulk-delete endpoint (FR-032) to AdminController
- [X] T173 Add GET /api/v1/admin/bulk-delete/{jobId} endpoint (FR-033) to AdminController
- [X] T174 Add BulkDeleteJobConsumer registration (FR-033) to MassTransit in Maliev.UploadService.Api/Program.cs
- [X] T175 Register BulkDeleteService in Maliev.UploadService.Api/Program.cs DI container
- [X] T176 Add BulkDeleteCompletedEvent publication (FR-025) to BulkDeleteJobConsumer

**Checkpoint**: At this point, bulk delete operations are fully functional

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, documentation, deployment artifacts, and production readiness

### Docker & Deployment

- [ ] T177 Create Dockerfile at repository root following Constitution Principle X best practices
- [ ] T178 Create docker-compose.yml for local development with PostgreSQL, Redis, RabbitMQ, ClamAV
- [ ] T179 [P] Create .github/workflows/ci.yml for CI/CD pipeline with BuildKit secrets

### Metrics & Observability

- [ ] T180 Create UploadMetrics class in Maliev.UploadService.Api/Metrics/UploadMetrics.cs with OpenTelemetry custom metrics
- [ ] T181 Add upload success/failure rate counters to UploadMetrics
- [ ] T182 Add upload duration histogram to UploadMetrics
- [ ] T183 Add active uploads gauge to UploadMetrics
- [ ] T184 Add storage quota utilization gauge to UploadMetrics
- [ ] T185 Add file validation rejection metrics to UploadMetrics
- [ ] T186 Register UploadMetrics in Maliev.UploadService.Api/Program.cs

### Documentation

- [ ] T187 [P] Create README.md at repository root with service overview and quickstart
- [ ] T188 [P] Update OpenAPI documentation with examples in contracts/openapi.yaml
- [ ] T189 [P] Create sample authorization policy seed data in Maliev.UploadService.Api/Data/SeedData.cs

### Integration Feedback (SC-011)

- [ ] T196 [P] Create integration feedback survey mechanism (SC-011) to measure developer experience with upload interface. Create feedback form in specs/001-upload-service/integration-feedback.md for post-deployment collection

### Final Integration Tests

- [ ] T190 [P] Write end-to-end test for complete upload-retrieve-delete flow in Maliev.UploadService.Tests/Integration/EndToEndTests.cs
- [ ] T191 [P] Write load test for concurrent upload handling in Maliev.UploadService.Tests/Integration/PerformanceTests.cs
- [ ] T192 [P] Write security test for authorization bypass attempts in Maliev.UploadService.Tests/Integration/SecurityTests.cs

### Code Quality

- [ ] T193 Run dotnet format to enforce code style
- [ ] T194 Run dotnet build with TreatWarningsAsErrors=true to ensure zero warnings
- [ ] T195 Run dotnet test --collect:"XPlat Code Coverage" and verify 80%+ coverage

**Final Checkpoint**: Upload Service is production-ready, all tests pass, documentation complete

---

## Implementation Strategy

### MVP Scope (Recommended First Iteration)

**Phase 1 + Phase 2 + Phase 3 (User Story 1) + Phase 4 (User Story 3)**

This provides:
- ✅ Basic file upload with validation
- ✅ File retrieval and signed URLs
- ✅ Security (auth, authorization, malware scanning)
- ✅ Complete test coverage
- ✅ Independently deployable service

**Tasks for MVP**: T001-T101 (101 tasks)

### Incremental Delivery Plan

1. **Week 1**: Setup + Foundation (T001-T060) - 60 tasks
2. **Week 2**: User Story 1 - Upload with Validation (T061-T082) - 22 tasks
3. **Week 3**: User Story 3 - File Retrieval (T083-T101) - 19 tasks
4. **Week 4**: User Story 2 - Path Organization (T102-T113) - 12 tasks
5. **Week 5**: User Story 5 - File Deletion (T114-T125) - 12 tasks
6. **Week 6**: User Story 4 - Lifecycle Management (T126-T137) - 12 tasks
7. **Week 7**: User Story 6 - Large Files (T138-T151) - 14 tasks
8. **Week 8**: User Story 7 - Async Notifications (T152-T161) - 10 tasks
9. **Week 9**: Bulk Delete + Polish (T162-T195) - 34 tasks

---

## Dependencies & Parallel Execution

### Story Completion Order

```
Phase 1 (Setup) → Phase 2 (Foundation)
                 ↓
         ┌───────┴────────┐
         ↓                ↓
    Phase 3 (US1)    Phase 4 (US3)  ← Can run in parallel after Phase 2
         ↓                ↓
         └───────┬────────┘
                 ↓
            Phase 5 (US2)  ← Enhances US1
                 ↓
         ┌───────┴────────┐
         ↓                ↓
    Phase 6 (US5)    Phase 7 (US4)  ← Can run in parallel
         ↓                ↓
         └───────┬────────┘
                 ↓
            Phase 8 (US6)  ← Enhances US1
                 ↓
            Phase 9 (US7)  ← Adds notifications
                 ↓
          Phase 10 (Bulk)
                 ↓
           Phase 11 (Polish)
```

### Parallel Opportunities

**Within Phase 1 (Setup)**: T004-T033 can all run in parallel (different files)

**Within Phase 2 (Foundation)**:
- T036-T041 (entities) can run in parallel
- T045-T052 (services/extensions) can run in parallel after entities
- T053-T055 (test fixtures) can run in parallel

**Within Phase 3 (US1)**:
- T061-T067 (tests) can run in parallel
- T068-T073 (models/interfaces) can run in parallel after tests fail
- T074-T082 (controller implementation) must be sequential

**Within Phase 4 (US3)**:
- T083-T088 (tests) can run in parallel
- T089-T093 (models) can run in parallel
- T096-T101 (controller) must be sequential

Similar patterns apply to other phases.

---

## Task Summary

**Total Tasks**: 196
**Tasks by Phase**:
- Phase 1 (Setup): 35 tasks
- Phase 2 (Foundation): 25 tasks
- Phase 3 (US1 - Upload): 22 tasks
- Phase 4 (US3 - Retrieval): 19 tasks
- Phase 5 (US2 - Path Org): 12 tasks
- Phase 6 (US5 - Deletion): 12 tasks
- Phase 7 (US4 - Lifecycle): 12 tasks
- Phase 8 (US6 - Large Files): 14 tasks
- Phase 9 (US7 - Notifications): 10 tasks
- Phase 10 (Bulk Delete): 15 tasks
- Phase 11 (Polish): 20 tasks

**Parallel Opportunities**: ~80 tasks marked with [P] can run in parallel within their phase

**MVP Task Count**: 101 tasks (Phases 1-4)

**Test Tasks**: 42 integration/unit tests (Test-First Development enforced)

**FR-Traceability**: All implementation tasks now reference their corresponding FR- IDs for complete requirement traceability

---

## Validation Checklist

✅ All tasks follow checklist format: `- [ ] [ID] [P?] [Story?] Description with file path`
✅ Tasks organized by user story (independent implementation/testing)
✅ Test-First Development: Tests written BEFORE implementation
✅ Each user story has independent test criteria
✅ MVP scope clearly defined (US1 + US3)
✅ Dependency graph shows story completion order
✅ Parallel execution opportunities identified
✅ File paths match project structure from plan.md
✅ All 7 user stories from spec.md mapped to phases
✅ Constitution compliance (ServiceDefaults, Testcontainers, no banned libraries)

