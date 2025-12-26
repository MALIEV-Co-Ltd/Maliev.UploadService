using Maliev.UploadService.Api.Services;

namespace Maliev.UploadService.Api.BackgroundServices;

/// <summary>
/// Background service that periodically processes lifecycle policies
/// Handles file expiration and storage class transitions
/// </summary>
public class LifecyclePolicyWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LifecyclePolicyWorker> _logger;
    private readonly TimeSpan _processInterval = TimeSpan.FromHours(1); // Run every hour

    public LifecyclePolicyWorker(
        IServiceProvider serviceProvider,
        ILogger<LifecyclePolicyWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lifecycle Policy Worker starting");

        // Wait a bit before first run to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecyclePoliciesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing lifecycle policies");
            }

            // Wait for next interval
            try
            {
                await Task.Delay(_processInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when application is shutting down
                break;
            }
        }

        _logger.LogInformation("Lifecycle Policy Worker stopping");
    }

    private async Task ProcessLifecyclePoliciesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing lifecycle policies");

        using var scope = _serviceProvider.CreateScope();
        var lifecycleService = scope.ServiceProvider.GetRequiredService<ILifecycleManagementService>();

        try
        {
            // Process expired files
            var expiredCount = await lifecycleService.ProcessExpiredFilesAsync(cancellationToken);
            if (expiredCount > 0)
            {
                _logger.LogInformation("Processed {Count} expired files", expiredCount);
            }

            // Update storage classes based on age transitions
            var updatedCount = await lifecycleService.UpdateStorageClassesAsync(cancellationToken);
            if (updatedCount > 0)
            {
                _logger.LogInformation("Updated storage class for {Count} files", updatedCount);
            }

            _logger.LogDebug("Lifecycle policy processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in lifecycle policy processing");
            throw;
        }
    }
}

