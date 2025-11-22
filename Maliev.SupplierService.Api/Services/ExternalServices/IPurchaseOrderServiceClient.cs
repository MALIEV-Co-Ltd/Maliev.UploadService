namespace Maliev.SupplierService.Api.Services.ExternalServices;

public interface IPurchaseOrderServiceClient
{
    Task<DependencyCheckResult> CheckReferencesAsync(Guid supplierId, CancellationToken cancellationToken = default);
}

public record DependencyCheckResult(
    bool HasReferences,
    string ServiceName,
    int ReferenceCount,
    string? ErrorMessage,
    bool ServiceUnavailable);
