# Research: IAM Authorization Migration

## Decision: IAM Integration Pattern
**Chosen**: Integrate with `Maliev.IAMService` via its client library/SDK.
**Rationale**: `Maliev.IAMService` is the central authority for permissions in the ecosystem. Using it ensures consistency across all services.
**Alternatives considered**: 
- GCP IAM: Rejected because we want to maintain a custom permission model decoupled from infrastructure providers.
- Local Permission Store: Rejected because it violates the goal of centralizing authorization.

## Decision: Migration Script Implementation
**Chosen**: A one-time Startup Background Task in `Maliev.UploadService.Api`.
**Rationale**: It allows for easy execution in any environment (Dev/Staging/Prod) upon deployment, and can be guarded by a "MigrationEnabled" flag or a check for records in the legacy table. Once the table is empty, the task becomes a no-op.
**Alternatives considered**:
- Entity Framework Migration (SQL): Rejected because granting IAM permissions requires API calls to `Maliev.IAMService`, which cannot be done from a SQL migration.
- Manual CLI Tool: Rejected as it adds operational complexity to the deployment process.

## Decision: Permission Naming Convention
**Chosen**: `upload.[resource].[action]`
**Rationale**: Aligns with existing patterns in other Maliev services and supports the convention-based folder mapping (Clarification 2).
**Examples**: 
- `upload.invoice-files.upload`
- `upload.admin.bulk-delete`

## Decision: Testing Strategy
**Chosen**: Use `Testcontainers` to spin up a mock or real `Maliev.IAMService` instance (if container available) or a specialized Test Double.
**Rationale**: Ensures adherence to Constitution Principle IV (Real Infrastructure) while maintaining test speed and isolation.
