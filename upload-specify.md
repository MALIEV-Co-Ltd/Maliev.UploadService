# UploadService Specification - Permission-Based Authorization Migration

## Overview
UploadService currently uses ServiceAuthorizationPolicy (database-stored policies). Migrate to IAM permission-based authorization.

## Current State
- Uses ServiceAuthorizationPolicy table for path-based authorization
- Services have allowed upload prefixes

## Target State
- Permission-based authorization
- Service accounts have specific upload permissions

## Permissions to Define

### Upload Operations
```
upload.files.upload              - Upload files to storage
upload.files.read                - Read/download files
upload.files.delete              - Delete files
upload.files.list                - List uploaded files
```

### Admin Operations
```
upload.admin.manage-policies     - Manage upload policies
upload.admin.bulk-delete         - Bulk delete files
upload.admin.view-metrics        - View upload metrics
```

### Retention Operations
```
upload.retention.configure       - Configure retention policies
upload.retention.execute         - Execute retention cleanup
```

### Service-Specific Upload Permissions
```
upload.invoice-files.upload      - Upload invoice-related files
upload.order-files.upload        - Upload order-related files
upload.receipt-files.upload      - Upload receipt-related files
(etc. for each service that uploads)
```

## Predefined Roles

### upload-admin
**Permissions**: All upload.* permissions

### upload-manager
**Permissions**: files.*, admin.manage-policies, admin.view-metrics, retention.configure

### upload-user
**Permissions**: files.upload, files.read, files.delete (own files only)

### upload-viewer
**Permissions**: files.read, files.list

## Service Account Permissions

Each service that uploads files should have its specific permission:
- InvoiceService → upload.invoice-files.upload
- OrderService → upload.order-files.upload
- ReceiptService → upload.receipt-files.upload

## Migration from ServiceAuthorizationPolicy

1. Analyze current ServiceAuthorizationPolicy records
2. Map each policy to equivalent IAM permission
3. Grant permissions to service accounts
4. Remove ServiceAuthorizationPolicy table

## Success Criteria
- [ ] ~15 permissions registered
- [ ] 4 predefined roles registered
- [ ] Service accounts have appropriate upload permissions
- [ ] ServiceAuthorizationPolicy removed
