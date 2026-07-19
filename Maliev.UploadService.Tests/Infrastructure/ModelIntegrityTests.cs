using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.UploadService.Tests.Infrastructure;

public class ModelIntegrityTests
{
    [Fact]
    public void Model_ShouldNotHavePendingChanges()
    {
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelCheck")
            .Options;

        using var context = new UploadDbContext(options);
        var hasChanges = context.Database.HasPendingModelChanges();

        Assert.False(hasChanges, "Run 'dotnet ef migrations add <Name> --project Maliev.UploadService.Infrastructure --startup-project Maliev.UploadService.Api'");
    }

    [Fact]
    public void Model_ShouldIncludeMassTransitOutboxEntities()
    {
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelCheck")
            .Options;

        using var context = new UploadDbContext(options);
        var entityNames = context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("MassTransit.EntityFrameworkCoreIntegration.InboxState", entityNames);
        Assert.Contains("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage", entityNames);
        Assert.Contains("MassTransit.EntityFrameworkCoreIntegration.OutboxState", entityNames);
    }
}
