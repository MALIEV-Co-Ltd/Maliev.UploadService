namespace Maliev.SupplierService.Api.DTOs.Responses;

public record ValidationErrorResponse(
    string Type,
    string Title,
    int Status,
    IDictionary<string, string[]> Errors);
