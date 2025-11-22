namespace Maliev.SupplierService.Data.Entities;

public class SupplierCapability
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
