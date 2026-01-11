using Maliev.UploadService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Maliev.Aspire.ServiceDefaults.Database;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Maliev.UploadService.Data;

public class UploadDbContext : DbContext
{
    public UploadDbContext(DbContextOptions<UploadDbContext> options)
        : base(options)
    {
    }

    public DbSet<Upload> Uploads { get; set; } = null!;
    public DbSet<FileMetadata> FileMetadata { get; set; } = null!;
    public DbSet<ServiceAuthorizationPolicy> ServiceAuthorizationPolicies { get; set; } = null!;
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; } = null!;
    public DbSet<UploadEvent> UploadEvents { get; set; } = null!;
    public DbSet<BulkDeleteJob> BulkDeleteJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JSON converters for Dictionary properties (works with both PostgreSQL and InMemory)
        var dictionaryConverter = new ValueConverter<Dictionary<string, string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.Count == c2.Count && !c1.Except(c2).Any()),
            c => c == null ? 0 : c.OrderBy(kvp => kvp.Key).Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
            c => c == null ? null : new Dictionary<string, string>(c));

        // Nullable list converter
        var stringListConverterNullable = new ValueConverter<List<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

        var stringListComparerNullable = new ValueComparer<List<string>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? null : c.ToList());

        // Non-nullable list converter
        var stringListConverter = new ValueConverter<List<string>, string?>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v!, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var storageClassTransitionListConverter = new ValueConverter<List<StorageClassTransition>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<StorageClassTransition>>(v, (JsonSerializerOptions?)null));

        var storageClassTransitionListComparer = new ValueComparer<List<StorageClassTransition>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Days.GetHashCode(), v.StorageClass.GetHashCode())),
            c => c == null ? null : c.ToList());

        // Upload configuration
        modelBuilder.Entity<Upload>(entity =>
        {
            entity.ToTable("uploads");
            entity.HasKey(e => e.UploadId);
            entity.Property(e => e.UploadId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Metadata)
                .HasConversion(dictionaryConverter, dictionaryComparer)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).HasDatabaseName("idx_uploads_service_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_uploads_status");
            entity.HasIndex(e => e.UploadedAt).HasDatabaseName("idx_uploads_uploaded_at");
            entity.HasIndex(e => e.StoragePath).IsUnique().HasDatabaseName("idx_uploads_storage_path");
        });

        // FileMetadata configuration
        modelBuilder.Entity<FileMetadata>(entity =>
        {
            entity.ToTable("file_metadata");
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.FileId).ValueGeneratedNever();
            entity.Property(e => e.Metadata)
                .HasConversion(dictionaryConverter, dictionaryComparer)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).HasDatabaseName("idx_filemetadata_service_id");
            entity.HasIndex(e => e.StoragePath).IsUnique().HasDatabaseName("idx_filemetadata_storage_path");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("idx_filemetadata_expires_at");
            entity.HasIndex(e => e.UploadedAt).HasDatabaseName("idx_filemetadata_uploaded_at");
            entity.HasOne<Upload>()
                .WithOne()
                .HasForeignKey<FileMetadata>(e => e.UploadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ServiceAuthorizationPolicy configuration
        modelBuilder.Entity<ServiceAuthorizationPolicy>(entity =>
        {
            entity.ToTable("service_authorization_policies");
            entity.HasKey(e => e.PolicyId);
            entity.Property(e => e.PolicyId).ValueGeneratedNever();
            entity.Property(e => e.AllowedPathPrefixes)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("jsonb");
            entity.Property(e => e.AllowedContentTypes)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).IsUnique().HasDatabaseName("idx_authz_policy_service_id");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_authz_policy_is_active");
        });

        // RetentionPolicy configuration
        modelBuilder.Entity<RetentionPolicy>(entity =>
        {
            entity.ToTable("retention_policies");
            entity.HasKey(e => e.PolicyId);
            entity.Property(e => e.PolicyId).ValueGeneratedNever();
            entity.Property(e => e.StorageClassTransitions)
                .HasConversion(storageClassTransitionListConverter, storageClassTransitionListComparer)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).HasDatabaseName("idx_retention_policy_service_id");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_retention_policy_is_active");
        });

        // UploadEvent configuration
        modelBuilder.Entity<UploadEvent>(entity =>
        {
            entity.ToTable("upload_events");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).ValueGeneratedNever();
            entity.Property(e => e.EventType).HasConversion<string>();
            entity.Property(e => e.EventResult).HasConversion<string>();
            entity.Property(e => e.Metadata)
                .HasConversion(dictionaryConverter, dictionaryComparer)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).HasDatabaseName("idx_upload_events_service_id");
            entity.HasIndex(e => e.EventType).HasDatabaseName("idx_upload_events_event_type");
            entity.HasIndex(e => e.EventTimestamp).HasDatabaseName("idx_upload_events_timestamp");
            entity.HasIndex(e => e.UploadId).HasDatabaseName("idx_upload_events_upload_id");
            entity.HasIndex(e => e.FileId).HasDatabaseName("idx_upload_events_file_id");
        });

        // BulkDeleteJob configuration
        modelBuilder.Entity<BulkDeleteJob>(entity =>
        {
            entity.ToTable("bulk_delete_jobs");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.ErrorDetails)
                .HasConversion(stringListConverterNullable, stringListComparerNullable)
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.ServiceId).HasDatabaseName("idx_bulk_delete_service_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_bulk_delete_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_bulk_delete_created_at");
        });

        // Apply PostgreSQL snake_case naming convention globally
        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);

        // Seed default policy for geometry-service (required for integration tests and platform initialization)
        modelBuilder.Entity<ServiceAuthorizationPolicy>().HasData(new ServiceAuthorizationPolicy
        {
            PolicyId = "policy-geometry-service",
            ServiceId = "geometry-service",
            ServiceName = "Geometry Analysis Service",
            AllowedPathPrefixes = new List<string> { "geometry-test", "geometry/" },
            AllowedContentTypes = new List<string> { "application/octet-stream", "model/stl", "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
            StorageQuotaBytes = 1024 * 1024 * 1024, // 1GB
            AllowOverwrite = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });
    }
}
