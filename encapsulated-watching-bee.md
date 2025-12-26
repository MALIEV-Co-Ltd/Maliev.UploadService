# GCP-Style IAM Consistency Analysis & Migration Plan

## Executive Summary

Analyzed 18 microservices (excluding UploadService) for consistency with Google Cloud IAM-style roles and permissions. Found significant inconsistencies across authorization implementations, with only 14/18 services having IAM integration and NO services using resource-scoped permissions despite IAM Service supporting it.

## Current State Overview

### IAM Service Architecture (✅ Well-Designed)

The IAM Service implements proper GCP-style architecture:
- **Principals**: Universal identity (users + service accounts)
- **Roles**: GCP format `roles.{service}.{role-name}`
- **Permissions**: Fine-grained format `{service}.{resource}.{action}`
- **Bindings**: Hierarchical resource paths (e.g., `projects/123/datasets/456`)
- **Caching**: Redis with 5-minute TTL
- **Performance**: <10ms cached permission checks

### Services Status

**IAM-Integrated (14 services):**
1. AccountingService - ✅ Permission-based
2. AuthService - ✅ Issues JWT tokens
3. CareerService - ✅ IAM integrated
4. CustomerService - ✅ IAM integrated
5. EmployeeService - ⚠️ HYBRID (mixed old roles + new permissions)
6. InvoiceService - ✅ IAM integrated
7. MaterialService - ✅ IAM integrated
8. NotificationService - ✅ Permission-based
9. OrderService - ✅ BEST IMPLEMENTATION (full IAM + caching)
10. PaymentService - ✅ IAM integrated
11. QuotationService - ✅ Permission-based
12. ReceiptService - ✅ IAM integrated
13. SupplierService - ✅ Permission-based
14. PurchaseOrderService - ⚠️ Partial integration

**NOT IAM-Integrated (4 services):**
1. ChatbotService - ❌ No IAM
2. ContactService - ❌ Stub authorization
3. CountryService - ❌ No IAM
4. CurrencyService - ❌ Local permission checking
5. PdfService - ❌ No IAM
6. PredictionService - ❌ No IAM

## Critical Inconsistencies Found

### 1. Multiple RequirePermissionAttribute Implementations

**Problem**: 6 different implementations across services!

- `Aspire.ServiceDefaults`: IAuthorizationFilter
- `PaymentService`: AuthorizeAttribute with Policy
- `OrderService`: AuthorizeAttribute with Policy
- `CurrencyService`: IAsyncAuthorizationFilter
- `ContactService`: HasPermissionAttribute (different name!)
- `IAMService`: Placeholder with TODO comment

**Impact**: Inconsistent behavior, difficult to maintain

### 2. Resource-Scoped Authorization NOT IMPLEMENTED

**Critical Finding**: Despite IAM Service supporting hierarchical resource paths (`projects/123/datasets/456`), ZERO services use it!

**Current**: All services only check global permissions
```csharp
[RequirePermission("order.orders.read")] // Global access to ALL orders
```

**Should Be**: Resource-scoped permissions
```csharp
// Check permission on specific resource path
HasPermission("order.orders.read", resourcePath: "orders/customer-123/*")
```

**Files Supporting Resource Paths**:
- IAMService `PermissionResolver.cs` - Has `MatchesResourcePath()` method
- `PrincipalRoleBinding` entity - Has `ResourcePath` property

### 3. EmployeeService Hybrid Mess

**Problem**: Uses BOTH old role-based AND new permission-based authorization

**Old Pattern (still in use)**:
```csharp
[Authorize(Policy = Policies.RequireHRRole)]
[Authorize(Policy = Policies.RequireManagerRole)]
```

**Controllers affected**: All 13 controllers still use role-based policies

**Should Be**:
```csharp
[RequirePermission("employee.employees.read")]
[RequirePermission("employee.leave.approve")]
```

### 4. IAM Service Self-Authorization is Broken

**File**: `Maliev.IAMService.Api/Authorization/RequirePermissionAttribute.cs`
```csharp
// TODO: Implement permission check logic
// For now, this is a placeholder that allows all requests
```

The IAM service protecting all other services has NO authorization on itself!

