# Implementation Plan: Supplier Service WebAPI

**Branch**: `001-supplier-service` | **Date**: 2025-11-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-supplier-service/spec.md`

## Summary

The Supplier Service is the authoritative source of supplier information within the MALIEV microservices ecosystem. It provides a high-performance WebAPI for managing supplier records including company profiles, contacts, material categories, capabilities, certifications, performance ratings, and onboarding workflows. The service implements comprehensive audit trails, caching for read performance, and deletion protection via synchronous dependency checks with Purchase Order, Invoice, and Stock services.

**Technical Approach**: .NET 10 WebAPI with Clean Architecture pattern, PostgreSQL for persistence, Redis for distributed caching, RabbitMQ for event publishing, and Testcontainers for integration testing.

## Technical Context

**Language/Version**: .NET 10 (C# 13)
**Primary Dependencies**:
- Entity Framework Core 9.0.10 (data access)
- Npgsql 9.0.4 (PostgreSQL provider)
- FluentValidation 11.3.0 (request validation)
- MassTransit 8.3.4 with RabbitMQ 7.0.0 (messaging)
- StackExchange.Redis 9.0.0 (distributed caching)
- Polly 8.5.0 with Microsoft.Extensions.Http.Resilience 9.0.0 (HTTP resilience)
- Serilog 8.0.2 (structured logging)
- Scalar 1.2.42 with Microsoft.AspNetCore.OpenApi 9.0.0 (API documentation)
- Asp.Versioning.Http 8.1.0 (API versioning)
- Prometheus.AspNetCore 8.2.1 (metrics)
- Maliev.Aspire.ServiceDefaults (observability via NuGet from GitHub Packages)

**Storage**: PostgreSQL 18 (database name: `supplier_app_db`)
**Testing**:
- xUnit with Testcontainers 4.0.0+ (PostgreSQL, RabbitMQ, Redis modules)
- Microsoft.AspNetCore.Mvc.Testing for integration tests
- FluentAssertions for test assertions

**Target Platform**: Linux containers (Docker), Kubernetes deployment
**Project Type**: Microservice WebAPI (3-project solution)

**Performance Goals** (from spec):
- SC-002: Supplier retrieval by ID < 500ms for 99% of requests
- SC-003: Active supplier list queries < 1 second for up to 1000 suppliers
- SC-004: 100 concurrent users without degradation
- SC-007: Cache hit rate > 80% for active supplier queries
- SC-008: Supplier eligibility validation < 200ms

**Constraints**:
- 7-year audit log retention (FR-022a)
- Synchronous dependency checks for deletion (fail-closed)
- No secrets in source code (Google Secret Manager injection)
- Zero warnings build policy

**Scale/Scope**:
- 8 key entities (Supplier, SupplierContact, MaterialCategory, SupplierCapability, SupplierCertification, PerformanceEvaluation, SupplierAuditLog, OnboardingStatus)
- 34 functional requirements
- 8 user stories across P1-P3 priorities

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Service Autonomy | PASS | Own database (supplier_app_db), own domain logic, API-only integration with other services |
| II. Explicit Contracts | PASS | OpenAPI via Scalar, versioned APIs (Asp.Versioning), contracts in /contracts/ |
| III. Test-First Development | PASS | Unit, integration, contract tests planned; Testcontainers for real infrastructure |
| IV. Real Infrastructure Testing | PASS | Testcontainers for PostgreSQL 18, RabbitMQ, Redis - no in-memory substitutes |
| V. Auditability & Observability | PASS | SupplierAuditLog entity, 7-year retention, Serilog JSON logging, health checks |
| VI. Security & Compliance | PASS | JWT authentication, role-based authorization, audit compliance |
| VII. Secrets Management | PASS | Google Secret Manager via /mnt/secrets, no secrets in code |
| VIII. Zero Warnings Policy | PASS | Build configured for TreatWarningsAsErrors |
| IX. Clean Project Artifacts | PASS | .gitignore and .dockerignore configured per standard |
| X. Docker Best Practices | PASS | Multi-stage build, app user, BuildKit secrets for NuGet |
| XI. Simplicity & Maintainability | PASS | Clean Architecture, stateless design, minimal dependencies |
| XII. Business Metrics & Analytics | PASS | Prometheus metrics for supplier operations, tagged by service/version/env |
| XIII. .NET Aspire Integration | PASS | ServiceDefaults via NuGet PackageReference, nuget.config with GitHub Packages |

**Gate Status**: ALL PASS - Proceed to Phase 0

## Project Structure

### Documentation (this feature)

```text
specs/001-supplier-service/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (OpenAPI specs)
│   └── openapi-v1.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
Maliev.SupplierService.sln

