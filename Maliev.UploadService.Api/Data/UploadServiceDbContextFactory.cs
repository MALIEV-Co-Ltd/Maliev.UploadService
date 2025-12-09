using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.UploadService.Api.Data;

/// <summary>
/// Design-time factory for creating UploadServiceDbContext instances during EF Core migrations
/// </summary>
public class UploadServiceDbContextFactory : IDesignTimeDbContextFactory<UploadServiceDbContext>
{
    public UploadServiceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UploadServiceDbContext>();

        // Use a temporary connection string for migrations
        // In production, this is provided via appsettings.json
        var connectionString = "Host=localhost;Database=uploadservice_design;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new UploadServiceDbContext(optionsBuilder.Options);
    }
}