### 5. Inconsistent Permission Checking

**Pattern 1**: Call IAM Service (CORRECT - OrderService)
```csharp
var permissions = await _iamClient.GetUserPermissionsAsync(userId);
```

**Pattern 2**: Read from JWT claims (STALE DATA - CurrencyService)
```csharp
var permissions = user.FindAll("permissions").Select(c => c.Value).ToList();
```

**Problem**: Pattern 2 doesn't reflect permission changes until re-login

### 6. Wildcard Support Inconsistent

- **ServiceDefaults**: Supports wildcards at any level
- **PurchaseOrderService**: Custom wildcard logic
- **IAM Service**: Hierarchical wildcards (`/*` and `/**`)
- **Most services**: No wildcard support

### 7. Caching Strategies Vary

- **OrderService**: 5-min Redis cache with thundering herd protection
- **IAM Service**: 5-min Redis cache
- **Most services**: No caching, read from JWT
- **UploadService**: 60-min cache

## Missing GCP Features

### Not Implemented:
1. ❌ Resource-scoped authorization (despite IAM supporting it)
2. ❌ Conditional IAM policies (time-based, IP-based, attribute-based)
3. ❌ Policy inheritance (hierarchical resources)
4. ❌ Consistent audit logging across services
5. ❌ Permission groups/organization
6. ❌ Policy simulation/testing tools

## Services Requiring Updates

### Priority 1 - Critical Issues

**EmployeeService** (HIGHEST PRIORITY)
- Remove ALL role-based authorization
- Replace with permission-based
- Fix `ResourceAuthorizationHandler` to use permissions
- Update all 13 controllers
- Files: 13 controllers + `AuthorizationConstants.cs`

**IAMService**
- Implement actual authorization (remove TODO placeholder)
- Add permission checks to all endpoints
- File: `Authorization/RequirePermissionAttribute.cs`

**OrderService, PaymentService, SupplierService**
- Add resource-scoped permission checks
- Example: User can only access their own orders

### Priority 2 - Missing IAM Integration

**ContactService, CountryService, CurrencyService, PdfService**
- Integrate with IAM Service
- Replace local permission checking
- Use centralized `RequirePermissionAttribute`

**PurchaseOrderService**
- Complete IAM integration (currently partial)
- Remove local wildcard logic
- Use IAM's hierarchical matching

### Priority 3 - Standardization

**All Services**
- Use single `RequirePermissionAttribute` from ServiceDefaults
- Remove duplicate implementations
- Standardize caching strategy
- Add consistent audit logging

## Implementation Strategy

### Approach Chosen (Based on User Input)
1. ✅ **Full consistency across all 18 services**
2. ✅ **Implement resource-scoped permissions** (true GCP-style)
3. ✅ **Complete EmployeeService migration** (remove ALL role-based auth)
4. ✅ **Standardize RequirePermissionAttribute** (single implementation)

---

## Detailed Implementation Plan

### Phase 1: Foundation (Week 1-2)

#### 1.1 Enhance ServiceDefaults RequirePermissionAttribute
**File**: `Maliev.Aspire.ServiceDefaults/Authorization/RequirePermissionAttribute.cs`

**Enhancements Needed**:
- Add `ResourcePathTemplate` property (e.g., `"customers/{customerId}/orders/{orderId}"`)
- Support route token replacement for automatic resource path resolution
- Add `RequireLiveCheck` flag for forcing IAM service calls
- Implement IIamServiceClient interface for resource-scoped checks
- Add Redis caching (5-min TTL) for IAM responses
- Add graceful degradation (fail-secure by default, configurable fail-open)
- Preserve backward compatibility with JWT-embedded permissions

**New Features**:
```csharp
[RequirePermission("order.orders.read", ResourcePathTemplate = "customers/{customerId}/orders/{orderId}")]
```

#### 1.2 Create IIamServiceClient Interface
**New File**: `Maliev.Aspire.ServiceDefaults/IAM/IIamServiceClient.cs`

**Methods**:
- `Task<bool> CheckPermissionAsync(CheckPermissionRequest)` - Resource-scoped check
- `Task<Dictionary<string, bool>> CheckPermissionsAsync(BulkCheckRequest)` - Bulk checks
- `Task<IEnumerable<string>> GetUserPermissionsAsync(string userId)` - Existing method

