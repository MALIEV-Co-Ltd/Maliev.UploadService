namespace Maliev.SupplierService.Api.DTOs.Responses;

public record ExpiringCertificationResponse(
    Guid CertificationId,
    Guid SupplierId,
    string SupplierName,
    string DocumentType,
    string DocumentName,
    DateOnly ExpirationDate,
    int DaysUntilExpiration
);

public record ExpiringCertificationsListResponse(
    IReadOnlyList<ExpiringCertificationResponse> Items,
    int TotalCount
);
