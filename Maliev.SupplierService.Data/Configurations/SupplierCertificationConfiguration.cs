using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class SupplierCertificationConfiguration : IEntityTypeConfiguration<SupplierCertification>
{
    public void Configure(EntityTypeBuilder<SupplierCertification> builder)
    {
        builder.ToTable("supplier_certifications");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(e => e.DocumentType)
            .HasColumnName("document_type")
            .IsRequired();

        builder.Property(e => e.DocumentName)
            .HasColumnName("document_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(e => e.ExpirationDate)
            .HasColumnName("expiration_date");

        builder.Property(e => e.ExternalFileRef)
            .HasColumnName("external_file_ref")
            .HasMaxLength(500);

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Foreign key
        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.Certifications)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SupplierId)
            .HasDatabaseName("ix_supplier_certifications_supplier_id");

        builder.HasIndex(e => e.ExpirationDate)
            .HasDatabaseName("ix_supplier_certifications_expiration_date");

        builder.HasIndex(e => e.DocumentType)
            .HasDatabaseName("ix_supplier_certifications_document_type");
    }
}
