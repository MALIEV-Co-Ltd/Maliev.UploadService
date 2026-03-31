using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maliev.UploadService.Api.Data;

/// <summary>
/// Sample data seeder for development and testing environments.
/// Provides default authorization policies and retention policies.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Seeds sample authorization policies and retention policies if they don't exist.
    /// Only runs in Development or Staging environments.
    /// </summary>
    public static async Task SeedSamplePoliciesAsync(UploadDbContext context, ILogger logger, bool isDevelopment)
    {
        if (!isDevelopment)
        {
            logger.LogInformation("Skipping seed data in non-development environment");
            return;
        }

        logger.LogInformation("Checking for seed data...");

        // Check if sample policies already exist
        var existingPolicies = await context.ServiceAuthorizationPolicies
            .AnyAsync(p => p.ServiceId == "test-service" || p.ServiceId == "demo-service");

        if (existingPolicies)
        {
            logger.LogInformation("Sample policies already exist, skipping seed");
            return;
        }

        logger.LogInformation("Seeding sample authorization and retention policies...");

        // Sample Authorization Policies
        var testServicePolicy = new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string>
            {
                "test-service/",
                "test-service/uploads/",
                "test-service/documents/"
            },
            AllowedContentTypes = new List<string>
            {
                "text/plain",
                "application/pdf",
                "image/jpeg",
                "image/png",
                "application/json"
            },
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100 MB
            StorageQuotaBytes = 10L * 1024 * 1024 * 1024, // 10 GB
            AllowOverwrite = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var demoServicePolicy = new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "demo-service",
            ServiceName = "Demo Service (Restricted)",
            AllowedPathPrefixes = new List<string>
            {
                "demo-service/"
            },
            AllowedContentTypes = new List<string>
            {
                "image/jpeg",
                "image/png",
                "image/gif"
            },
            MaxFileSizeBytes = 10 * 1024 * 1024, // 10 MB
            StorageQuotaBytes = 1L * 1024 * 1024 * 1024, // 1 GB
            AllowOverwrite = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var apiServicePolicy = new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "api-gateway",
            ServiceName = "API Gateway (High Limits)",
            AllowedPathPrefixes = new List<string>
            {
                "api-gateway/",
                "api-gateway/uploads/",
                "api-gateway/cache/"
            },
            AllowedContentTypes = new List<string>
            {
                "text/plain",
                "application/pdf",
                "application/json",
                "application/xml",
                "image/jpeg",
                "image/png",
                "image/gif",
                "video/mp4",
                "application/zip"
            },
            MaxFileSizeBytes = 500 * 1024 * 1024, // 500 MB
            StorageQuotaBytes = 100L * 1024 * 1024 * 1024, // 100 GB
            AllowOverwrite = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.ServiceAuthorizationPolicies.AddRange(
            testServicePolicy,
            demoServicePolicy,
            apiServicePolicy
        );

        // Sample Retention Policies
        var shortTermPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Short-Term (30 days)",
            RetentionDays = 30,
            ApplyToPathPrefix = null, // Global policy
            ServiceId = null, // Applies to all services
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 7, StorageClass = "NEARLINE" }
            }
        };

        var mediumTermPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Medium-Term (90 days)",
            RetentionDays = 90,
            ApplyToPathPrefix = "test-service/documents/",
            ServiceId = "test-service",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 60, StorageClass = "COLDLINE" }
            }
        };

        var longTermPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Long-Term Archive (1 year)",
            RetentionDays = 365,
            ApplyToPathPrefix = "api-gateway/archive/",
            ServiceId = "api-gateway",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" },
                new StorageClassTransition { Days = 180, StorageClass = "ARCHIVE" }
            }
        };

        var indefinitePolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Indefinite (Legal Hold)",
            RetentionDays = 0, // 0 = indefinite
            ApplyToPathPrefix = "api-gateway/legal/",
            ServiceId = "api-gateway",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 90, StorageClass = "ARCHIVE" }
            }
        };

        // Bucket-specific retention policies for multi-bucket GCS architecture
        var customerDocumentsPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Customer Documents (7 years)",
            RetentionDays = 2555, // ~7 years
            ApplyToPathPrefix = "customer-",
            ServiceId = null, // Applies across services
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" }
            }
        };

        var financialRecordsPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Financial Records (Permanent)",
            RetentionDays = 0, // Indefinite
            ApplyToPathPrefix = null, // Matched by path containing /invoices/, /receipts/, /statements/
            ServiceId = "invoice-service",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 365, StorageClass = "COLDLINE" },
                new StorageClassTransition { Days = 1825, StorageClass = "ARCHIVE" } // 5 years
            }
        };

        var operationsPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Operations Documents (3 years)",
            RetentionDays = 1095, // ~3 years
            ApplyToPathPrefix = null, // Matched by path containing /orders/, /materials/, /quotations/
            ServiceId = "order-service",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" }
            }
        };

        var tempDevPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Temporary Files (30 days auto-delete)",
            RetentionDays = 30,
            ApplyToPathPrefix = "ai-extraction/",
            ServiceId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = null // STANDARD only, auto-delete after 30 days
        };

        var customerProjectFilesPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "Customer Project Files (7 years)",
            RetentionDays = 2555, // ~7 years
            ApplyToPathPrefix = "customers/",
            ServiceId = null, // Applies across services
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 90, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 365, StorageClass = "COLDLINE" },
                new StorageClassTransition { Days = 1825, StorageClass = "ARCHIVE" } // 5 years
            }
        };

        context.RetentionPolicies.AddRange(
            shortTermPolicy,
            mediumTermPolicy,
            longTermPolicy,
            indefinitePolicy,
            customerDocumentsPolicy,
            financialRecordsPolicy,
            operationsPolicy,
            tempDevPolicy,
            customerProjectFilesPolicy
        );

        await context.SaveChangesAsync();

        logger.LogInformation("Successfully seeded {AuthPolicyCount} authorization policies and {RetentionPolicyCount} retention policies",
            3, 9);
    }
}
