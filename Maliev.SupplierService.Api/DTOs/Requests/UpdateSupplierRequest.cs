namespace Maliev.SupplierService.Api.DTOs.Requests;

public record UpdateSupplierRequest(
    string? CompanyName,
    string? Address,
    string? City,
    string? Country,
    string? PostalCode,
    IEnumerable<Guid>? MaterialCategoryIds,
    IEnumerable<string>? Capabilities,
    string RowVersion // Required for optimistic concurrency
);
