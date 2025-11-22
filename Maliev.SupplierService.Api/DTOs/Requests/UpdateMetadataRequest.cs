namespace Maliev.SupplierService.Api.DTOs.Requests;

public record UpdateMetadataRequest(
    DateTime? LastOrderDate,
    decimal? TotalOrderValue
);
