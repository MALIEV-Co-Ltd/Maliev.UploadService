using Maliev.UploadService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.UploadService.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly UploadDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(UploadDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if using in-memory database (for testing)
            if (_context.Database.IsInMemory())
            {
                // For in-memory databases, just check if we can access the context
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("In-memory database connection is healthy")
                    : HealthCheckResult.Unhealthy("In-memory database connection failed");
            }
            else
            {
                // For relational databases, execute a simple query
                await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                return HealthCheckResult.Healthy("Database connection is healthy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}