namespace Maliev.SupplierService.Api.DTOs.Requests;

using Maliev.SupplierService.Data.Enums;

public record CreateCertificationRequest(
    CertificationType DocumentType,
    string DocumentName,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    string? ExternalFileRef,
    string? Notes
);