#### 1.3 Enhance IAM Service PermissionResolver
**File**: `Maliev.IAMService.Api/Services/PermissionResolver.cs`

**Changes**:
- Already has `MatchesResourcePath()` - verify it works correctly
- Add bulk check endpoint: `POST /iam/v1/auth/check-permissions` (accept array)
- Optimize caching for bulk requests

#### 1.4 Implement IAMService Self-Authorization
**Critical Fix**: IAM currently has TODO placeholder!

**Steps**:
1. Define IAMPermissions constants (iam.principals.create, iam.roles.create, etc.)
2. Bootstrap admin principal in database seed
3. Replace TODO RequirePermissionAttribute with ServiceDefaults version
4. Apply permissions to all IAM controllers

**Files**:
- `IAMService.Api/Authorization/IAMPermissions.cs` (new)
- `IAMService.Data/IAMDbContext.cs` (add seed)
- `IAMService.Api/Controllers/*` (add [RequirePermission])

---

### Phase 2: EmployeeService Migration (Week 3)

#### 2.1 Permission Definitions (Already Done ✅)
**File**: `EmployeeService.Api/Authorization/EmployeePermissions.cs`
- 27 permissions already defined
- Format: `employee.{resource}.{action}`

#### 2.2 Role Mapping (Already Done ✅)
**File**: `EmployeeService.Api/Authorization/EmployeePredefinedRoles.cs`
- 5 predefined roles already defined
- Mapped to IAM format: `roles.employee.{role-name}`

#### 2.3 Data Migration: Export Existing Role Bindings
**New Script**: Create migration service to:
1. Query existing employee role assignments
2. Map old roles → IAM roles (`Admin` → `roles.employee.system-administrator`)
3. Create resource-scoped bindings for managers (`employees/{reportId}/**`)
4. Bulk import to IAM via API

**Affected Records**: All active employees with roles

#### 2.4 Controller Updates (32 IsInRole calls to remove)

**Controllers to Update**:
1. **LeaveController** - 5 `IsInRole()` calls → `[RequirePermission(EmployeePermissions.LeaveApprove)]`
2. **PerformanceController** - 8 calls → Use `PerformanceCreate/Read/Update`
3. **TrainingController** - 10 calls → Use `TrainingAssign/Read`
4. **ManagersController** - 4 calls → Use manager permissions
5. **EmployeeProfileController** - 2 calls → Use profile permissions
6. **TeamsController** - 2 calls → Use teams permissions
7. **BulkOperationsController** - 1 call → Use admin permissions

**Pattern**:
```csharp
// OLD (remove)
if (!_currentUserService.IsInRole(Roles.HR) && !_currentUserService.IsInRole(Roles.Admin))
    return Forbid();

// NEW
[RequirePermission(EmployeePermissions.LeaveRead)]
// Attribute handles authorization
```

**Resource-Scoped Example** (managers approving direct reports):
```csharp
[RequirePermission(EmployeePermissions.LeaveApprove)]
public async Task ApproveLeave(Guid requestId)
{
    var request = await _service.GetAsync(requestId);
    var resourcePath = $"employees/{request.EmployeeId}/leave/{requestId}";

    // Manual check for resource-scoped permission
    if (!await _iamClient.CheckPermissionAsync(principalId, permission, resourcePath))
        return Forbid();
}
```

#### 2.5 Remove Role Constants
**File**: `EmployeeService.Domain/Authorization/AuthorizationConstants.cs`
- Remove `Roles` class (HR, Admin, Manager, Employee)
- Keep `Policies` temporarily for compatibility, mark deprecated
- Eventually remove policies entirely

---

### Phase 3: Standardize Existing IAM-Integrated Services (Week 4)

#### 3.1 Remove Duplicate RequirePermissionAttribute Implementations

**Services with Custom Implementations**:
1. **OrderService** - `OrderService.Api/Authorization/RequirePermissionAttribute.cs`
2. **ContactService** - `ContactService.Api/Services/Auth/HasPermissionAttribute.cs`
3. **PaymentService** - Has custom implementation
4. **CurrencyService** - Has local implementation
5. **IAMService** - Has TODO placeholder

