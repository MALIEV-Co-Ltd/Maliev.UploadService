namespace Maliev.SupplierService.Data.Entities;

public class MaterialCategory
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property (many-to-many)
    public ICollection<Supplier> Suppliers { get; set; } = [];
}
