namespace Maliev.SupplierService.Api.DTOs.Responses;

public record CapabilityResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
);
