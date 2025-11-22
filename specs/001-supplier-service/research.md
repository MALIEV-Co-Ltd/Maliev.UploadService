# Research: Supplier Service WebAPI

**Branch**: `001-supplier-service` | **Date**: 2025-11-22
**Phase**: 0 - Research & Decisions

## Research Summary

This document consolidates technical decisions and best practices research for the Supplier Service implementation. All "NEEDS CLARIFICATION" items from the Technical Context have been resolved.

---

## 1. Entity Framework Core Audit Trail Pattern

**Decision**: Implement automatic audit logging using EF Core interceptors with `SaveChangesInterceptor`.

**Rationale**:
- EF Core interceptors provide a clean, centralized way to capture all entity changes without modifying business logic
- `SaveChangesInterceptor` can capture before/after values via `ChangeTracker`
- Works with all CRUD operations automatically
- Supports async operations required for production workloads

**Implementation Pattern**:
```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return result;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                // Create audit log entry with before/after values
            }
        }
        return result;
    }
}
```

**Alternatives Considered**:
- Domain events: More complex, requires event handlers
- Manual logging in services: Error-prone, easy to miss operations
- Database triggers: PostgreSQL-specific, harder to maintain

---

## 2. Redis Caching Strategy

**Decision**: Use distributed caching with `IDistributedCache` abstraction and custom `ICacheService` wrapper.

**Rationale**:
- `IDistributedCache` provides standard abstraction compatible with Redis
- Custom wrapper allows for cache-aside pattern with automatic invalidation
- Supports cache bypass for real-time operations
- Tag-based invalidation for related entity groups

**Cache Keys Pattern**:
```
supplier:{id}                    # Individual supplier
suppliers:active                 # Active suppliers list
suppliers:category:{categoryId}  # Suppliers by category
suppliers:eligibility:{id}       # Eligibility check result
```

**TTL Strategy**:
- Individual suppliers: 5 minutes
- List queries: 2 minutes
- Eligibility checks: 1 minute (frequent updates)

**Invalidation Events**:
- Supplier created/updated/deleted → invalidate `supplier:{id}`, all list caches
- Status changed → invalidate eligibility cache
- Certification updated → invalidate eligibility cache

**Alternatives Considered**:
- In-memory cache only: Doesn't scale across instances
- No caching: Performance targets not achievable (SC-002, SC-007)
- Cache-through: More complex, not needed for this read-heavy workload

---

## 3. Synchronous Dependency Check Pattern

**Decision**: Implement typed HttpClients with Polly resilience for synchronous dependency checks during supplier deletion.

**Rationale**:
- FR-005 requires synchronous verification with Purchase Order, Invoice, and Stock services
- Fail-closed behavior (FR-005a) requires reliable timeout handling
- Polly provides retry with exponential backoff and circuit breaker patterns

**Implementation Pattern**:
```csharp
public class DependencyCheckResult
{
    public bool HasReferences { get; set; }
    public string ServiceName { get; set; }
    public int ReferenceCount { get; set; }
    public string ErrorMessage { get; set; }
    public bool ServiceUnavailable { get; set; }
}

public async Task<DependencyCheckResult[]> CheckAllDependenciesAsync(Guid supplierId)
{
    var tasks = new[]
    {
        _purchaseOrderClient.CheckReferencesAsync(supplierId),
        _invoiceClient.CheckReferencesAsync(supplierId),
        _stockClient.CheckReferencesAsync(supplierId)
    };

    return await Task.WhenAll(tasks);
}
```

**Resilience Configuration**:
- Timeout: 5 seconds per service
- Retry: 3 attempts with exponential backoff (1s, 2s, 4s)
- Circuit breaker: Break after 5 failures, half-open after 30 seconds

**Alternatives Considered**:
- Event-driven with local reference counts: Eventual consistency not acceptable for deletion safety
- Single aggregated endpoint: Requires coordination service, adds complexity
- No retry: Risk of false negatives during transient failures

---

## 4. Optimistic Concurrency Control

**Decision**: Use `RowVersion` (byte[]) property with EF Core concurrency token.

**Rationale**:
- PostgreSQL `xmin` system column can change unexpectedly
- Explicit `RowVersion` column with `BYTEA` type provides reliable concurrency
- EF Core handles `DbUpdateConcurrencyException` automatically

**Implementation Pattern**:
```csharp
public class Supplier
{
    public Guid Id { get; set; }
    // ... other properties

    [Timestamp]
    public byte[] RowVersion { get; set; }
}

// Configuration
builder.Property(e => e.RowVersion)
    .IsRowVersion()
    .HasColumnName("row_version");
```

**Conflict Resolution**:
- Return 409 Conflict with current entity state
- Client must refetch and retry with updated RowVersion

**Alternatives Considered**:
- Last-write-wins: Data loss risk
- Database-level locking: Performance impact
- `xmin` system column: Can change during VACUUM operations

---

## 5. Onboarding Workflow State Machine

**Decision**: Implement simple state machine with allowed transitions validation.

**Rationale**:
- Fixed workflow stages per spec (Pending Approval → Documentation Review → Final Approval → Active)
- No complex branching or parallel states required
- Explicit transition validation prevents invalid state changes

**State Transitions**:
```
PendingApproval → DocumentationReview
DocumentationReview → FinalApproval
FinalApproval → Active
Active → Suspended (any time)
Suspended → Active (reactivation)
Any → Inactive (deactivation)
```

