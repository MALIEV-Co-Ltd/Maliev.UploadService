using nClam;
using System.Net;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Dummy ClamAV client that always returns a clean scan result.
/// Used when ClamAV scanning is disabled in configuration.
/// </summary>
public class DummyClamClient : IClamClient
{
    private readonly ILogger<DummyClamClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyClamClient"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DummyClamClient(ILogger<DummyClamClient> logger)
    {
        _logger = logger;
        _logger.LogWarning("CRITICAL: DummyClamClient instantiated. Malware scanning is BYPASSED.");
    }

    /// <inheritdoc />
    public int Port { get; set; } = 3310;

    /// <inheritdoc />
    public string? Server { get; set; } = "localhost";

    /// <inheritdoc />
    public IPAddress? ServerIP { get; set; } = null;

    /// <inheritdoc />
    public int MaxChunkSize { get; set; } = 128 * 1024;

    /// <inheritdoc />
    public long MaxStreamSize { get; set; } = 0;

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(Stream fileStream, CancellationToken cancellationToken = default) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(Stream fileStream) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(byte[] fileData, CancellationToken cancellationToken = default) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(byte[] fileData) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> SendAndScanFileAsync(string filePath) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> PingAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> TryPingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> TryPingAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("Mock 1.0");

    /// <inheritdoc />
    public Task<string> GetVersionAsync() => Task.FromResult("Mock 1.0");

    /// <inheritdoc />
    public Task<ClamScanResult> ScanFileOnServerAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> ScanFileOnServerAsync(string filePath) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> ScanFileOnServerMultithreadedAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<ClamScanResult> ScanFileOnServerMultithreadedAsync(string filePath) => Task.FromResult(new ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Shutdown() => Task.CompletedTask;
}
