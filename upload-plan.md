# UploadService Implementation Plan

## Total: ~15 hours (~2 days)

## Phase 1: Define Permissions & Roles (2 hours)
- Create UploadPermissions.cs
- Create UploadPredefinedRoles.cs
- Define service-specific upload permissions

## Phase 2: IAM Registration (2 hours)
- Create UploadIAMRegistrationService.cs

## Phase 3: Migrate from ServiceAuthorizationPolicy (3 hours)
- Analyze current policies
- Create equivalent IAM permissions
- Grant to service accounts
- Update authorization logic

## Phase 4: Update Controllers (3 hours)
- Update all controllers
- Remove ServiceAuthorizationPolicy checks

## Phase 5: Update Tests (3 hours)
- Update integration tests
- Test service-to-service uploads

## Phase 6: Deploy & Cleanup (2 hours)
- Deploy and verify
- Drop ServiceAuthorizationPolicy table after verification
