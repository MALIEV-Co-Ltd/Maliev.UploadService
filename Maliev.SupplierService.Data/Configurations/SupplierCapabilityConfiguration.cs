using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class SupplierCapabilityConfiguration : IEntityTypeConfiguration<SupplierCapability>
{
    public void Configure(EntityTypeBuilder<SupplierCapability> builder)
    {
        builder.ToTable("supplier_capabilities");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Foreign key
        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.Capabilities)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SupplierId)
            .HasDatabaseName("ix_supplier_capabilities_supplier_id");

        builder.HasIndex(e => e.Name)
            .HasDatabaseName("ix_supplier_capabilities_name");
    }
}
