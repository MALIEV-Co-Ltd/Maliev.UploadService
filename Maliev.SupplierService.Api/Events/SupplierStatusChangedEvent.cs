namespace Maliev.SupplierService.Api.Events;

using Maliev.SupplierService.Data.Enums;

public record SupplierStatusChangedEvent(
    Guid SupplierId,
    SupplierStatus OldStatus,
    SupplierStatus NewStatus,
    DateTime ChangedAt,
    string ChangedBy
);
