namespace Maliev.SupplierService.Api.Services;

public interface IAuditService
{
    Task LogChangeAsync(
        Guid supplierId,
        string changeType,
        string entityType,
        Guid entityId,
        object? oldValues,
        object? newValues,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);
}
