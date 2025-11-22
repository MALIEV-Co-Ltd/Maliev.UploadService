namespace Maliev.SupplierService.Data.Entities;

public class SupplierContact
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public required string Name { get; set; }
    public string? Role { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
