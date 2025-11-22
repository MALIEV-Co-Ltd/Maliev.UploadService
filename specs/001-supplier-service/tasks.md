# Tasks: Supplier Service WebAPI

**Input**: Design documents from `/specs/001-supplier-service/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi-v1.yaml

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US8)
- All file paths are relative to repository root

## Path Conventions

This is a MALIEV 3-project microservice structure:
- **API**: `Maliev.SupplierService.Api/`
- **Data**: `Maliev.SupplierService.Data/`
- **Tests**: `Maliev.SupplierService.Tests/`

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create solution structure and configure basic dependencies

- [X] T001 Create solution file `Maliev.SupplierService.sln` at repository root
- [X] T002 [P] Create Api project `Maliev.SupplierService.Api/Maliev.SupplierService.Api.csproj` with .NET 10 and all NuGet dependencies per plan.md
- [X] T003 [P] Create Data project `Maliev.SupplierService.Data/Maliev.SupplierService.Data.csproj` with EF Core and Npgsql dependencies
- [X] T004 [P] Create Tests project `Maliev.SupplierService.Tests/Maliev.SupplierService.Tests.csproj` with xUnit, Testcontainers, FluentAssertions
- [X] T005 Add project references: Api references Data; Tests references both Api and Data
- [X] T006 [P] Create `nuget.config` at repository root with GitHub Packages source per constitution XIII
- [X] T007 [P] Create `.gitignore` with standard .NET ignores at repository root
- [X] T008 [P] Create `.dockerignore` excluding Tests, specs, .github per plan.md

**Checkpoint**: Solution builds with `dotnet build Maliev.SupplierService.sln`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

### Data Layer Foundation

- [X] T009 [P] Create enum `Maliev.SupplierService.Data/Enums/SupplierStatus.cs` (PendingApproval, Active, Suspended, Inactive)
- [X] T010 [P] Create enum `Maliev.SupplierService.Data/Enums/OnboardingStage.cs` (PendingApproval, DocumentationReview, FinalApproval, Active)
- [X] T011 [P] Create enum `Maliev.SupplierService.Data/Enums/CertificationType.cs` (TaxForm, BusinessLicense, RegulatoryCompliance, QualityCertification, InsuranceCertificate, Other)
- [X] T012 [P] Create enum `Maliev.SupplierService.Data/Enums/PerformanceRatingCategory.cs` (Quality, Delivery, Communication, Pricing, Overall)
- [X] T013 Create base entity `Maliev.SupplierService.Data/Entities/Supplier.cs` with all properties per data-model.md including RowVersion
- [X] T014 [P] Create entity `Maliev.SupplierService.Data/Entities/SupplierContact.cs` with FK to Supplier
- [X] T015 [P] Create entity `Maliev.SupplierService.Data/Entities/MaterialCategory.cs` with Name, Description, IsActive
- [X] T016 [P] Create entity `Maliev.SupplierService.Data/Entities/SupplierCapability.cs` with FK to Supplier
- [X] T017 [P] Create entity `Maliev.SupplierService.Data/Entities/SupplierCertification.cs` with FK to Supplier per data-model.md
- [X] T018 [P] Create entity `Maliev.SupplierService.Data/Entities/PerformanceEvaluation.cs` with FK to Supplier, Score 1-5 constraint
- [X] T019 [P] Create entity `Maliev.SupplierService.Data/Entities/SupplierAuditLog.cs` with JSONB OldValues/NewValues
- [X] T020 [P] Create entity `Maliev.SupplierService.Data/Entities/OnboardingStatus.cs` with FK to Supplier

### EF Core Configurations

- [X] T021 [P] Create configuration `Maliev.SupplierService.Data/Configurations/SupplierConfiguration.cs` with indexes on TaxId (unique), Status, CompanyName
- [X] T022 [P] Create configuration `Maliev.SupplierService.Data/Configurations/SupplierContactConfiguration.cs` with FK and indexes
- [X] T023 [P] Create configuration `Maliev.SupplierService.Data/Configurations/MaterialCategoryConfiguration.cs` with unique Name index
- [X] T024 [P] Create configuration `Maliev.SupplierService.Data/Configurations/SupplierCapabilityConfiguration.cs`
- [X] T025 [P] Create configuration `Maliev.SupplierService.Data/Configurations/SupplierCertificationConfiguration.cs` with ExpirationDate index
- [X] T026 [P] Create configuration `Maliev.SupplierService.Data/Configurations/PerformanceEvaluationConfiguration.cs` with EvaluationDate index
- [X] T027 [P] Create configuration `Maliev.SupplierService.Data/Configurations/SupplierAuditLogConfiguration.cs` with Timestamp index
- [X] T028 [P] Create configuration `Maliev.SupplierService.Data/Configurations/OnboardingStatusConfiguration.cs`
- [X] T029 Create DbContext `Maliev.SupplierService.Data/SupplierDbContext.cs` with all DbSets and ApplyConfigurationsFromAssembly
- [X] T030 Create initial EF Core migration `Maliev.SupplierService.Data/Migrations/` using `dotnet ef migrations add InitialCreate`

### API Infrastructure

- [X] T031 [P] Create configuration class `Maliev.SupplierService.Api/Configuration/JwtSettings.cs`
- [X] T032 [P] Create configuration class `Maliev.SupplierService.Api/Configuration/RedisSettings.cs`
- [X] T033 [P] Create configuration class `Maliev.SupplierService.Api/Configuration/RabbitMQSettings.cs`
- [X] T034 [P] Create configuration class `Maliev.SupplierService.Api/Configuration/ExternalServicesSettings.cs` for PurchaseOrder, Invoice, Stock services
- [X] T035 Create middleware `Maliev.SupplierService.Api/Middleware/ExceptionHandlingMiddleware.cs` with structured error responses
- [X] T036 Create middleware `Maliev.SupplierService.Api/Middleware/RequestLoggingMiddleware.cs` with Serilog
- [X] T037 Create extension `Maliev.SupplierService.Api/Extensions/ServiceCollectionExtensions.cs` for DI registration
- [X] T038 Create extension `Maliev.SupplierService.Api/Extensions/WebApplicationExtensions.cs` for middleware pipeline
- [X] T039 [P] Create `Maliev.SupplierService.Api/appsettings.json` with placeholder configuration structure
- [X] T040 [P] Create `Maliev.SupplierService.Api/appsettings.Development.json` with local dev settings
- [X] T041 Create `Maliev.SupplierService.Api/Program.cs` with full pipeline: Aspire ServiceDefaults, Serilog, JWT auth, rate limiting, CORS, Scalar, health checks, metrics per plan.md

### Core Services Infrastructure

- [X] T042 Create interface `Maliev.SupplierService.Api/Services/ICacheService.cs` with Get, Set, Remove, InvalidateByTag methods
- [X] T043 Create implementation `Maliev.SupplierService.Api/Services/CacheService.cs` using StackExchange.Redis with TTL strategies per research.md
- [X] T044 Create interface `Maliev.SupplierService.Api/Services/IAuditService.cs` with LogChange method
- [X] T045 Create EF Core interceptor for automatic audit logging based on research.md SaveChangesInterceptor pattern

### Test Infrastructure

- [X] T046 Create `Maliev.SupplierService.Tests/Integration/Infrastructure/IntegrationTestWebAppFactory.cs` with Testcontainers for PostgreSQL, RabbitMQ, Redis
- [X] T047 Create `Maliev.SupplierService.Tests/Integration/Infrastructure/BaseIntegrationTest.cs` with HttpClient, DbContext, cleanup helpers
- [X] T048 Create `Maliev.SupplierService.Tests/Integration/Infrastructure/TestAuthHandler.cs` with Admin claims mock

**Checkpoint**: Foundation ready - `dotnet build` passes, migrations exist, Program.cs runs with health endpoints

---

## Phase 3: User Story 1 - Register New Supplier (Priority: P1)

**Goal**: Enable procurement administrators to register new suppliers with company profile, contacts, and material categories

**Independent Test**: Create a new supplier, verify it gets ID and "PendingApproval" status, then retrieve it by ID

### DTOs for US1

- [X] T049 [P] [US1] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/CreateSupplierRequest.cs` per openapi-v1.yaml schema
- [X] T050 [P] [US1] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/CreateContactRequest.cs`
- [X] T051 [P] [US1] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/SupplierResponse.cs` with RowVersion
- [X] T052 [P] [US1] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/ContactResponse.cs`
- [X] T053 [P] [US1] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/MaterialCategoryResponse.cs`
- [X] T054 [P] [US1] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/CapabilityResponse.cs`

### Validators for US1

- [X] T055 [P] [US1] Create validator `Maliev.SupplierService.Api/Validators/CreateSupplierRequestValidator.cs` with required fields, TaxId format
- [X] T056 [P] [US1] Create validator `Maliev.SupplierService.Api/Validators/CreateContactRequestValidator.cs` with email validation

### Service Layer for US1

- [X] T057 [US1] Create interface `Maliev.SupplierService.Api/Services/ISupplierService.cs` with CreateAsync method signature
- [X] T058 [US1] Create implementation `Maliev.SupplierService.Api/Services/SupplierService.cs` - CreateAsync: validate uniqueness by TaxId, create with PendingApproval status, handle contacts/categories, publish SupplierCreated event

### Controller for US1

- [X] T059 [US1] Create controller `Maliev.SupplierService.Api/Controllers/SuppliersController.cs` with POST /suppliers/v1/suppliers endpoint, [Authorize] attribute, validation, 201 response with Location header

### Events for US1

- [X] T060 [US1] Create event `Maliev.SupplierService.Api/Events/SupplierCreated.cs` record with SupplierId, CompanyName, CreatedAt
- [X] T061 [US1] Configure MassTransit publisher in ServiceCollectionExtensions for SupplierCreated event

**Checkpoint**: POST /suppliers creates supplier with PendingApproval status, returns 201 with ID, audit log created

---

## Phase 4: User Story 2 - Retrieve Supplier Information (Priority: P1)

**Goal**: Enable retrieval of supplier details by ID and basic search for integration with Purchase Order Service

**Independent Test**: Query supplier by ID, verify complete profile returned; query non-existent ID, verify 404

### DTOs for US2

- [X] T062 [P] [US2] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/SupplierDetailResponse.cs` including contacts, categories, capabilities, certifications, performanceSummary
- [X] T063 [P] [US2] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/SupplierValidationResponse.cs` for integration endpoint
- [X] T064 [P] [US2] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/SupplierEligibilityResponse.cs` with isEligible, reasons

