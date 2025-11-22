namespace Maliev.SupplierService.Api.DTOs.Responses;

public record MaterialCategoryResponse(
    Guid Id,
    string Name,
    string? Description
);
