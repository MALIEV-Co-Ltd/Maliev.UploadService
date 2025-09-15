using Maliev.UploadService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.UploadService.Data.DbContexts;

public class UploadDbContext : DbContext
{
    public UploadDbContext(DbContextOptions<UploadDbContext> options) : base(options)
    {
    }

    public DbSet<UploadedFile> UploadedFiles { get; set; }
    public DbSet<FileAccessLog> FileAccessLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure UploadedFile
        modelBuilder.Entity<UploadedFile>(entity =>
        {
            entity.ToTable("uploaded_files");

            entity.HasKey(e => e.Id);

            // Indexes for performance
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("idx_uploaded_files_category");

            entity.HasIndex(e => e.EntityId)
                .HasDatabaseName("idx_uploaded_files_entity_id");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("idx_uploaded_files_customer_id");

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("idx_uploaded_files_order_id");

            entity.HasIndex(e => e.UploadedAt)
                .HasDatabaseName("idx_uploaded_files_uploaded_at");

            entity.HasIndex(e => new { e.Category, e.EntityId })
                .HasDatabaseName("idx_uploaded_files_category_entity");

            entity.HasIndex(e => e.ObjectName)
                .IsUnique()
                .HasDatabaseName("idx_uploaded_files_object_name_unique");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("idx_uploaded_files_is_deleted");

            // Enum conversions
            entity.Property(e => e.AccessLevel)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ProcessingStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            // JSON column for tags
            entity.Property(e => e.Tags)
                .HasColumnType("jsonb")
                .HasDefaultValue("[]");

            // Computed columns
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure FileAccessLog
        modelBuilder.Entity<FileAccessLog>(entity =>
        {
            entity.ToTable("file_access_logs");

            entity.HasKey(e => e.Id);

            // Foreign key relationship
            entity.HasOne(e => e.File)
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.FileId)
                .HasDatabaseName("idx_file_access_logs_file_id");

            entity.HasIndex(e => e.AccessedAt)
                .HasDatabaseName("idx_file_access_logs_accessed_at");

            entity.HasIndex(e => e.AccessType)
                .HasDatabaseName("idx_file_access_logs_access_type");

            entity.HasIndex(e => e.AccessedBy)
                .HasDatabaseName("idx_file_access_logs_accessed_by");

            // Enum conversion
            entity.Property(e => e.AccessType)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Default timestamp
            entity.Property(e => e.AccessedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}