### Service Layer for US2

- [X] T065 [US2] Add GetByIdAsync method to ISupplierService and SupplierService.cs - include all related entities, use caching per research.md
- [X] T066 [US2] Add ValidateSupplierAsync method to ISupplierService for integration endpoint
- [X] T067 [US2] Add CheckEligibilityAsync method to ISupplierService - check Active status and valid certifications

### Controller Endpoints for US2

- [X] T068 [US2] Add GET /suppliers/v1/suppliers/{id} to SuppliersController.cs - return SupplierDetailResponse, 404 if not found
- [X] T069 [US2] Add GET /suppliers/v1/suppliers/{id}/validate to SuppliersController.cs for service-to-service validation
- [X] T070 [US2] Add GET /suppliers/v1/suppliers/{id}/eligibility to SuppliersController.cs for PO eligibility check
- [X] T071 [US2] Add GET /suppliers/v1/categories to SuppliersController.cs - list all material categories

**Checkpoint**: GET /suppliers/{id} returns complete profile with 500ms caching, eligibility endpoint works for PO integration

---

## Phase 5: User Story 3 - Update Supplier Information (Priority: P2)

**Goal**: Enable updating supplier details with optimistic concurrency and automatic audit logging

**Independent Test**: Update supplier fields, verify changes persisted, verify audit log entry created with before/after values

