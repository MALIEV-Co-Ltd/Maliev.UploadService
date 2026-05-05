using Maliev.UploadService.Application.Interfaces;
using Maliev.MessagingContracts.Contracts.Uploads;
using MassTransit;

namespace Maliev.UploadService.Api.Consumers;

/// <summary>
/// MassTransit consumer for processing bulk delete jobs (FR-033)
/// </summary>
public class BulkDeleteJobConsumer : IConsumer<BulkDeleteJobCommand>
{
    private readonly IBulkDeleteService _bulkDeleteService;
    private readonly ILogger<BulkDeleteJobConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the BulkDeleteJobConsumer class.
    /// </summary>
    /// <param name="bulkDeleteService">The bulk delete service.</param>
    /// <param name="logger">The logger for this consumer.</param>
    public BulkDeleteJobConsumer(
        IBulkDeleteService bulkDeleteService,
        ILogger<BulkDeleteJobConsumer> logger)
    {
        _bulkDeleteService = bulkDeleteService;
        _logger = logger;
    }

    /// <summary>
    /// Consumes and processes a bulk delete job message.
    /// </summary>
    /// <param name="context">The consume context containing the message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Consume(ConsumeContext<BulkDeleteJobCommand> context)
    {
        var jobId = context.Message.Payload.JobId;

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
