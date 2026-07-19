# Implementation Plan: Permission-Based Authorization Migration

**Branch**: `002-iam-integration` | **Date**: 2025-12-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/002-iam-integration/spec.md`

## Summary

Migrate the UploadService's authorization logic from a legacy path-based database model (`ServiceAuthorizationPolicy`) to a resource-scoped IAM permission-based model. This involves defining permissions in the `{service}.{resource}.{action}` format, implementing hierarchical resource path authorization (GCP-style), and utilizing the standardized `[RequirePermission]` attribute from `Maliev.Aspire.ServiceDefaults`.

## Technical Context

**Language/Version**: .NET 10 (C#)
**Primary Dependencies**: ASP.NET Core, EF Core, Google Cloud Storage, .NET Aspire, `Maliev.Aspire.ServiceDefaults`
**Storage**: PostgreSQL (for remaining metadata), Google Cloud Storage (for files), Central IAM Service (for permissions)
**Testing**: xUnit, Testcontainers (PostgreSQL, IAM mock/real instance)
**Target Platform**: Linux (Docker)
**Project Type**: Web API
**Performance Goals**: Authorization check latency < 10ms (cached), < 50ms (uncached)
**Constraints**: IAM Overrides Legacy fallback; Resource-scoped granularity; Standardized `[RequirePermission]` attribute; 5-minute Redis cache TTL.
**Scale/Scope**: ~15 granular permissions; 4 predefined roles; 100% endpoint protection using resource-scoped bindings.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Service Autonomy | PASS | IAM migration centralizes auth but preserves service-specific data ownership. |
| II. Explicit Contracts | PASS | New auth requirements will be reflected in API documentation. |
| III. Test-First | PASS | Phase 5 explicitly covers test updates. |
| IV. Real Infrastructure | PASS | Testcontainers will be used for PostgreSQL. |
| V. Auditability | PASS | Audit history preserved during decommissioning (Clarification 5). |
| X. Docker Best Practices| PASS | Dockerfile is correctly located in the API project. |
| XIII. Aspire Integration| PASS | builder.AddServiceDefaults() already in place. |
| XIV. No AutoMapper/Fluent| PASS | Using manual mapping and DataAnnotations. |

## Project Structure

### Documentation (this feature)

```text
specs/002-iam-integration/
├── spec.md              # Feature Specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── auth-migration.openapi.json
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
Maliev.UploadService.Api/
├── Controllers/v1/      # Update auth attributes
├── Services/            # Implement IAM logic, Migration Service
├── Extensions/          # Update mapping/validation
└── BackgroundServices/  # Lifecycle management

Maliev.UploadService.Data/
├── Entities/            # Legacy table to be removed
└── Migrations/          # Script to drop legacy table

Maliev.UploadService.Tests/
├── Integration/         # New IAM-based auth tests
└── Unit/                # Service logic tests
```

**Structure Decision**: Standard .NET microservice structure as per MALIEV constitution (Flat root).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*No violations detected.*