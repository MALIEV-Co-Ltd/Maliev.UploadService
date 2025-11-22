namespace Maliev.SupplierService.Api.DTOs.Requests;

using Maliev.SupplierService.Data.Enums;

public record UpdateStatusRequest(
    SupplierStatus Status,
    string? Reason
);
