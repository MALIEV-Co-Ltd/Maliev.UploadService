using Maliev.UploadService.Data;
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

        Assert.False(hasChanges, "Run 'dotnet ef migrations add <Name> --project Maliev.UploadService.Data --startup-project Maliev.UploadService.Api'");
    }
}
