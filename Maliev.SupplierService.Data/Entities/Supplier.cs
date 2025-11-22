using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Data.Entities;

public class Supplier
{
    public Guid Id { get; set; }
    public required string CompanyName { get; set; }
    public required string TaxId { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string Country { get; set; }
    public string? PostalCode { get; set; }
    public SupplierStatus Status { get; set; } = SupplierStatus.PendingApproval;
    public OnboardingStage OnboardingStage { get; set; } = OnboardingStage.PendingApproval;
    public DateTime? LastOrderDate { get; set; }
    public decimal? TotalOrderValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<SupplierContact> Contacts { get; set; } = [];
    public ICollection<MaterialCategory> MaterialCategories { get; set; } = [];
    public ICollection<SupplierCapability> Capabilities { get; set; } = [];
    public ICollection<SupplierCertification> Certifications { get; set; } = [];
    public ICollection<PerformanceEvaluation> Evaluations { get; set; } = [];
    public ICollection<OnboardingStatus> OnboardingHistory { get; set; } = [];
    public ICollection<SupplierAuditLog> AuditLogs { get; set; } = [];
}
