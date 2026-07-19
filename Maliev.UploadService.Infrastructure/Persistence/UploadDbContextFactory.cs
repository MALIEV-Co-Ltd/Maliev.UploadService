using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.UploadService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating UploadDbContext instances during EF Core migrations.
/// </summary>
public class UploadDbContextFactory : IDesignTimeDbContextFactory<UploadDbContext>
{
    /// <summary>
    /// Creates a new UploadDbContext instance for design-time tools such as EF Core migrations.
    /// </summary>
    /// <param name="args">Command-line arguments passed by the EF tools.</param>
    /// <returns>A configured <see cref="UploadDbContext"/> instance.</returns>
    public UploadDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UploadDbContext>();

        // Use a temporary connection string for migrations
        // In production, this is provided via environment variables
        var connectionString = "Host=localhost;Database=uploadservice_design;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new UploadDbContext(optionsBuilder.Options);
    }
}
