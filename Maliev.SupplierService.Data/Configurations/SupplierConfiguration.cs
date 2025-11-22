using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.TaxId)
            .HasColumnName("tax_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Address)
            .HasColumnName("address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.City)
            .HasColumnName("city")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Country)
            .HasColumnName("country")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(20);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(e => e.OnboardingStage)
            .HasColumnName("onboarding_stage")
            .IsRequired();

        builder.Property(e => e.LastOrderDate)
            .HasColumnName("last_order_date");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Concurrency control removed for simplicity - use UpdatedAt for optimistic locking if needed

        // Indexes
        builder.HasIndex(e => e.TaxId)
            .IsUnique()
            .HasDatabaseName("ix_suppliers_tax_id");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_suppliers_status");

        builder.HasIndex(e => e.CompanyName)
            .HasDatabaseName("ix_suppliers_company_name");

        builder.HasIndex(e => e.OnboardingStage)
            .HasDatabaseName("ix_suppliers_onboarding_stage");

        // Many-to-many relationship with MaterialCategory
        builder.HasMany(e => e.MaterialCategories)
            .WithMany(e => e.Suppliers)
            .UsingEntity<Dictionary<string, object>>(
                "supplier_material_categories",
                j => j.HasOne<MaterialCategory>()
                    .WithMany()
                    .HasForeignKey("material_category_id")
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Supplier>()
                    .WithMany()
                    .HasForeignKey("supplier_id")
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("supplier_id", "material_category_id");
                    j.HasIndex("material_category_id")
                        .HasDatabaseName("ix_supplier_material_categories_category_id");
                });
    }
}
