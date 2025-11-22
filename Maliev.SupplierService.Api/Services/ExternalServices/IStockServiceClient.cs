namespace Maliev.SupplierService.Api.Services.ExternalServices;

public interface IStockServiceClient
{
    Task<DependencyCheckResult> CheckReferencesAsync(Guid supplierId, CancellationToken cancellationToken = default);
}