Maliev.SupplierService.Api/
├── Controllers/
│   ├── SuppliersController.cs
│   ├── SupplierContactsController.cs
│   ├── SupplierCertificationsController.cs
│   ├── PerformanceEvaluationsController.cs
│   └── SupplierAuditController.cs
├── DTOs/
│   ├── Requests/
│   │   ├── CreateSupplierRequest.cs
│   │   ├── UpdateSupplierRequest.cs
│   │   ├── CreateContactRequest.cs
│   │   ├── CreateCertificationRequest.cs
│   │   ├── CreatePerformanceEvaluationRequest.cs
│   │   └── AdvanceOnboardingRequest.cs
│   └── Responses/
│       ├── SupplierResponse.cs
│       ├── SupplierListResponse.cs
│       ├── SupplierDetailResponse.cs
│       ├── ContactResponse.cs
│       ├── CertificationResponse.cs
│       ├── PerformanceEvaluationResponse.cs
│       ├── AuditLogResponse.cs
│       └── SupplierEligibilityResponse.cs
├── Services/
│   ├── ISupplierService.cs
│   ├── SupplierService.cs
│   ├── ICacheService.cs
│   ├── CacheService.cs
│   ├── IAuditService.cs
│   ├── AuditService.cs
│   └── ExternalServices/
│       ├── IPurchaseOrderServiceClient.cs
│       ├── PurchaseOrderServiceClient.cs
│       ├── IInvoiceServiceClient.cs
│       ├── InvoiceServiceClient.cs
│       ├── IStockServiceClient.cs
│       └── StockServiceClient.cs
├── Validators/
│   ├── CreateSupplierRequestValidator.cs
│   ├── UpdateSupplierRequestValidator.cs
│   ├── CreateContactRequestValidator.cs
│   ├── CreateCertificationRequestValidator.cs
│   └── CreatePerformanceEvaluationRequestValidator.cs
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── WebApplicationExtensions.cs
├── Configuration/
│   ├── JwtSettings.cs
│   ├── RedisSettings.cs
│   ├── RabbitMQSettings.cs
│   └── ExternalServicesSettings.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile
└── Maliev.SupplierService.Api.csproj

Maliev.SupplierService.Data/
├── Entities/
│   ├── Supplier.cs
│   ├── SupplierContact.cs
│   ├── MaterialCategory.cs
│   ├── SupplierCapability.cs
│   ├── SupplierCertification.cs
│   ├── PerformanceEvaluation.cs
│   ├── SupplierAuditLog.cs
│   └── OnboardingStatus.cs
├── Enums/
│   ├── SupplierStatus.cs
│   ├── OnboardingStage.cs
│   ├── CertificationType.cs
│   └── PerformanceRatingCategory.cs
├── Configurations/
│   ├── SupplierConfiguration.cs
│   ├── SupplierContactConfiguration.cs
│   ├── MaterialCategoryConfiguration.cs
│   ├── SupplierCapabilityConfiguration.cs
│   ├── SupplierCertificationConfiguration.cs
│   ├── PerformanceEvaluationConfiguration.cs
│   ├── SupplierAuditLogConfiguration.cs
│   └── OnboardingStatusConfiguration.cs
├── SupplierDbContext.cs
├── Migrations/
└── Maliev.SupplierService.Data.csproj

Maliev.SupplierService.Tests/
├── Unit/
│   ├── Services/
│   │   ├── SupplierServiceTests.cs
│   │   ├── CacheServiceTests.cs
│   │   └── AuditServiceTests.cs
│   └── Validators/
│       ├── CreateSupplierRequestValidatorTests.cs
│       └── UpdateSupplierRequestValidatorTests.cs
├── Integration/
│   ├── Infrastructure/
│   │   ├── IntegrationTestWebAppFactory.cs
│   │   ├── BaseIntegrationTest.cs
│   │   └── TestAuthHandler.cs
│   ├── Controllers/
│   │   ├── SuppliersControllerTests.cs
│   │   ├── SupplierContactsControllerTests.cs
│   │   ├── SupplierCertificationsControllerTests.cs
│   │   └── PerformanceEvaluationsControllerTests.cs
│   └── ExternalServices/
│       └── DependencyCheckTests.cs
├── Contract/
│   └── SupplierApiContractTests.cs
└── Maliev.SupplierService.Tests.csproj

# Root configuration files
nuget.config
.dockerignore
.gitignore
```

**Structure Decision**: Standard MALIEV 3-project microservice structure (Api, Data, Tests) following Clean Architecture pattern with Controllers → Services → Data flow.

## Complexity Tracking

> No constitution violations requiring justification. All principles satisfied with standard patterns.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |
