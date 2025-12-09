using MassTransit;
using Maliev.UploadService.Api.Services;

namespace Maliev.UploadService.Api.Consumers;

/// <summary>
/// T170: MassTransit consumer for processing bulk delete jobs (FR-033)
/// </summary>
public class BulkDeleteJobConsumer : IConsumer<BulkDeleteJobMessage>
{
    private readonly IBulkDeleteService _bulkDeleteService;
    private readonly ILogger<BulkDeleteJobConsumer> _logger;

    public BulkDeleteJobConsumer(
        IBulkDeleteService bulkDeleteService,
        ILogger<BulkDeleteJobConsumer> logger)
    {
        _bulkDeleteService = bulkDeleteService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BulkDeleteJobMessage> context)
    {
        var jobId = context.Message.JobId;

        _logger.LogInformation("Processing bulk delete job. JobId: {JobId}", jobId);

        try
        {
            await _bulkDeleteService.ProcessBulkDeleteJobAsync(jobId, context.CancellationToken);
            _logger.LogInformation("Bulk delete job processed successfully. JobId: {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk delete job. JobId: {JobId}", jobId);
            throw; // Re-throw to trigger MassTransit retry logic
        }
    }
}

public class BulkDeleteJobMessage
{
    public required string JobId { get; set; }
}
