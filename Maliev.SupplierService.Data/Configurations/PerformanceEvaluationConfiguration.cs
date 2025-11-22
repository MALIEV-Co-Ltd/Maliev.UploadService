using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SupplierService.Data.Configurations;

public class PerformanceEvaluationConfiguration : IEntityTypeConfiguration<PerformanceEvaluation>
{
    public void Configure(EntityTypeBuilder<PerformanceEvaluation> builder)
    {
        builder.ToTable("performance_evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SupplierId)
            .HasColumnName("supplier_id")
            .IsRequired();

        builder.Property(e => e.RatingCategory)
            .HasColumnName("rating_category")
            .IsRequired();

        builder.Property(e => e.Score)
            .HasColumnName("score")
            .IsRequired();

        builder.Property(e => e.EvaluationDate)
            .HasColumnName("evaluation_date")
            .IsRequired();

        builder.Property(e => e.EvaluatorId)
            .HasColumnName("evaluator_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EvaluatorName)
            .HasColumnName("evaluator_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Check constraint for score 1-5
        builder.ToTable(t => t.HasCheckConstraint("ck_performance_evaluations_score", "score >= 1 AND score <= 5"));

        // Foreign key
        builder.HasOne(e => e.Supplier)
            .WithMany(e => e.Evaluations)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.SupplierId)
            .HasDatabaseName("ix_performance_evaluations_supplier_id");

        builder.HasIndex(e => e.EvaluationDate)
            .HasDatabaseName("ix_performance_evaluations_evaluation_date");

        builder.HasIndex(e => e.RatingCategory)
            .HasDatabaseName("ix_performance_evaluations_rating_category");
    }
}
