namespace Maliev.SupplierService.Api.DTOs.Requests;

using Maliev.SupplierService.Data.Enums;

public record ListSuppliersRequest(
    int Page = 1,
    int PageSize = 20,
    SupplierStatus? Status = null,
    Guid? CategoryId = null,
    string? Capability = null,
    string? Search = null,
    string SortBy = "CompanyName",
    string SortOrder = "asc"
);
