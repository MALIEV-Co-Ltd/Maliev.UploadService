using Maliev.UploadService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.UploadService.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UploadDbContext>
    {
        public UploadDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UploadDbContext>();

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UploadDbContext");

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Host=localhost;Port=5433;Database=upload_app_db;Username=postgres;Password=temp;SslMode=Disable";
            }

            optionsBuilder.UseNpgsql(connectionString);

            return new UploadDbContext(optionsBuilder.Options);
        }
    }
}