### DTOs for US3

- [X] T072 [P] [US3] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/UpdateSupplierRequest.cs` with RowVersion for concurrency
- [X] T073 [P] [US3] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/UpdateStatusRequest.cs` with Status and reason

### Validators for US3

- [X] T074 [P] [US3] Create validator `Maliev.SupplierService.Api/Validators/UpdateSupplierRequestValidator.cs`

### Service Layer for US3

- [X] T075 [US3] Add UpdateAsync method to ISupplierService and SupplierService.cs - handle concurrency conflict (409), invalidate cache, publish SupplierUpdated event
- [X] T076 [US3] Add UpdateStatusAsync method to ISupplierService - validate status transition rules, publish SupplierStatusChanged event

### Audit Service Implementation for US3

- [X] T077 [US3] Create implementation `Maliev.SupplierService.Api/Services/AuditService.cs` using interceptor from T045 - capture before/after JSON values

### Controller Endpoints for US3

- [X] T078 [US3] Add PUT /suppliers/v1/suppliers/{id} to SuppliersController.cs - partial update support, 409 on concurrency conflict
- [X] T079 [US3] Add PATCH /suppliers/v1/suppliers/{id}/status to SuppliersController.cs - status change with reason
- [X] T080 [US3] Add PATCH /suppliers/v1/suppliers/{id}/metadata to SuppliersController.cs - lightweight update for lastOrderDate (FR-034) from external service callbacks

