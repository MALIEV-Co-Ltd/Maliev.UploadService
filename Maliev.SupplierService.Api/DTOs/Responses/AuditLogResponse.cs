namespace Maliev.SupplierService.Api.DTOs.Responses;

public record AuditLogResponse(
    Guid Id,
    string ChangeType,
    string EntityType,
    Guid EntityId,
    string? OldValues,
    string? NewValues,
    string ChangedBy,
    string ChangedByName,
    DateTime Timestamp
);

public record AuditLogListResponse(
    IReadOnlyList<AuditLogResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
