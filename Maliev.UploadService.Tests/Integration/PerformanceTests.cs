using Maliev.Aspire.ServiceDefaults.IAM;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// Performance and load tests for the Upload Service.
/// Tests concurrent upload handling, throughput, and resource utilization.
/// </summary>
[Collection("Database")]
public class PerformanceTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private HttpClient _client = null!;

    public PerformanceTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        var token = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ConcurrentUploads_10Files_AllSucceed()
    {
        // Arrange
        const int concurrentUploads = 10;
        var uploadTasks = new List<Task<(HttpStatusCode StatusCode, string? UploadId)>>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Upload 10 files concurrently
        for (int i = 0; i < concurrentUploads; i++)
        {
            var fileIndex = i;
            var task = Task.Run(async () =>
            {
                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes($"Concurrent test file {fileIndex}"));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                content.Add(fileContent, "File", $"concurrent-{fileIndex}.txt");
                content.Add(new StringContent($"test-service/perf/concurrent-{fileIndex}.txt"), "Path");
                content.Add(new StringContent("test-service"), "ServiceName");

                var response = await _client.PostAsync("/upload/v1/uploads", content);
                var uploadResult = await response.Content.ReadFromJsonAsync<UploadResponse>();

                return (response.StatusCode, uploadResult?.UploadId);
            });
            uploadTasks.Add(task);
        }

        var results = await Task.WhenAll(uploadTasks);
        stopwatch.Stop();

        // Assert
        Assert.All(results, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        Assert.All(results, result => Assert.NotNull(result.UploadId));

        _output.WriteLine($"Uploaded {concurrentUploads} files concurrently in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average time per upload: {stopwatch.ElapsedMilliseconds / concurrentUploads}ms");

        // Cleanup
        var cleanupTasks = results
            .Where(r => r.UploadId != null)
            .Select(r => _client.DeleteAsync($"/upload/v1/files/{r.UploadId}"));
        await Task.WhenAll(cleanupTasks);
    }

    [Fact]
    public async Task ConcurrentUploads_50Files_MaintainsThroughput()
    {
        // Arrange
        const int concurrentUploads = 50;
        var uploadTasks = new List<Task<(bool Success, long ElapsedMs)>>();
        var overallStopwatch = Stopwatch.StartNew();

        // Act - Upload 50 files with individual timing
        for (int i = 0; i < concurrentUploads; i++)
        {
            var fileIndex = i;
            var task = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes($"Load test file {fileIndex} - {new string('x', 1024)}"));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                content.Add(fileContent, "File", $"load-{fileIndex}.txt");
                content.Add(new StringContent($"test-service/perf/load-{fileIndex}.txt"), "Path");
                content.Add(new StringContent("test-service"), "ServiceName");

                var response = await _client.PostAsync("/upload/v1/uploads", content);
                sw.Stop();

                return (response.IsSuccessStatusCode, sw.ElapsedMilliseconds);
            });
            uploadTasks.Add(task);
        }

        var results = await Task.WhenAll(uploadTasks);
        overallStopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        var averageLatency = results.Average(r => r.ElapsedMs);
        var maxLatency = results.Max(r => r.ElapsedMs);
        var minLatency = results.Min(r => r.ElapsedMs);

        _output.WriteLine($"Total time: {overallStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Successful uploads: {successCount}/{concurrentUploads}");
        _output.WriteLine($"Average latency: {averageLatency:F2}ms");
        _output.WriteLine($"Min latency: {minLatency}ms");
        _output.WriteLine($"Max latency: {maxLatency}ms");
        _output.WriteLine($"Throughput: {(concurrentUploads * 1000.0 / overallStopwatch.ElapsedMilliseconds):F2} uploads/sec");

        // Assert at least 90% success rate
        Assert.True(successCount >= concurrentUploads * 0.9,
            $"Expected at least 90% success rate, got {successCount}/{concurrentUploads}");

        // Assert reasonable average latency (under 5 seconds for small files)
        Assert.True(averageLatency < 5000,
            $"Average latency too high: {averageLatency}ms");
    }

    [Fact]
    public async Task ConcurrentReads_20Files_EfficientRetrieval()
    {
        // Arrange - Upload a file first
        var setupContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Shared read test file"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        setupContent.Add(fileContent, "File", "shared.txt");
        setupContent.Add(new StringContent("test-service/perf/shared-read.txt"), "Path");
        setupContent.Add(new StringContent("test-service"), "ServiceName");

        var setupResponse = await _client.PostAsync("/upload/v1/uploads", setupContent);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var uploadId = setupResult!.UploadId;

        // Act - Perform 20 concurrent reads of the same file
        const int concurrentReads = 20;
        var stopwatch = Stopwatch.StartNew();
        var readTasks = Enumerable.Range(0, concurrentReads)
            .Select(_ => _client.GetAsync($"/upload/v1/files/{uploadId}"))
            .ToList();

        var responses = await Task.WhenAll(readTasks);
        stopwatch.Stop();

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        _output.WriteLine($"Performed {concurrentReads} concurrent reads in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average time per read: {stopwatch.ElapsedMilliseconds / concurrentReads}ms");

        // Cleanup
        await _client.DeleteAsync($"/upload/v1/files/{uploadId}");
    }

    [Fact]
    public async Task MixedWorkload_UploadQueryDelete_HandlesGracefully()
    {
        // Arrange
        const int operations = 30; // 10 uploads, 10 queries, 10 deletes
        var uploadIds = new List<string>();
        var stopwatch = Stopwatch.StartNew();

        // Phase 1: Upload 10 files
        var uploadTasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var content = new MultipartFormDataContent();
            var fileBytes = Encoding.UTF8.GetBytes($"Mixed workload file {i}");
            var fileContentPart = new ByteArrayContent(fileBytes);
            fileContentPart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContentPart, "File", $"mixed-{i}.txt");
            content.Add(new StringContent($"test-service/perf/mixed-{i}.txt"), "Path");
            content.Add(new StringContent("test-service"), "ServiceName");

            var response = await _client.PostAsync("/upload/v1/uploads", content);
            var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
            return result?.UploadId;
        }).ToList();

        uploadIds.AddRange((await Task.WhenAll(uploadTasks)).Where(id => id != null)!);

        // Phase 2: Concurrent queries and metadata retrieval
        var queryTasks = uploadIds.Take(10).Select(id =>
            _client.GetAsync($"/upload/v1/files/{id}")
        ).ToList();

        var queryResponses = await Task.WhenAll(queryTasks);

        // Phase 3: Concurrent deletes
        var deleteTasks = uploadIds.Select(id =>
            _client.DeleteAsync($"/upload/v1/files/{id}")
        ).ToList();

        var deleteResponses = await Task.WhenAll(deleteTasks);
        stopwatch.Stop();

        // Assert
        Assert.All(queryResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.All(deleteResponses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));

        _output.WriteLine($"Mixed workload ({operations} operations) completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average operation time: {stopwatch.ElapsedMilliseconds / operations}ms");
    }

    [Fact]
    public async Task LargeFileUpload_5MB_CompletesWithinTimeout()
    {
        // Arrange
        const int fileSizeBytes = 5 * 1024 * 1024; // 5 MB
        var largeFileBytes = new byte[fileSizeBytes];
        new Random().NextBytes(largeFileBytes);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(largeFileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "large-file.bin");
        content.Add(new StringContent("test-service/perf/large-file.bin"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.PostAsync("/upload/v1/uploads", content);
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);
        Assert.Equal(fileSizeBytes, result.FileSize);

        _output.WriteLine($"Uploaded {fileSizeBytes / (1024 * 1024)}MB file in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {(fileSizeBytes / 1024.0 / 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0):F2} MB/s");

        // Cleanup
        await _client.DeleteAsync($"/upload/v1/files/{result.UploadId}");
    }

    private string GenerateJwtToken(string serviceName, string audience)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName),
            new Claim("permission", "upload.files.upload"),
            new Claim("permission", "upload.files.download"),
            new Claim("permission", "upload.files.read"),
            new Claim("permission", "upload.files.delete"),
            new Claim("permission", "upload.files.list"),
            new Claim("permission", "upload.admin.manage-policies"),
            new Claim("permission", "upload.admin.bulk-delete"),
            new Claim("permission", "upload.admin.view-metrics"),
            new Claim("permission", "upload.retention.configure"),
            new Claim("permission", "upload.retention.execute")
        };

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _factory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
