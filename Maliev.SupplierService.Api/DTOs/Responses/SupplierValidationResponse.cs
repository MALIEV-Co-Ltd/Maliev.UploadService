namespace Maliev.SupplierService.Api.DTOs.Responses;

using Maliev.SupplierService.Data.Enums;

public record SupplierValidationResponse(
    Guid Id,
    string CompanyName,
    string TaxId,
    SupplierStatus Status,
    bool IsActive
);
