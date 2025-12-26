# Quickstart: IAM Authorization Migration

## 1. Configuration
Configure IAM settings in your `appsettings.json`:

```json
{
  "IAM": {
    "BaseUrl": "http://iam-service",
    "CacheTtlMinutes": 5
  }
}
```

## 2. Registering Permissions
The service defines its permissions in `UploadPermissions.cs`. These can be registered using `UploadIAMRegistrationService`.

## 3. Running the Migration
The `UploadIAMMigrationService` can be invoked to convert legacy `ServiceAuthorizationPolicy` records into IAM bindings.

```csharp
// Example invocation in a startup task
await migrationService.MigrateLegacyPoliciesAsync(cleanupLegacy: true);
```

## 4. Verification
Verify that a service (e.g., `InvoiceService`) can still upload to its path:
```bash
# Attempt upload with IAM token
curl -X POST http://localhost:8080/upload/v1/uploads \
  -H "Authorization: Bearer <IAM_TOKEN>" \
  -F "file=@invoice.pdf" \
  -F "ServiceName=InvoiceService" \
  -F "Path=/invoice-files/2025/inv-001.pdf"
```

## 5. Decommissioning
After verifying all services are migrated, set `Authorization:Migration:CleanupLegacyAfterMigration=true` or manually drop the `ServiceAuthorizationPolicies` table.
