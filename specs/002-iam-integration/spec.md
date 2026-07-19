# Feature Specification: Permission-Based Authorization Migration

**Feature Branch**: `002-iam-integration`  
**Created**: 2025-12-23  
**Status**: Draft  
**Input**: User description: "Migrate UploadService from ServiceAuthorizationPolicy to IAM permission-based authorization"

## Clarifications

### Session 2025-12-23
- Q: How does the system handle authorization when both legacy rules and new IAM permissions exist for a service? → A: IAM Overrides Legacy (Use IAM if present, otherwise fallback to legacy).
- Q: How are service-specific permissions mapped to storage folders or categories? → A: Resource-scoped permissions (Permissions are checked against hierarchical resource paths, e.g., `folders/invoice-files/**`).
- Q: What is the primary mechanism for migrating legacy authorization rules? → A: Automated Migration Script (One-time utility to convert all rules to IAM assignments).
- Q: What is the granularity of the new IAM permissions? → A: Resource-scoped (GCP-style `{service}.{resource}.{action}` with support for hierarchical resource paths).
- Q: What is the policy for cleaning up legacy authorization data? → A: Retain Metadata (Delete legacy rules, but keep audit history/metadata for compliance).
- Q: What is the caching strategy for authorization checks? → A: 5-minute Redis TTL (Standardized across ecosystem to balance performance and freshness).
- Q: What is the preferred authorization implementation pattern? → A: Standardized `[RequirePermission]` attribute from `Maliev.Aspire.ServiceDefaults`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resource-Scoped File Upload (Priority: P1)

As a service (e.g., InvoiceService), I want to upload files to a specific storage path using resource-scoped permissions (e.g., `upload.files.upload` on `folders/invoice-files/**`) so that I am restricted only to my designated resource hierarchy.

**Why this priority**: Core security requirement for true multi-tenancy and alignment with GCP-style IAM principles.

**Independent Test**: Verify that a service with permission on `folders/invoices/**` can upload to that path but is denied when attempting to upload to `folders/orders/**`.

**Acceptance Scenarios**:

1. **Given** a service account with `upload.files.upload` permission scoped to `folders/invoice-files/**`, **When** it attempts to upload to `/invoice-files/2025/inv.pdf`, **Then** the system authorizes the request.
2. **Given** the same service account, **When** it attempts to upload to `/receipts/2025/rec.pdf`, **Then** the system returns an "Access Denied" response.

---

### User Story 2 - Administrative Management (Priority: P2)

As an administrator, I want to manage roles and view system-wide metrics using granular permissions so that I can maintain the service without having unrestricted access to all data.

**Why this priority**: Granular permissions improve security and follow the principle of least privilege.

**Independent Test**: Verify a metrics-only permission grants access to dashboards but denies administrative deletion.

**Acceptance Scenarios**:

1. **Given** a user with permissions to view metrics, **When** they request upload metrics, **Then** the data is returned successfully.
2. **Given** a user with metrics permissions but without administrative deletion permissions, **When** they attempt a bulk delete operation, **Then** the request is denied.

---

### User Story 3 - User File Ownership (Priority: P3)

As an end-user, I want to list and download my own files while being prevented from accessing files belonging to other users or services, enforced via resource-scoped permissions on user directories.

**Why this priority**: Essential for multi-tenant security and user privacy.

**Independent Test**: Verify User A can only access resources under `users/user-a/**`.

**Acceptance Scenarios**:

1. **Given** a user with permissions scoped to `users/{userId}/**`, **When** they request to list their files, **Then** only files under their path are returned.
2. **Given** the same user, **When** they attempt to access a file under `users/other-user/**`, **Then** the request is denied.

---

### Edge Cases

- **Mixed Mode Authorization**: System will follow an "IAM Overrides Legacy" strategy. If an identity has IAM permissions assigned, those are used exclusively.
- **Resource Path Wildcards**: Correct handling of `/*` (single level) vs `/**` (recursive) matching in resource-scoped authorization.
- **Service Identity Changes**: How the system handles permissions if a service identity is updated or replaced.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST define and register resource-scoped permissions following the `{service}.{resource}.{action}` format.
- **FR-002**: System MUST support hierarchical resource paths for authorization (e.g., `folders/invoice-files/**`).
- **FR-003**: System MUST provide predefined roles (e.g., Admin, Manager, User, Viewer) using standard `roles.upload.{role-name}` format.
- **FR-004**: Standard user roles MUST be restricted to operations on specific resource paths matching the user identity.
- **FR-005**: System MUST use the standardized `[RequirePermission]` attribute from `Maliev.Aspire.ServiceDefaults` for controller-level authorization.
- **FR-006**: System MUST provide an automated migration script to map existing legacy rules to resource-scoped IAM bindings.
- **FR-007**: Legacy authorization rules MUST be decommissioned after all services have migrated, while preserving audit history/metadata for compliance.
- **FR-008**: System MUST implement Redis caching for authorization checks with a 5-minute TTL.

### Key Entities *(include if feature involves data)*

- **Permission**: Represents a granular action that can be performed (e.g., "Read File").
- **Role**: A collection of permissions assigned to an identity.
- **File Metadata**: Existing entity that must now store or be linked to ownership information for permission checks.
- **Legacy Rule Map**: A temporary mapping used during migration to ensure business continuity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of service interactions are protected by the new permission model.
- **SC-002**: All required permissions and predefined roles are successfully registered and enforceable.
- **SC-003**: Migration is complete when the legacy authorization storage contains no active rules and can be safely removed.
- **SC-004**: Zero regressions in file operations for migrated services during and after migration.
- **SC-005**: Authorization check latency remains under 10ms (cached) or 50ms (uncached) per request.