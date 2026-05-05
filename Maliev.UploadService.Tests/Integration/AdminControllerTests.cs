using Maliev.Aspire.ServiceDefaults.IAM;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// Integration tests for AdminController (T162, T163, T164)
/// Tests bulk delete job initiation and status query
/// </summary>
[Collection("Database")]
public class AdminControllerTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private HttpClient _client = null!;
    private string _adminToken = null!;
    private string _nonAdminToken = null!;
    private readonly List<string> _testUploadIds = new();
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public AdminControllerTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Ensure we replace any existing registration
                var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IIamServiceClient));
                if (descriptor != null) services.Remove(descriptor);

                services.AddScoped(_ => _iamClientMock.Object);
            });
        });
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _adminToken = GenerateJwtToken("admin-service", "uploadservice", isAdmin: true);
        _nonAdminToken = GenerateJwtToken("test-service", "uploadservice", isAdmin: false);

        // Mock IAM for Admin token
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "admin-service",
            UploadPermissions.AdminBulkDelete,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "admin-service",
            UploadPermissions.StorageManage,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Mock IAM for Non-Admin token (explicit denial)
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service",
            UploadPermissions.AdminBulkDelete,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        for (var i = 0; i < 3; i++)
        {
            var uploadId = Guid.NewGuid().ToString();
            var fileId = Guid.NewGuid().ToString();
            var storagePath = $"test-service/bulk-delete/{Guid.NewGuid():N}-test-{i}.txt";

            dbContext.Uploads.Add(new Upload
            {
                UploadId = uploadId,
                ServiceId = "test-service",
                UserId = "test-user",
                FileName = $"test-{i}.txt",
                ContentType = "text/plain",
                FileSize = Encoding.UTF8.GetByteCount($"Test file {i}"),
                Checksum = $"checksum-{i}",
                StoragePath = storagePath,
                BytesUploaded = Encoding.UTF8.GetByteCount($"Test file {i}"),
                Status = UploadStatus.Completed,
                UploadedAt = DateTime.UtcNow.AddMinutes(-i),
                CompletedAt = DateTime.UtcNow.AddMinutes(-i)
            });

            dbContext.FileMetadata.Add(new FileMetadata
            {
                FileId = fileId,
                UploadId = uploadId,
                ServiceId = "test-service",
                StoragePath = storagePath,
                VersionETag = $"etag-{i}",
                FileSize = Encoding.UTF8.GetByteCount($"Test file {i}"),
                ContentType = "text/plain",
                Checksum = $"checksum-{i}",
                UploadedAt = DateTime.UtcNow.AddMinutes(-i),
                Metadata = new Dictionary<string, string>
                {
                    ["seeded_by"] = nameof(AdminControllerTests)
                }
            });

            _testUploadIds.Add(uploadId);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task InitiateBulkDelete_WithAdminRole_ReturnsAccepted()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var request = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            PathPrefix = "test-service/bulk-delete/",
            Reason = "Test cleanup"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkDeleteJobResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.JobId);
        Assert.Equal("Pending", result.Status);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task InitiateBulkDelete_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _nonAdminToken);
        var request = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            PathPrefix = "test-service/",
            Reason = "Test"
        };

        // Explicitly deny in IAM
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InitiateBulkDelete_WithUploadIds_ReturnsAccepted()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var request = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            UploadIds = _testUploadIds.Take(2).ToList(),
            Reason = "Specific files cleanup"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkDeleteJobResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.JobId);
    }

    [Fact]
    public async Task InitiateBulkDelete_WithDeleteOlderThan_ReturnsAccepted()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var request = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            PathPrefix = "test-service/",
            DeleteFilesOlderThan = DateTime.UtcNow.AddDays(-30),
            Reason = "Delete old files"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetBulkDeleteStatus_ExistingJob_ReturnsJob()
    {
        // Arrange - create a job first
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var createRequest = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            PathPrefix = "test-service/",
            Reason = "Test status check"
        };

        var createResponse = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", createRequest);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var createResult = await createResponse.Content.ReadFromJsonAsync<BulkDeleteJobResponse>();
        var jobId = createResult!.JobId;

        // Act
        var response = await _client.GetAsync($"/upload/v1/admin/bulk-delete/{jobId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkDeleteJobResponse>();
        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.NotNull(result.Status);
    }

    [Fact]
    public async Task GetBulkDeleteStatus_NonExistentJob_ReturnsNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var nonExistentJobId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync($"/upload/v1/admin/bulk-delete/{nonExistentJobId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBulkDeleteStatus_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _nonAdminToken);
        var jobId = Guid.NewGuid().ToString();

        // Explicitly deny in IAM
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await _client.GetAsync($"/upload/v1/admin/bulk-delete/{jobId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CopyFileWithMetadata_CreatesIndependentFileMetadata_AndLeavesSourceUnchanged()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        var sourceUploadId = _testUploadIds[0];
        var sourceFile = await dbContext.FileMetadata
            .SingleAsync(f => f.UploadId == sourceUploadId);
        var sourceUpload = await dbContext.Uploads
            .SingleAsync(u => u.UploadId == sourceUploadId);
        var originalPath = sourceFile.StoragePath;
        var originalFileId = sourceFile.FileId;
        var originalUploadId = sourceFile.UploadId;
        var destinationPath = $"test-service/reorder-copy/{Guid.NewGuid():N}.txt";

        var request = new
        {
            sourcePath = originalPath,
            destinationPath,
            fileName = sourceUpload.FileName,
            serviceName = sourceUpload.ServiceId,
            metadata = new Dictionary<string, string>
            {
                ["reorder_source_file_id"] = originalFileId
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/copy-file-with-metadata", request);

        // Assert
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var result = await response.Content.ReadFromJsonAsync<CopyFileWithMetadataResponseProbe>();
        Assert.NotNull(result);
        Assert.NotEqual(originalFileId, result.FileId);
        Assert.NotEqual(originalUploadId, result.UploadId);
        Assert.Equal(destinationPath, result.StoragePath);

        dbContext.ChangeTracker.Clear();
        var copiedFile = await dbContext.FileMetadata
            .SingleAsync(f => f.FileId == result.FileId);
        var copiedUpload = await dbContext.Uploads
            .SingleAsync(u => u.UploadId == result.UploadId);
        var unchangedSource = await dbContext.FileMetadata
            .SingleAsync(f => f.FileId == originalFileId);

        Assert.Equal(destinationPath, copiedFile.StoragePath);
        Assert.Equal(destinationPath, copiedUpload.StoragePath);
        Assert.Equal(sourceUpload.FileName, copiedUpload.FileName);
        Assert.Equal(sourceUpload.ServiceId, copiedFile.ServiceId);
        Assert.Equal(sourceFile.ContentType, copiedFile.ContentType);
        Assert.NotNull(copiedFile.Metadata);
        Assert.Equal(originalFileId, copiedFile.Metadata["reorder_source_file_id"]);
        Assert.Equal(originalPath, unchangedSource.StoragePath);
    }

    [Fact]
    public async Task MigrateProject_WhenCalledConcurrently_IsIdempotent()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var seededFileIds = new List<string>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
            for (var i = 0; i < 2; i++)
            {
                var uploadId = Guid.NewGuid().ToString();
                var fileId = Guid.NewGuid().ToString();
                var storagePath = $"projects/{projectId}/{Guid.NewGuid():N}-part-{i}.step";

                dbContext.Uploads.Add(new Upload
                {
                    UploadId = uploadId,
                    ServiceId = "Intranet",
                    UserId = "test-user",
                    FileName = $"part-{i}.step",
                    ContentType = "model/step",
                    FileSize = 1024,
                    Checksum = $"checksum-project-migration-{i}",
                    StoragePath = storagePath,
                    BytesUploaded = 1024,
                    Status = UploadStatus.Completed,
                    UploadedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

                dbContext.FileMetadata.Add(new FileMetadata
                {
                    FileId = fileId,
                    UploadId = uploadId,
                    ServiceId = "Intranet",
                    StoragePath = storagePath,
                    VersionETag = $"etag-project-migration-{i}",
                    FileSize = 1024,
                    ContentType = "model/step",
                    Checksum = $"checksum-project-migration-{i}",
                    UploadedAt = DateTime.UtcNow
                });

                seededFileIds.Add(fileId);
            }

            await dbContext.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        var url = $"/upload/v1/admin/migrate-project/{projectId}?customerId={customerId}";

        var responses = await Task.WhenAll(
            _client.PostAsync(url, null),
            _client.PostAsync(url, null));

        foreach (var response in responses)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("DbUpdateConcurrencyException", responseBody, StringComparison.Ordinal);
        }

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<UploadDbContext>();
        var paths = await verifyDbContext.FileMetadata
            .Where(file => seededFileIds.Contains(file.FileId))
            .Select(file => file.StoragePath)
            .ToListAsync();

        Assert.Equal(seededFileIds.Count, paths.Count);
        Assert.All(paths, path => Assert.StartsWith($"customers/{customerId}/projects/{projectId}/", path));
    }

    [Fact]
    public async Task InitiateBulkDelete_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new BulkDeleteRequest
        {
            ServiceId = "test-service",
            PathPrefix = "test-service/",
            Reason = "Test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private string GenerateJwtToken(string serviceName, string audience, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName),
            new Claim("permission", "upload.files.upload"),
            new Claim("permission", "upload.files.download"),
            new Claim("permission", "upload.files.read"),
            new Claim("permission", "upload.files.delete"),
            new Claim("permission", "upload.files.list")
        };

        if (isAdmin)
        {
            claims.Add(new Claim("role", "Admin"));  // Use "role" claim name, not ClaimTypes.Role
            claims.Add(new Claim("permission", "upload.admin.manage-policies"));
            claims.Add(new Claim("permission", "upload.storage.manage"));
            claims.Add(new Claim("permission", "upload.admin.bulk-delete"));
            claims.Add(new Claim("permission", "upload.admin.view-metrics"));
            claims.Add(new Claim("permission", "upload.retention.configure"));
            claims.Add(new Claim("permission", "upload.retention.execute"));
        }

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _baseFactory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class CopyFileWithMetadataResponseProbe
    {
        public string FileId { get; set; } = string.Empty;

        public string UploadId { get; set; } = string.Empty;

        public string StoragePath { get; set; } = string.Empty;
    }
}
