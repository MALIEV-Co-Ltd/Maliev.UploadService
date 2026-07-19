# Data Model: IAM Authorization Migration

## Removed Entities
- **ServiceAuthorizationPolicy**: This table will be decommissioned and dropped after migration.

## New/Updated Entities (IAM System)
*Note: These are managed by Maliev.IAMService but used by UploadService.*

### Permissions
| Permission Name | Category | Description |
|-----------------|----------|-------------|
| `upload.files.upload` | Files | Upload files to specific resource path |
| `upload.files.read` | Files | Read/Download files from resource path |
| `upload.files.delete` | Files | Delete individual files |
| `upload.files.list` | Files | List files in resource hierarchy |
| `upload.admin.manage-policies` | Admin | Manage IAM roles/perms for Upload |
| `upload.admin.bulk-delete` | Admin | Trigger bulk delete jobs |
| `upload.admin.view-metrics` | Admin | Access telemetry dashboard |
| `upload.retention.configure` | Retention | Set lifecycle policies |
| `upload.retention.execute` | Retention | Manually trigger cleanup |

### Predefined Roles
- **roles.upload.admin**: All `upload.*` permissions on `/**`.
- **roles.upload.manager**: `files.*`, `admin.manage-policies`, `admin.view-metrics`, `retention.configure` on specific resource paths.
- **roles.upload.user**: `files.upload`, `files.read`, `files.delete` scoped to owner resource path (e.g., `users/{userId}/**`).
- **roles.upload.viewer**: `files.read`, `files.list`.

## Ownership Mapping
To support resource-scoped authorization, the `[RequirePermission]` attribute will resolve resource paths from route parameters (e.g., `folders/{folderName}/**`). The IAM Service will verify if the principal has the required permission for that specific path.

| Entity | Field | Purpose |
|--------|-------|---------|
| `FileMetadata` | `UploadedBy` | User ID / Subject of the uploader |
