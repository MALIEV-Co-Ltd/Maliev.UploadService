using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

public class UploadBackgroundServiceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UploadBackgroundServiceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessLifecyclePoliciesAsync_ShouldRunSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<ILifecycleManagementService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LifecyclePolicyWorker>>();

        // Instantiate manually because the factory removes it from DI
        var worker = new LifecyclePolicyWorker(_factory.Services, logger);

        // Act & Assert
        await worker.ProcessLifecyclePoliciesAsync(default);

        Assert.True(true);
    }
}
