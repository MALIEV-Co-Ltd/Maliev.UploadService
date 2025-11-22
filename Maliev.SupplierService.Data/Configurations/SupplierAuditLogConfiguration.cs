using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class SupplierAuditLogConfiguration : IEntityTypeConfiguration<SupplierAuditLog>
{
    public void Configure(EntityTypeBuilder<SupplierAuditLog> builder)
    {
        builder.ToTable("supplier_audit_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(e => e.ChangeType)
            .HasColumnName("change_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ChangedBy)
            .HasColumnName("changed_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ChangedByName)
            .HasColumnName("changed_by_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(e => e.OldValues)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(e => e.NewValues)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        // Foreign key
        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.AuditLogs)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SupplierId)
            .HasDatabaseName("ix_supplier_audit_logs_supplier_id");

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_supplier_audit_logs_timestamp");

        builder.HasIndex(e => e.ChangedBy)
            .HasDatabaseName("ix_supplier_audit_logs_changed_by");

        builder.HasIndex(e => new { e.EntityType, e.EntityId })
            .HasDatabaseName("ix_supplier_audit_logs_entity_type_entity_id");
    }
}