### Events for US3

- [X] T081 [P] [US3] Create event `Maliev.SupplierService.Api/Events/SupplierUpdated.cs` with ChangedFields array
- [X] T082 [P] [US3] Create event `Maliev.SupplierService.Api/Events/SupplierStatusChanged.cs` with OldStatus, NewStatus

**Checkpoint**: Updates trigger audit logs with before/after values, concurrency conflicts return 409 with current state

---

## Phase 6: User Story 4 - List Active Suppliers (Priority: P2)

**Goal**: Enable paginated listing with filters by status, category, capability, and sorting

**Independent Test**: Create multiple suppliers with different statuses/categories, query with filters, verify correct results returned

### DTOs for US4

- [X] T083 [P] [US4] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/SupplierListResponse.cs` with Items, TotalCount, Page, PageSize, TotalPages

### Service Layer for US4

- [X] T084 [US4] Add ListSuppliersAsync method to ISupplierService with pagination, status filter, categoryId filter, capability filter, search, sortBy, sortOrder parameters
- [X] T085 [US4] Implement ListSuppliersAsync in SupplierService.cs - build dynamic query, apply caching for common queries (active suppliers list)

### Controller Endpoint for US4

- [X] T086 [US4] Add GET /suppliers/v1/suppliers to SuppliersController.cs - all query parameters per openapi-v1.yaml, default page=1, pageSize=20

**Checkpoint**: List endpoint returns paginated results under 1 second for 1000 suppliers, filters work correctly

---

## Phase 7: User Story 5 - Manage Supplier Documentation Metadata (Priority: P2)

**Goal**: Track certification metadata with expiration dates for compliance monitoring

**Independent Test**: Add certification to supplier, query for expiring certifications, verify supplier appears in results

### DTOs for US5

- [X] T087 [P] [US5] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/CreateCertificationRequest.cs`
- [X] T088 [P] [US5] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/CertificationResponse.cs` with isExpired, isExpiringSoon flags
- [X] T089 [P] [US5] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/ExpiringCertificationResponse.cs` with supplierName, daysUntilExpiration

### Validators for US5

- [X] T090 [P] [US5] Create validator `Maliev.SupplierService.Api/Validators/CreateCertificationRequestValidator.cs` - issueDate required, expirationDate >= issueDate if provided

### Service Layer for US5

- [X] T091 [US5] Create interface/methods for certification management in ISupplierService: AddCertificationAsync, ListCertificationsAsync, GetExpiringCertificationsAsync
- [X] T092 [US5] Implement certification methods in SupplierService.cs - ExpirationDate index query per research.md

### Controller for US5

- [X] T093 [US5] Create controller `Maliev.SupplierService.Api/Controllers/SupplierCertificationsController.cs` with:
  - POST /suppliers/v1/suppliers/{id}/certifications
  - GET /suppliers/v1/suppliers/{id}/certifications
  - GET /suppliers/v1/certifications/expiring?days=30

**Checkpoint**: Certifications can be added, expiring certifications query returns suppliers needing attention

---

## Phase 8: User Story 6 - Record Performance Ratings (Priority: P3)

**Goal**: Track supplier performance history with 1-5 ratings and aggregate metrics

**Independent Test**: Add multiple evaluations, query history with summary statistics, verify average calculation

