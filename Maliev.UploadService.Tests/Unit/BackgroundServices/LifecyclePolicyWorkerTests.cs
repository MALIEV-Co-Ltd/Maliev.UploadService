using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.BackgroundServices;

public class LifecyclePolicyWorkerTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<LifecyclePolicyWorker>> _loggerMock;
    private readonly Mock<ILifecycleManagementService> _lifecycleServiceMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;

    public LifecyclePolicyWorkerTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<LifecyclePolicyWorker>>();
        _lifecycleServiceMock = new Mock<ILifecycleManagementService>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(_scopeFactoryMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(ILifecycleManagementService)))
            .Returns(_lifecycleServiceMock.Object);
    }

    [Fact]
    public async Task ProcessLifecyclePoliciesAsync_WithExpiredFiles_ProcessesCorrectly()
    {
        _lifecycleServiceMock.Setup(s => s.ProcessExpiredFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _lifecycleServiceMock.Setup(s => s.UpdateStorageClassesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var worker = new LifecyclePolicyWorker(_serviceProviderMock.Object, _loggerMock.Object);
        await worker.ProcessLifecyclePoliciesAsync(CancellationToken.None);

        _lifecycleServiceMock.Verify(s => s.ProcessExpiredFilesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _lifecycleServiceMock.Verify(s => s.UpdateStorageClassesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLifecyclePoliciesAsync_WithNoExpiredFiles_StillUpdatesStorageClasses()
    {
        _lifecycleServiceMock.Setup(s => s.ProcessExpiredFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _lifecycleServiceMock.Setup(s => s.UpdateStorageClassesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var worker = new LifecyclePolicyWorker(_serviceProviderMock.Object, _loggerMock.Object);
        await worker.ProcessLifecyclePoliciesAsync(CancellationToken.None);

        _lifecycleServiceMock.Verify(s => s.ProcessExpiredFilesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _lifecycleServiceMock.Verify(s => s.UpdateStorageClassesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLifecyclePoliciesAsync_WhenServiceThrows_PropagatesException()
    {
        _lifecycleServiceMock.Setup(s => s.ProcessExpiredFilesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var worker = new LifecyclePolicyWorker(_serviceProviderMock.Object, _loggerMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            worker.ProcessLifecyclePoliciesAsync(CancellationToken.None));
    }
}
