# Tasks: Permission-Based Authorization Migration

**Input**: Design documents from `specs/002-iam-integration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Integration tests are included to verify the migration and authorization logic as per the Test-First principle in the Constitution. Performance tests are mandatory for latency verification.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- File paths use the repository root structure

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic configuration for IAM integration.

- [x] T001 Configure IAM settings and 5-min Redis cache TTL in `Maliev.UploadService.Api/appsettings.json` (Constitution V)
- [x] T002 Update NuGet references to include `Maliev.Aspire.ServiceDefaults` in `Maliev.UploadService.Api/Maliev.UploadService.Api.csproj`
- [x] T003 [P] Configure LogLevel and Business Metrics instrumentation in `Maliev.UploadService.Api/appsettings.json` (Constitution V, XII)
- [x] T004 [P] Verify `.github/CODEOWNERS` contains `* @MALIEV-Co-Ltd/core-developers` (Constitution IX)
- [x] T005 [P] Enable `TreatWarningsAsErrors` in all `.csproj` files to enforce zero warnings (Constitution VIII)
- [x] T006 [P] Verify `nuget.config` has correct GitHub Packages source and placeholders (Constitution XIII)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure for resource-scoped permission definitions and IAM service interaction.

**⚠️ CRITICAL**: These tasks must be completed before any user story work can begin.

- [x] T007 [P] Create permission constants in `{service}.{resource}.{action}` format in `Maliev.UploadService.Api/Services/Auth/UploadPermissions.cs`
- [x] T008 [P] Create predefined role definitions in `roles.upload.{role-name}` format in `Maliev.UploadService.Api/Services/Auth/UploadPredefinedRoles.cs`
- [x] T009 [P] Update `IIAMServiceClient` and implementation to support `CheckPermissionAsync` with resource paths in `Maliev.UploadService.Api/Services/Auth/`
- [x] T010 Implement Business Metrics for authorization events including `service_name`, `version`, `region`, `environment` tags in `Maliev.UploadService.Api/Metrics/UploadMetrics.cs` (Constitution XII)
- [x] T011 Update `UploadIAMRegistrationService` for resource-scoped permission registration in `Maliev.UploadService.Api/Services/Auth/UploadIAMRegistrationService.cs`
- [x] T012 Update `IAuthorizationPolicyService` and implementation with "IAM Overrides Legacy" fallback and 5-min caching logic.

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Resource-Scoped File Upload (Priority: P1) 🎯 MVP

**Goal**: Enable services to upload files using resource-scoped IAM permissions and migrate existing policies.

**Independent Test**: Verify service with `upload.files.upload` on `folders/invoices/**` can upload to that path but is blocked from others.

### Tests for User Story 1

- [x] T013 [P] [US1] Create integration test for resource-scoped upload in `Maliev.UploadService.Tests/Integration/IAMResourceScopedTests.cs`
- [x] T014 [US1] Performance test for authorization latency (< 10ms cached) in `Maliev.UploadService.Tests/Performance/AuthPerformanceTests.cs` (SC-005)

### Implementation for User Story 1

- [x] T015 [US1] Update `UploadsController` to use `[RequirePermission]` with `ResourcePathTemplate` for upload endpoints in `Maliev.UploadService.Api/Controllers/v1/UploadsController.cs`
- [x] T016 [US1] Implement `UploadIAMMigrationService` to map legacy rules to resource-scoped bindings in `Maliev.UploadService.Api/Services/Auth/UploadIAMMigrationService.cs`
- [x] T017 [US1] Update OpenAPI/Scalar documentation to reflect required IAM permissions for upload endpoints (Constitution II)

**Checkpoint**: User Story 1 (MVP) is fully functional and testable independently.

---

## Phase 4: User Story 2 - Administrative Management (Priority: P2)

**Goal**: Implement granular admin permissions for viewing metrics and performing bulk deletions.

### Tests for User Story 2

- [x] T018 [P] [US2] Create integration test for admin granular permissions in `Maliev.UploadService.Tests/Integration/IAMAdminTests.cs`

### Implementation for User Story 2

- [x] T019 [US2] Update `AdminController` to use `[RequirePermission]` for metrics and bulk delete endpoints in `Maliev.UploadService.Api/Controllers/v1/AdminController.cs`
- [x] T020 [US2] Update OpenAPI/Scalar documentation for Admin endpoints (Constitution II)

**Checkpoint**: Administrative management is secured via IAM permissions.

---

## Phase 5: User Story 3 - User File Management (Priority: P3)

**Goal**: Implement owner-only access for end-users based on resource-scoped bindings (e.g., `users/{userId}/**`).

### Tests for User Story 3

- [x] T021 [P] [US3] Create integration test for user resource-scoped access in `Maliev.UploadService.Tests/Integration/IAMUserTests.cs`

### Implementation for User Story 3

- [x] T022 [US3] Update `FilesController` to use `[RequirePermission]` with dynamic resource path resolution in `Maliev.UploadService.Api/Controllers/v1/FilesController.cs`

**Checkpoint**: All user stories are independently functional and secured via resource-scoped IAM.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, decommissioning of legacy logic, and documentation.

- [x] T023 Implement automated legacy policy cleanup flag logic in `Maliev.UploadService.Api/Services/Auth/UploadIAMMigrationService.cs`
- [x] T024 [P] Update `README.md` and `quickstart.md` with final authorization details and migration steps
- [ ] T025 [P] Remove `ServiceAuthorizationPolicy` entity and related DB context code in `Maliev.UploadService.Data/UploadDbContext.cs`
- [ ] T026 Create final migration to drop `ServiceAuthorizationPolicies` table in `Maliev.UploadService.Data/Migrations/`
- [x] T027 [P] Final validation: Run all existing and new tests to ensure zero regressions (SC-004)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on T001-T006. Blocks all user stories.
- **User Stories (Phases 3-5)**: All depend on Foundational (Phase 2) completion.
- **Polish (Phase 6)**: Depends on successful verification of all user stories.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases.
2. Complete User Story 1 (Resource-scoped Implementation + Migration script).
3. Validate with `IAMResourceScopedTests.cs` and `AuthPerformanceTests.cs`.

### Notes

- All tasks use the standardized `[RequirePermission]` attribute from `ServiceDefaults`.
- Performance targets (< 10ms cached) are strictly enforced via tests.
- Business metrics and OpenAPI documentation are mandatory for constitutional compliance.
