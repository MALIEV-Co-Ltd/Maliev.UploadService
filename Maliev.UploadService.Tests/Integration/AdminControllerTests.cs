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
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        // Mock IAM for Non-Admin token (explicit denial)
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service",
            UploadPermissions.AdminBulkDelete,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Upload some test files for bulk delete
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _nonAdminToken);

        // Also mock IAM for upload if needed by fallback
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service",
            UploadPermissions.FilesUpload,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (int i = 0; i < 3; i++)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes($"Test file {i}"));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContent, "File", $"test-{i}.txt");
            content.Add(new StringContent($"test-service/bulk-delete/test-{i}.txt"), "Path");
            content.Add(new StringContent("test-service"), "ServiceName");

            var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
            var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
            _testUploadIds.Add(uploadResult!.UploadId);
        }
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
            new Claim("permission", "upload.files.read"),
            new Claim("permission", "upload.files.delete"),
            new Claim("permission", "upload.files.list")
        };

        if (isAdmin)
        {
            claims.Add(new Claim("role", "Admin"));  // Use "role" claim name, not ClaimTypes.Role
            claims.Add(new Claim("permission", "upload.admin.manage-policies"));
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
}





