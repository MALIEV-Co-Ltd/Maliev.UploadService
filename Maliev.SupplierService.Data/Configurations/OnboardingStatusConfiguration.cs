using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class OnboardingStatusConfiguration : IEntityTypeConfiguration<OnboardingStatus>
{
    public void Configure(EntityTypeBuilder<OnboardingStatus> builder)
    {
        builder.ToTable("onboarding_statuses");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(e => e.Stage)
            .HasColumnName("stage")
            .IsRequired();

        builder.Property(e => e.TransitionedAt)
            .HasColumnName("transitioned_at")
            .IsRequired();

        builder.Property(e => e.TransitionedBy)
            .HasColumnName("transitioned_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TransitionedByName)
            .HasColumnName("transitioned_by_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        // Foreign key
        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.OnboardingHistory)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SupplierId)
            .HasDatabaseName("ix_onboarding_statuses_supplier_id");

        builder.HasIndex(e => e.Stage)
            .HasDatabaseName("ix_onboarding_statuses_stage");

        builder.HasIndex(e => e.TransitionedAt)
            .HasDatabaseName("ix_onboarding_statuses_transitioned_at");
    }
}
