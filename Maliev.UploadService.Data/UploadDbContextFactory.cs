using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.UploadService.Data;

/// <summary>
/// Design-time factory for creating UploadDbContext instances during EF Core migrations
/// </summary>
public class UploadDbContextFactory : IDesignTimeDbContextFactory<UploadDbContext>
{
    public UploadDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UploadDbContext>();

        // Use a temporary connection string for migrations
        // In production, this is provided via appsettings.json
        var connectionString = "Host=localhost;Database=uploadservice_design;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new UploadDbContext(optionsBuilder.Options);
    }
}

