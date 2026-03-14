using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.UploadService.Tests.Fixtures;

[CollectionDefinition("TestDatabase")]
public class TestDatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
}

public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .WithDatabase("uploadservice_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
#pragma warning restore CS0618

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public UploadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        var context = new UploadDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
