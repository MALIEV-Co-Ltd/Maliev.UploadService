namespace Maliev.SupplierService.Api.Events;

public record SupplierCreatedEvent(
    Guid SupplierId,
    string CompanyName,
    string TaxId,
    string Country,
    DateTime CreatedAt,
    string CreatedBy
);