### DTOs for US6

- [X] T094 [P] [US6] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/CreatePerformanceEvaluationRequest.cs` with score 1-5 validation
- [X] T095 [P] [US6] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/PerformanceEvaluationResponse.cs`
- [X] T096 [P] [US6] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/EvaluationListResponse.cs` with pagination
- [X] T097 [P] [US6] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/PerformanceSummaryResponse.cs` with averageRating, evaluationCount, ratingsByCategory

### Validators for US6

- [X] T098 [P] [US6] Create validator `Maliev.SupplierService.Api/Validators/CreatePerformanceEvaluationRequestValidator.cs` - score 1-5, evaluationDate not future

### Service Layer for US6

- [X] T099 [US6] Create interface/methods in ISupplierService: AddEvaluationAsync, ListEvaluationsAsync, GetPerformanceSummaryAsync
- [X] T100 [US6] Implement evaluation methods in SupplierService.cs - aggregate calculation per research.md (on-demand with caching)

### Controller for US6

- [X] T101 [US6] Create controller `Maliev.SupplierService.Api/Controllers/PerformanceEvaluationsController.cs` with:
  - POST /suppliers/v1/suppliers/{id}/evaluations
  - GET /suppliers/v1/suppliers/{id}/evaluations (paginated)
  - GET /suppliers/v1/suppliers/{id}/evaluations/summary

**Checkpoint**: Evaluations recorded with 1-5 scores, summary shows correct average and count

---

## Phase 9: User Story 7 - Track Supplier Onboarding Workflow (Priority: P3)

**Goal**: Track onboarding through stages with validated transitions

**Independent Test**: Advance supplier through stages, verify invalid transitions rejected, verify timestamps recorded

### DTOs for US7

- [X] T102 [P] [US7] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/AdvanceOnboardingRequest.cs` with targetStage, notes
- [X] T103 [P] [US7] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/OnboardingStatusResponse.cs`

### Service Layer for US7

- [X] T104 [US7] Create state machine helper `Maliev.SupplierService.Api/Services/OnboardingTransitions.cs` with IsValidTransition per research.md
- [X] T105 [US7] Add AdvanceOnboardingAsync method to ISupplierService - validate transition, record timestamp, update supplier status when reaching Active
- [X] T106 [US7] Add GetOnboardingHistoryAsync method to ISupplierService

### Controller Endpoints for US7

- [X] T107 [US7] Add POST /suppliers/v1/suppliers/{id}/onboarding to SuppliersController.cs - advance stage
- [X] T108 [US7] Add GET /suppliers/v1/suppliers/{id}/onboarding to SuppliersController.cs - get history

**Checkpoint**: Valid transitions succeed with timestamp, invalid transitions return 400 with explanation

---

## Phase 10: User Story 8 - Audit Trail Access (Priority: P3)

**Goal**: Provide queryable audit trail with date filtering for compliance

**Independent Test**: Make changes to supplier, query audit trail, verify all changes visible with timestamps and before/after values

### DTOs for US8

- [X] T109 [P] [US8] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/AuditLogResponse.cs` with changeType, changedByName, oldValues, newValues
- [X] T110 [P] [US8] Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/AuditLogListResponse.cs` with pagination

### Service Layer for US8

- [X] T111 [US8] Add GetAuditTrailAsync method to ISupplierService with startDate, endDate filters, pagination
- [X] T112 [US8] Implement audit query in SupplierService.cs - paginated, filtered by date range

### Controller for US8

- [X] T113 [US8] Create controller `Maliev.SupplierService.Api/Controllers/SupplierAuditController.cs` with GET /suppliers/v1/suppliers/{id}/audit

**Checkpoint**: Audit trail queryable with date filters, pagination works for large histories

---

## Phase 11: Deletion Protection & External Service Integration

**Goal**: Implement safe deletion with synchronous dependency checks per FR-005

### External Service Clients

