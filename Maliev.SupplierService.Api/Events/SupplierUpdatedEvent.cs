namespace Maliev.SupplierService.Api.Events;

public record SupplierUpdatedEvent(
    Guid SupplierId,
    string CompanyName,
    IReadOnlyList<string> ChangedFields,
    DateTime UpdatedAt,
    string UpdatedBy
);
