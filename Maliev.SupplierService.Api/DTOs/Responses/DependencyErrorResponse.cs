namespace Maliev.SupplierService.Api.DTOs.Responses;

public record DependencyErrorResponse(
    string Message,
    IReadOnlyList<DependencyInfo> Dependencies);

public record DependencyInfo(
    string ServiceName,
    int ReferenceCount,
    string? ErrorMessage = null);