- [X] T114 [P] Create interface `Maliev.SupplierService.Api/Services/ExternalServices/IPurchaseOrderServiceClient.cs` with CheckReferencesAsync
- [X] T115 [P] Create interface `Maliev.SupplierService.Api/Services/ExternalServices/IInvoiceServiceClient.cs` with CheckReferencesAsync
- [X] T116 [P] Create interface `Maliev.SupplierService.Api/Services/ExternalServices/IStockServiceClient.cs` with CheckReferencesAsync
- [X] T117 [P] Create implementation `Maliev.SupplierService.Api/Services/ExternalServices/PurchaseOrderServiceClient.cs` with Polly resilience per research.md
- [X] T118 [P] Create implementation `Maliev.SupplierService.Api/Services/ExternalServices/InvoiceServiceClient.cs` with Polly resilience
- [X] T119 [P] Create implementation `Maliev.SupplierService.Api/Services/ExternalServices/StockServiceClient.cs` with Polly resilience

### DTOs for Deletion

- [X] T120 Create response DTO `Maliev.SupplierService.Api/DTOs/Responses/DependencyErrorResponse.cs` with dependencies array

### Service Layer for Deletion

- [X] T121 Add DeleteAsync method to ISupplierService - check all dependencies synchronously, fail-closed if service unavailable
- [X] T122 Implement DeleteAsync in SupplierService.cs - parallel dependency checks with Task.WhenAll, 409 if references exist, 503 if service unavailable

### Controller Endpoint for Deletion

- [X] T123 Add DELETE /suppliers/v1/suppliers/{id} to SuppliersController.cs - return 204 on success, 409 with dependencies on block, 503 on service unavailable

### Events for Deletion

- [X] T124 Create event `Maliev.SupplierService.Api/Events/SupplierDeleted.cs` record

**Checkpoint**: Deletion blocked when references exist, fail-closed when dependent services unavailable

---

## Phase 12: Contacts Management

**Goal**: Full CRUD for supplier contacts

### Validators

- [X] T125 [P] Create request DTO `Maliev.SupplierService.Api/DTOs/Requests/UpdateContactRequest.cs`
- [X] T126 [P] Create validator `Maliev.SupplierService.Api/Validators/UpdateContactRequestValidator.cs`

### Service Layer

- [X] T127 Add contact management methods to ISupplierService: AddContactAsync, UpdateContactAsync, DeleteContactAsync, ListContactsAsync
- [X] T128 Implement contact methods in SupplierService.cs - enforce single primary contact rule

### Controller

- [X] T129 Create controller `Maliev.SupplierService.Api/Controllers/SupplierContactsController.cs` with full CRUD endpoints per openapi-v1.yaml

**Checkpoint**: Contacts can be added/updated/deleted, primary contact enforced

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements affecting multiple user stories

### Docker

- [X] T130 Create `Maliev.SupplierService.Api/Dockerfile` with BuildKit secrets for NuGet per plan.md Dockerfile pattern

### Metrics & Health

- [X] T131 Add Prometheus metrics for supplier operations: supplier_operations_total, suppliers_by_status, supplier_operation_duration_seconds per constitution XII
- [X] T132 Configure health check endpoints at /suppliers/liveness and /suppliers/readiness in Program.cs

### Error Responses

- [X] T133 Create error response DTOs `Maliev.SupplierService.Api/DTOs/Responses/ErrorResponse.cs` and `ValidationErrorResponse.cs` per openapi-v1.yaml

### Final Validation

- [X] T134 Verify zero warnings: `dotnet build Maliev.SupplierService.sln --warnaserror`
- [X] T135 Run all tests: `dotnet test Maliev.SupplierService.Tests/`
- [X] T136 Verify Docker build: `docker build -f Maliev.SupplierService.Api/Dockerfile .` (Note: requires GitHub token for private NuGet packages in CI/CD)
- [X] T137 Validate against quickstart.md: Start service locally and verify all documented endpoints respond
- [X] T138 [P] Validate /suppliers/metrics endpoint returns Prometheus format with required labels per constitution XII

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Stories (Phase 3-10)**: All depend on Foundational phase completion
  - US1 and US2 (both P1) can run in parallel after Foundational
  - US3 depends on US1 (needs suppliers to update)
  - US4 depends on US1 (needs suppliers to list)
  - US5-US8 can start after US1 (need supplier creation)
- **Deletion (Phase 11)**: Depends on US1-US2
- **Contacts (Phase 12)**: Depends on US1
- **Polish (Phase 13)**: Depends on all desired user stories being complete

