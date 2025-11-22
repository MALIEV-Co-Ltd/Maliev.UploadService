namespace Maliev.SupplierService.Api.DTOs.Requests;

public record CreateContactRequest(
    string Name,
    string Email,
    string? Role,
    string? Phone,
    bool IsPrimary = false
);
