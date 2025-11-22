namespace Maliev.SupplierService.Api.DTOs.Requests;

public record CreateSupplierRequest(
    string CompanyName,
    string TaxId,
    string Address,
    string City,
    string Country,
    string? PostalCode,
    IEnumerable<Guid>? MaterialCategoryIds,
    IEnumerable<string>? Capabilities,
    CreateContactRequest? PrimaryContact
);
