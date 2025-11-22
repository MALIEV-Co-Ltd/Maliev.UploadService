namespace Maliev.SupplierService.Api.DTOs.Responses;

public record MaterialCategoryListResponse(
    IReadOnlyList<MaterialCategoryResponse> Categories
);
