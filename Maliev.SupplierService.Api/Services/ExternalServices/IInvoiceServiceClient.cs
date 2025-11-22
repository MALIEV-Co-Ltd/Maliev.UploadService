namespace Maliev.SupplierService.Api.Services.ExternalServices;

public interface IInvoiceServiceClient
{
    Task<DependencyCheckResult> CheckReferencesAsync(Guid supplierId, CancellationToken cancellationToken = default);
}
