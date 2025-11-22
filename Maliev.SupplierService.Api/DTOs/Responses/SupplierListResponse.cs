namespace Maliev.SupplierService.Api.DTOs.Responses;

public record SupplierListResponse(
    IReadOnlyList<SupplierResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