### User Story Dependencies

| Story | Priority | Dependencies | Can Start After |
|-------|----------|--------------|-----------------|
| US1 - Register Supplier | P1 | Foundational | Phase 2 complete |
| US2 - Retrieve Supplier | P1 | Foundational | Phase 2 complete |
| US3 - Update Supplier | P2 | US1 | US1 complete |
| US4 - List Suppliers | P2 | US1 | US1 complete |
| US5 - Certifications | P2 | US1 | US1 complete |
| US6 - Performance | P3 | US1 | US1 complete |
| US7 - Onboarding | P3 | US1 | US1 complete |
| US8 - Audit Trail | P3 | US3 (audit entries) | US3 complete |

### Parallel Opportunities

**Within Foundational Phase:**
- All enum tasks (T009-T012) in parallel
- All entity tasks (T013-T020) in parallel (after enums)
- All configuration tasks (T021-T028) in parallel
- All API config tasks (T031-T034) in parallel

**Within Each User Story:**
- All DTO tasks marked [P] in parallel
- All validator tasks marked [P] in parallel

**Across User Stories:**
- US1 and US2 can run in parallel
- US3, US4, US5 can run in parallel (after US1)
- US6, US7 can run in parallel (after US1)

---

## Parallel Example: Foundational Phase

```bash
# Launch all enum tasks in parallel:
Task: "Create enum SupplierStatus.cs"
Task: "Create enum OnboardingStage.cs"
Task: "Create enum CertificationType.cs"
Task: "Create enum PerformanceRatingCategory.cs"

# Then launch all entity tasks in parallel:
Task: "Create entity Supplier.cs"
Task: "Create entity SupplierContact.cs"
Task: "Create entity MaterialCategory.cs"
# ... all 8 entities
```

## Parallel Example: User Story 1

```bash
# Launch all US1 DTOs in parallel:
Task: "Create CreateSupplierRequest.cs"
Task: "Create CreateContactRequest.cs"
Task: "Create SupplierResponse.cs"
Task: "Create ContactResponse.cs"
Task: "Create MaterialCategoryResponse.cs"
Task: "Create CapabilityResponse.cs"

# Launch validators in parallel:
Task: "Create CreateSupplierRequestValidator.cs"
Task: "Create CreateContactRequestValidator.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 - Register Supplier
4. Complete Phase 4: User Story 2 - Retrieve Supplier
5. **STOP and VALIDATE**: Test US1 + US2 independently
6. Deploy/demo: Basic supplier CRUD with integration endpoints

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 + US2 → Test independently → **MVP Deploy** (create + retrieve)
3. Add US3 + US4 → Test independently → Deploy (update + list)
4. Add US5 → Test independently → Deploy (compliance tracking)
5. Add US6 + US7 → Test independently → Deploy (performance + onboarding)
6. Add US8 + Deletion → Test independently → Deploy (audit + safe deletion)
7. Polish → Final release

### Suggested MVP Scope

**Minimum Viable Product**: Phases 1-4 (Setup + Foundational + US1 + US2)
- Total tasks: 71 tasks
- Delivers: Supplier creation, retrieval, validation, eligibility check
- Integration ready: Purchase Order Service can validate suppliers

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Total tasks: 138

### Test Strategy

Per Constitution III (Testing & Quality), this task list follows the **implementation-first approach with checkpoint validation**:

1. **Test Infrastructure**: Phase 2 includes T046-T048 establishing Testcontainers-based integration test infrastructure
2. **Checkpoint Testing**: Each phase checkpoint describes independent test scenarios to validate before proceeding
3. **Final Validation**: Phase 13 includes T135 (`dotnet test`) ensuring all tests pass before release

**Test tasks are intentionally omitted from individual user story phases** to allow flexibility in test approach:
- TDD practitioners: Write test tasks before implementation tasks
- Implementation-first: Write tests at checkpoints or end of phase
- Contract testing: Use openapi-v1.yaml for API contract validation

If TDD is required, add test tasks before each service/controller task (e.g., `T057-TEST [US1] Write unit tests for SupplierService.CreateAsync`).