**Implementation Pattern**:
```csharp
public static class OnboardingTransitions
{
    private static readonly Dictionary<OnboardingStage, OnboardingStage[]> AllowedTransitions = new()
    {
        [OnboardingStage.PendingApproval] = [OnboardingStage.DocumentationReview],
        [OnboardingStage.DocumentationReview] = [OnboardingStage.FinalApproval],
        [OnboardingStage.FinalApproval] = [OnboardingStage.Active],
    };

    public static bool IsValidTransition(OnboardingStage from, OnboardingStage to)
        => AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}
```

**Alternatives Considered**:
- Full workflow engine (Elsa, etc.): Overkill for fixed workflow
- Database-driven transitions: Over-engineering for current requirements
- No validation: Risk of invalid state transitions

---

## 6. Performance Rating Aggregation

**Decision**: Compute aggregates on-demand with caching; no materialized views.

**Rationale**:
- 1-5 integer scale has low computational overhead
- Performance evaluations are infrequent compared to reads
- Caching handles performance requirements (SC-002)

**Aggregation Query**:
```csharp
var aggregate = await _context.PerformanceEvaluations
    .Where(e => e.SupplierId == supplierId)
    .GroupBy(e => e.SupplierId)
    .Select(g => new
    {
        AverageRating = g.Average(e => e.Score),
        EvaluationCount = g.Count(),
        LastEvaluationDate = g.Max(e => e.EvaluationDate)
    })
    .FirstOrDefaultAsync();
```

**Alternatives Considered**:
- Materialized view: Adds complexity, not needed for expected scale
- Denormalized aggregate column: Sync issues, audit complexity
- Pre-computed on write: Additional write overhead

---

## 7. Certification Expiration Query Strategy

**Decision**: Direct database query with indexed `ExpirationDate` column.

**Rationale**:
- Simple date range query satisfies FR-014
- Index on `ExpirationDate` ensures performance
- No background jobs needed for initial implementation

**Query Pattern**:
```csharp
var expiringCertifications = await _context.SupplierCertifications
    .Include(c => c.Supplier)
    .Where(c => c.ExpirationDate <= DateTime.UtcNow.AddDays(30))
    .Where(c => c.ExpirationDate > DateTime.UtcNow) // Not yet expired
    .OrderBy(c => c.ExpirationDate)
    .ToListAsync();
```

**Index**:
```sql
CREATE INDEX IX_SupplierCertifications_ExpirationDate
ON supplier_certifications (expiration_date);
```

**Alternatives Considered**:
- Background job with notifications: Phase 2 enhancement
- Materialized view: Over-engineering for query frequency

---

## 8. MassTransit Event Publishing

**Decision**: Publish domain events for supplier lifecycle changes via MassTransit with RabbitMQ.

**Rationale**:
- Enables async communication with other services
- Supports eventual consistency patterns for non-critical updates
- Decouples supplier service from consumers

**Events Published**:
```csharp
public record SupplierCreated(Guid SupplierId, string CompanyName, DateTime CreatedAt);
public record SupplierUpdated(Guid SupplierId, string[] ChangedFields, DateTime UpdatedAt);
public record SupplierStatusChanged(Guid SupplierId, SupplierStatus OldStatus, SupplierStatus NewStatus, DateTime ChangedAt);
public record SupplierDeleted(Guid SupplierId, DateTime DeletedAt);
```

**Exchange Configuration**:
- Exchange type: Fanout (for events)
- Durable: Yes
- Message TTL: 7 days (aligns with audit retention visibility)

**Alternatives Considered**:
- Direct HTTP webhooks: Tight coupling, reliability issues
- Database polling: Inefficient, latency
- No events: Limits integration capabilities

---

## 9. API Versioning Strategy

**Decision**: URL path versioning with `Asp.Versioning.Http`.

**Rationale**:
- Explicit version in URL (`/suppliers/v1/...`) is clear and debuggable
- Matches MALIEV standard routing pattern
- Supports multiple versions simultaneously during deprecation

**Configuration**:
```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});
```

**Alternatives Considered**:
- Header versioning: Less visible, harder to debug
- Query string versioning: Clutters URLs
- Media type versioning: Complex, overkill

---

## 10. Business Metrics Implementation

**Decision**: Prometheus metrics with standard labels per Constitution XII.

**Metrics Exposed**:
```
# Counter
supplier_operations_total{operation="create|update|delete|read", status="success|failure"}
supplier_evaluations_total{rating_category="..."}

# Gauge
suppliers_by_status{status="Active|Suspended|PendingApproval|Inactive"}
certifications_expiring_soon{days="7|30|90"}

# Histogram
supplier_operation_duration_seconds{operation="..."}
dependency_check_duration_seconds{service="PurchaseOrder|Invoice|Stock"}
```

**Labels (per Constitution)**:
- `service_name`: "supplier-service"
- `version`: from assembly
- `region`: from config
- `environment`: from ASPNETCORE_ENVIRONMENT

**Alternatives Considered**:
- OpenTelemetry only: Prometheus integration required for existing dashboards
- Custom metrics format: Non-standard, harder to integrate

---

## Summary of Decisions

| Topic | Decision | Impact |
|-------|----------|--------|
| Audit Trail | EF Core SaveChangesInterceptor | Automatic, centralized logging |
| Caching | Redis with IDistributedCache | 80%+ cache hit rate target |
| Dependency Checks | Typed HttpClients with Polly | Fail-closed, resilient |
| Concurrency | RowVersion byte[] | Reliable optimistic locking |
| Onboarding | Simple state machine | Fixed transitions, validated |
| Rating Aggregates | On-demand with caching | Efficient for expected scale |
| Expiration Queries | Indexed date column | Direct query, fast |
| Events | MassTransit/RabbitMQ | Decoupled integration |
| Versioning | URL path versioning | Clear, debuggable |
| Metrics | Prometheus with labels | Constitution compliant |

All research items resolved. Ready for Phase 1: Design & Contracts.
