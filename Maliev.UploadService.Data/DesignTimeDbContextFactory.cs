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

            optionsBuilder.UseNpgsql(connectionString);

            return new UploadDbContext(optionsBuilder.Options);
        }
    }
}