**Action**: Delete all, replace with:
```csharp
using Maliev.Aspire.ServiceDefaults.Authorization;
```

#### 3.2 Remove Custom PermissionAuthorizationHandlers

**Files to Remove**:
- `OrderService.Api/Authorization/PermissionAuthorizationHandler.cs`
- Similar handlers in other services

**Reason**: Functionality moved to standardized `RequirePermissionAttribute`

#### 3.3 Add Resource-Scoped Checks

**OrderService Example**:
```csharp
[HttpGet("orders/{customerId}/{orderId}")]
[RequirePermission("order.orders.read", ResourcePathTemplate = "customers/{customerId}/orders/{orderId}")]
public async Task<IActionResult> GetOrder(string customerId, string orderId)
```

**InvoiceService Example**:
```csharp
[RequirePermission("invoice.invoices.read", ResourcePathTemplate = "customers/{customerId}/invoices/{invoiceId}")]
```

---

### Phase 4: Integrate Non-IAM Services (Week 5)

#### 4.1 PdfService (Full Integration Needed)

**Define Permissions** (new file):
```csharp
// PdfService.Api/Authorization/PdfPermissions.cs
public static class PdfPermissions
{
    public const string DocumentsGenerate = "pdf.documents.generate";
    public const string DocumentsRead = "pdf.documents.read";
    public const string TemplatesCreate = "pdf.templates.create";
    public const string TemplatesRead = "pdf.templates.read";
    public const string TemplatesUpdate = "pdf.templates.update";
    public const string TemplatesDelete = "pdf.templates.delete";
}
```

**Define Roles** (new file):
```csharp
// PdfService.Api/Authorization/PdfPredefinedRoles.cs
public static readonly RoleRegistration Admin = new()
{
    RoleId = "roles.pdf.admin",
    Permissions = PdfPermissions.All
};
```

**Create IAMRegistrationService** (new file):
- Copy pattern from `ContactService.Api/Services/Auth/ContactIAMRegistrationService.cs`

**Update Controllers**:
```csharp
[RequirePermission(PdfPermissions.DocumentsGenerate)]
public async Task<IActionResult> GeneratePdf(...)
```

#### 4.2 CountryService & CurrencyService (Partial Integration)

**Status**: Already have IAMRegistrationService, just need controller updates

**CountryService Permissions**:
```csharp
public const string CountriesRead = "country.countries.read";
public const string CountriesManage = "country.countries.manage";
```

**Update Controllers**:
```csharp
// Replace [Authorize(Policy = "CountryWrite")]
[RequirePermission(CountryPermissions.CountriesManage)]
```

#### 4.3 ChatbotService & PredictionService

**Action**: Verify if deployed, if yes apply standard migration

**Estimated**: 2 days each if needed

---

### Phase 5: Resource-Scoped Rollout (Week 6)

#### 5.1 Define Resource Path Hierarchies

**OrderService**:
- `customers/{customerId}/orders/{orderId}`
- `customers/{customerId}/orders/{orderId}/items/{itemId}`

**InvoiceService**:
- `customers/{customerId}/invoices/{invoiceId}`
- `customers/{customerId}/invoices/{invoiceId}/payments/{paymentId}`

**EmployeeService**:
- `employees/{employeeId}/**` (all employee data)
- `employees/{employeeId}/leave/{requestId}`
- `employees/{employeeId}/performance/{reviewId}`
- `departments/{deptId}/**`

**MaterialService**:
- `materials/{materialId}`
- `warehouses/{warehouseId}/inventory/{itemId}`

#### 5.2 Apply Resource Path Templates

**Pattern**:
```csharp
[HttpGet("{customerId}/orders/{orderId}")]
[RequirePermission("order.orders.read", ResourcePathTemplate = "customers/{customerId}/orders/{orderId}")]
```

#### 5.3 Create Manager-Scoped Bindings

**Example**: Manager can approve leave for direct reports only
```csharp
// Grant via IAM API
{
  "principalId": "manager-uuid",
  "roleId": "roles.employee.manager",
  "resourcePath": "employees/report-1/**"  // All resources under this employee
}
```

