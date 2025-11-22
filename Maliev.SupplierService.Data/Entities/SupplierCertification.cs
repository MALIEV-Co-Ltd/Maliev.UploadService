using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Data.Entities;

public class SupplierCertification
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public CertificationType DocumentType { get; set; }
    public required string DocumentName { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? ExternalFileRef { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
