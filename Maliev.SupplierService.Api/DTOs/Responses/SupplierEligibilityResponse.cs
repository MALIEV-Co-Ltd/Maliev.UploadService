namespace Maliev.SupplierService.Api.DTOs.Responses;

public record SupplierEligibilityResponse(
    Guid SupplierId,
    bool IsEligible,
    IReadOnlyList<string> Reasons
);
