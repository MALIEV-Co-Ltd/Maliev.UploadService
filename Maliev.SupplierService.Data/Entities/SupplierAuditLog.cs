namespace Maliev.SupplierService.Data.Entities;

public class SupplierAuditLog
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public required string ChangeType { get; set; } // CREATE, UPDATE, DELETE
    public required string ChangedBy { get; set; }
    public required string ChangedByName { get; set; }
    public DateTime Timestamp { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
