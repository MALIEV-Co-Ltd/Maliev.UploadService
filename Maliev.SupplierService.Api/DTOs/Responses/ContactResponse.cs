namespace Maliev.SupplierService.Api.DTOs.Responses;

public record ContactResponse(
    Guid Id,
    string Name,
    string? Role,
    string Email,
    string? Phone,
    bool IsPrimary,
    DateTime CreatedAt
);
