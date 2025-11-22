namespace Maliev.SupplierService.Api.DTOs.Responses;

public record ErrorResponse(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    string? Instance = null,
    IDictionary<string, object?>? Extensions = null);
