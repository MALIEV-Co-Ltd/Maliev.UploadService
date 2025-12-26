using Maliev.Aspire.ServiceDefaults.IAM;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Data;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Performance;

public class AuthPerformanceTests
{
    private readonly Mock<IIamServiceClient> _iamClientMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<UploadDbContext> _dbContextMock = new();
    private readonly Mock<ILogger<AuthorizationPolicyService>> _loggerMock = new();
    private readonly UploadMetrics _metrics;

    public AuthPerformanceTests()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["Service:Name"]).Returns("UploadService");
        
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(x => x.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("test"));

        _metrics = new UploadMetrics(meterFactoryMock.Object, configMock.Object);
    }

    [Fact]
    public async Task AuthorizationCheck_Cached_ShouldBeUnder10ms()
    {
        // Arrange
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AuthorizationPolicyService(
            null!, // DbContext not used for IAM check
            _cacheMock.Object,
            _loggerMock.Object,
            new ConfigurationBuilder().Build(),
            _iamClientMock.Object,
            _metrics);

        // Warm up
        await service.CanUploadToPathAsync("test-service", "test/path");

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await service.CanUploadToPathAsync("test-service", "test/path");
        }
        sw.Stop();

        var averageLatency = sw.Elapsed.TotalMilliseconds / 100;

        // Assert
        Assert.True(averageLatency < 10, $"Average latency {averageLatency}ms exceeded 10ms target");
    }
}


