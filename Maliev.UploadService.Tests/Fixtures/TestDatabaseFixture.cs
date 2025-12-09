using Maliev.UploadService.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Maliev.UploadService.Tests.Fixtures;

public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("uploadservice_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public UploadServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        var context = new UploadServiceDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