**Bulk Creation**: Script to create bindings for all manager → report relationships

---

### Phase 6: Testing & Validation (Week 7)

#### 6.1 Unit Tests

**Files to Create**:
- `ServiceDefaults.Tests/Authorization/RequirePermissionAttributeTests.cs`
- `IAMService.Tests/Services/PermissionResolverTests.cs`

**Test Cases**:
- Resource path template resolution
- Wildcard matching (`/*`, `/**`)
- Cache hit/miss scenarios
- Graceful degradation

#### 6.2 Integration Tests

**Pattern** (add to each service):
```csharp
[Fact]
public async Task Authorization_WithResourceScope_AllowsAccess()
{
    var token = CreateTokenWithBinding("roles.order.viewer", "customers/customer-123/**");
    var response = await client.GetAsync("/orders/customer-123/order-456");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task Authorization_WithoutResourceScope_DeniesAccess()
{
    var token = CreateTokenWithBinding("roles.order.viewer", "customers/customer-456/**");
    var response = await client.GetAsync("/orders/customer-123/order-456");
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

#### 6.3 Performance Tests

**Target Metrics**:
- Permission check latency: p95 < 10ms (cached), p95 < 50ms (uncached)
- Cache hit rate: > 95%
- IAM service throughput: > 1000 checks/sec

**Load Test**:
```csharp
// Run 10,000 permission checks
// Verify p95 latency meets targets
// Verify no memory leaks in cache
```

---

## Critical Design Decisions

### Decision 1: Resource Path Resolution Strategy

**Three-Tier Approach**:

**Tier 1** - Route-based (automatic):
```csharp
[RequirePermission("order.orders.read", ResourcePathTemplate = "customers/{customerId}/orders/{orderId}")]
```
Attribute automatically resolves from route parameters.

**Tier 2** - Manual (business logic):
```csharp
var resourcePath = $"customers/{order.CustomerId}/orders/{orderId}";
await _iamClient.CheckPermissionAsync(principalId, permission, resourcePath);
```
Use when resource path depends on database lookups.

**Tier 3** - Global (no resource scoping):
```csharp
[RequirePermission("order.orders.list")]  // No ResourcePathTemplate = global
```

### Decision 2: JWT vs Live IAM Checks

**Hybrid Approach**:
- **Default**: Use JWT-embedded permissions (fast <5ms)
- **Resource-scoped**: Call IAM with caching (10-50ms)
- **Cache**: Redis 5-minute TTL

**Trade-off**: Performance vs freshness (acceptable for most use cases)

### Decision 3: Graceful Degradation

**Fail-Secure by Default**:
- If IAM unreachable: Return 503 (Service Unavailable)
- Config option: `FailSecureOnIAMError=false` for fail-open
- JWT-embedded permissions work during IAM outage

### Decision 4: Permission Granularity

**Action-Level, Not Field-Level**:
- Format: `{service}.{resource}.{action}`
- Examples: `employee.leave.approve`, `order.orders.cancel`
- NOT: `employee.leave.salary-field.read` (too granular)

**Rationale**: Manageable permission set size, easier administration

---

## Data Migration Plan

### Step 1: Principal Creation (One-Time)

**Run for EmployeeService and CustomerService**:
```sql
-- Create principals for all employees
INSERT INTO iam.principals (principal_id, principal_type, email, linked_service, linked_entity_id)
SELECT principal_id, 'user', work_email, 'EmployeeService', employee_id
FROM employee_service.employees
WHERE principal_id IS NOT NULL
ON CONFLICT DO NOTHING;
```

### Step 2: Role Binding Migration

**Export from EmployeeService**:
```sql
SELECT e.principal_id, er.role_name, e.reports_to_id
FROM employees e JOIN employee_roles er ON e.employee_id = er.employee_id;
```

**Transform and Import**:
```csharp
// Map role_name → IAM role ID
// Create resource-scoped bindings for managers
// Bulk import via /iam/v1/principals/{id}/roles
```

### Step 3: Validate Migration

**Queries**:
```sql
-- Count principals created
SELECT COUNT(*) FROM iam.principals WHERE linked_service = 'EmployeeService';

