using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Data.Entities;

public class OnboardingStatus
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public OnboardingStage Stage { get; set; }
    public DateTime TransitionedAt { get; set; }
    public required string TransitionedBy { get; set; }
    public required string TransitionedByName { get; set; }
    public string? Notes { get; set; }

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
