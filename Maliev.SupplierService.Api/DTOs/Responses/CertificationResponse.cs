namespace Maliev.SupplierService.Api.DTOs.Responses;

public record CertificationResponse(
    Guid Id,
    string DocumentType,
    string DocumentName,
    DateOnly? IssueDate,
    DateOnly? ExpirationDate,
    string? ExternalFileRef,
    bool IsExpired,
    bool IsExpiringSoon,
    DateTime CreatedAt
);
