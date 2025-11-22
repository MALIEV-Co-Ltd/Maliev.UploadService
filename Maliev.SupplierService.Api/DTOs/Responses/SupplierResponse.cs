using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Api.DTOs.Responses;

public record SupplierResponse(
    Guid Id,
    string CompanyName,
    string TaxId,
    string Address,
    string City,
    string Country,
    string? PostalCode,
    SupplierStatus Status,
    OnboardingStage OnboardingStage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RowVersion
);
