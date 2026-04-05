using Maliev.UploadService.Api.Consumers;
using Maliev.UploadService.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Consumers;

public class BulkDeleteJobConsumerTests
{
    private readonly Mock<IBulkDeleteService> _bulkDeleteServiceMock;
    private readonly Mock<ILogger<BulkDeleteJobConsumer>> _loggerMock;
    private readonly BulkDeleteJobConsumer _consumer;

    public BulkDeleteJobConsumerTests()
    {
        _bulkDeleteServiceMock = new Mock<IBulkDeleteService>();
        _loggerMock = new Mock<ILogger<BulkDeleteJobConsumer>>();
        _consumer = new BulkDeleteJobConsumer(_bulkDeleteServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_WithValidJobId_ProcessesSuccessfully()
    {
        var jobId = Guid.NewGuid().ToString();
        var message = new BulkDeleteJobMessage { JobId = jobId };
        var contextMock = new Mock<ConsumeContext<BulkDeleteJobMessage>>();
        contextMock.Setup(c => c.Message).Returns(message);
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _bulkDeleteServiceMock
            .Setup(s => s.ProcessBulkDeleteJobAsync(jobId, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await _consumer.Consume(contextMock.Object);

        _bulkDeleteServiceMock.Verify(
            s => s.ProcessBulkDeleteJobAsync(jobId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenServiceThrows_ReThrowsException()
    {
        var jobId = Guid.NewGuid().ToString();
        var message = new BulkDeleteJobMessage { JobId = jobId };
        var contextMock = new Mock<ConsumeContext<BulkDeleteJobMessage>>();
        contextMock.Setup(c => c.Message).Returns(message);
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _bulkDeleteServiceMock
            .Setup(s => s.ProcessBulkDeleteJobAsync(jobId, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Processing failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _consumer.Consume(contextMock.Object));
    }

    [Fact]
    public async Task Consume_WithEmptyJobId_StillProcesses()
    {
        var jobId = "";
        var message = new BulkDeleteJobMessage { JobId = jobId };
        var contextMock = new Mock<ConsumeContext<BulkDeleteJobMessage>>();
        contextMock.Setup(c => c.Message).Returns(message);
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _bulkDeleteServiceMock
            .Setup(s => s.ProcessBulkDeleteJobAsync(jobId, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await _consumer.Consume(contextMock.Object);

        _bulkDeleteServiceMock.Verify(
            s => s.ProcessBulkDeleteJobAsync(jobId, CancellationToken.None),
            Times.Once);
    }
}
