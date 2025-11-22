namespace Maliev.SupplierService.Api.DTOs.Requests;

public record UpdateContactRequest(
    string? Name,
    string? Email,
    string? Role,
    string? Phone,
    bool? IsPrimary
);