-- Count role bindings
SELECT COUNT(*) FROM iam.principal_role_bindings WHERE role_id LIKE 'roles.employee%';

-- Verify manager scopes
SELECT * FROM iam.principal_role_bindings WHERE resource_path IS NOT NULL;
```

---

## Rollback Strategy

### Feature Flags (Progressive Rollout)

```json
{
  "Features": {
    "PermissionBasedAuthEnabled": true,
    "ResourceScopedAuthEnabled": false,  // Disable if issues
    "RequirePermissionClaims": false     // Graceful degradation
  }
}
```

### Rollback Steps

**Level 1** (5 min): Disable resource scoping
```bash
kubectl set env deployment/order-service Features__ResourceScopedAuthEnabled=false
```

**Level 2** (10 min): Disable permission checks
```bash
kubectl set env deployment/order-service Features__PermissionBasedAuthEnabled=false
```

**Level 3** (15 min): Full rollback
```bash
kubectl rollout undo deployment/order-service
```

### Gradual Rollout Plan

**Week 1**: 5% traffic (canary)
**Week 2**: 25% traffic
**Week 3**: 50% traffic
**Week 4**: 100% traffic

Monitor at each stage for 48 hours before increasing.

---

## Monitoring & Alerts

### Key Metrics

**Authorization**:
- `auth.permission.success` (counter)
- `auth.permission.failure` (counter)
- `auth.permission.duration` (histogram, target p95 < 10ms)
- `auth.cache.hit_rate` (gauge, target > 95%)

**IAM Service**:
- `iam.permission_check.duration` (histogram, target p95 < 50ms)
- `iam.cache.invalidation.lag` (gauge)

### Critical Alerts

```yaml
- name: HighAuthorizationFailureRate
  condition: rate(auth.permission.failure[5m]) > 10
  severity: critical

- name: PermissionCheckLatencyHigh
  condition: histogram_quantile(0.95, auth.permission.duration) > 50ms
  severity: warning

- name: IAMServiceDown
  condition: up{job="iam-service"} == 0
  severity: critical
  action: Enable fail-open mode
```

---

## Implementation Timeline

**Week 1-2**: Foundation (ServiceDefaults, IAM enhancements)
**Week 3**: EmployeeService migration (highest priority)
**Week 4**: Standardize existing IAM-integrated services
**Week 5**: Integrate non-IAM services (Pdf, Country, Currency)
**Week 6**: Resource-scoped rollout
**Week 7**: Testing, monitoring, documentation

**Total**: 7 weeks for complete migration

---

## Success Criteria

✅ **All 18 services** use standardized `RequirePermissionAttribute`
✅ **Zero** role-based authorization in EmployeeService
✅ **Resource-scoped** permissions functional in critical services
✅ **IAMService** properly authorizes its own endpoints
✅ **Performance**: p95 permission check < 10ms cached, < 50ms uncached
✅ **Cache hit rate** > 95%
✅ **Zero** authorization bypass vulnerabilities
✅ **Complete** audit trail for all permission checks

---

## Critical Files

### Framework
- `Maliev.Aspire.ServiceDefaults/Authorization/RequirePermissionAttribute.cs` ⭐
- `Maliev.Aspire.ServiceDefaults/IAM/IIamServiceClient.cs` (new)
- `Maliev.IAMService.Api/Services/PermissionResolver.cs`

### EmployeeService (Highest Priority)
- `EmployeeService.Api/Controllers/LeaveController.cs` (5 IsInRole calls)
- `EmployeeService.Api/Controllers/PerformanceController.cs` (8 IsInRole calls)
- `EmployeeService.Api/Controllers/TrainingController.cs` (10 IsInRole calls)
- All 13 controllers total

### IAM Self-Authorization
- `IAMService.Api/Authorization/IAMPermissions.cs` (new)
- `IAMService.Data/IAMDbContext.cs` (add bootstrap seed)
- All IAM controllers

### Reference Implementations
- `OrderService.Api/Authorization/PermissionAuthorizationHandler.cs` (best practices)
- `ContactService.Api/Services/Auth/ContactIAMRegistrationService.cs` (template)
