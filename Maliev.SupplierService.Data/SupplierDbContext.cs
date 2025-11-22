using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SupplierService.Data;

public class SupplierDbContext : DbContext
{
    public SupplierDbContext(DbContextOptions<SupplierDbContext> options)
        : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierContact> SupplierContacts => Set<SupplierContact>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<SupplierCapability> SupplierCapabilities => Set<SupplierCapability>();
    public DbSet<SupplierCertification> SupplierCertifications => Set<SupplierCertification>();
    public DbSet<PerformanceEvaluation> PerformanceEvaluations => Set<PerformanceEvaluation>();
    public DbSet<SupplierAuditLog> SupplierAuditLogs => Set<SupplierAuditLog>();
    public DbSet<OnboardingStatus> OnboardingStatuses => Set<OnboardingStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupplierDbContext).Assembly);

        // Explicitly ensure no concurrency token on Supplier
        modelBuilder.Entity<Supplier>().Property(s => s.UpdatedAt).IsConcurrencyToken(false);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is Supplier supplier)
                {
                    supplier.CreatedAt = now;
                    supplier.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierContact contact)
                {
                    contact.CreatedAt = now;
                    contact.UpdatedAt = now;
                }
                else if (entry.Entity is MaterialCategory category)
                {
                    category.CreatedAt = now;
                    category.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierCapability capability)
                {
                    capability.CreatedAt = now;
                    capability.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierCertification certification)
                {
                    certification.CreatedAt = now;
                    certification.UpdatedAt = now;
                }
                else if (entry.Entity is PerformanceEvaluation evaluation)
                {
                    evaluation.CreatedAt = now;
                }
                else if (entry.Entity is SupplierAuditLog auditLog)
                {
                    auditLog.Timestamp = now;
                }
                else if (entry.Entity is OnboardingStatus onboarding)
                {
                    onboarding.TransitionedAt = now;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is Supplier supplier)
                {
                    supplier.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierContact contact)
                {
                    contact.UpdatedAt = now;
                }
                else if (entry.Entity is MaterialCategory category)
                {
                    category.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierCapability capability)
                {
                    capability.UpdatedAt = now;
                }
                else if (entry.Entity is SupplierCertification certification)
                {
                    certification.UpdatedAt = now;
                }
            }
        }
    }
}
