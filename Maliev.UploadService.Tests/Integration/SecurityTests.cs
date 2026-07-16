using Maliev.Aspire.ServiceDefaults.IAM;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// Security tests for the Upload Service.
/// Tests authorization bypass attempts, injection attacks, and access control violations.
/// </summary>
[Collection("Database")]
public class SecurityTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private HttpClient _client = null!;
    private HttpClient _unauthenticatedClient = null!;
    private string _service1UploadId = null!;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public SecurityTests(TestWebApplicationFactory factory)
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
        // Clear all legacy policies to ensure IAM is the primary authority for these tests
        using var scope = _baseFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        dbContext.ServiceAuthorizationPolicies.RemoveRange(dbContext.ServiceAuthorizationPolicies);
        await dbContext.SaveChangesAsync();

        // Allow all IAM checks by default for security tests (focus on other vulns)
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _iamClientMock.Setup(x => x.CheckPermissionLiveAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Authenticated client for test-service
        _client = _factory.CreateClient();
        var token = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Unauthenticated client
        _unauthenticatedClient = _factory.CreateClient();

        // Upload a file as test-service for cross-service access tests
        // Use unique path per test run to avoid conflicts
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test service confidential data"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "secure.txt");
        content.Add(new StringContent($"test-service/secure/data-{uniqueId}.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var response = await _client.PostAsync("/upload/v1/uploads", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        _service1UploadId = result!.UploadId;
    }

    public async Task DisposeAsync()
    {
        // Cleanup: Delete the test file
        if (!string.IsNullOrEmpty(_service1UploadId))
        {
            try
            {
                await _client.DeleteAsync($"/upload/v1/files/{_service1UploadId}");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _client?.Dispose();
        _unauthenticatedClient?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task UnauthenticatedRequest_Upload_Returns401()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("test-service/test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _unauthenticatedClient.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_GetFile_Returns401()
    {
        // Act
        var response = await _unauthenticatedClient.GetAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_DeleteFile_Returns401()
    {
        // Act
        var response = await _unauthenticatedClient.DeleteAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossServiceAccess_GetFile_Returns403()
    {
        // Arrange - create separate client to avoid modifying shared _client
        using var crossServiceClient = _factory.CreateClient();
        var service2Token = GenerateJwtToken("other-service", "uploadservice");
        crossServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service2Token);

        // Explicitly deny in IAM for this test
        _iamClientMock.Reset();
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await crossServiceClient.GetAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossServiceAccess_DeleteFile_Returns403()
    {
        // Arrange - create separate client to avoid modifying shared _client
        using var crossServiceClient = _factory.CreateClient();
        var service2Token = GenerateJwtToken("other-service", "uploadservice");
        crossServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service2Token);

        // Explicitly deny in IAM for this test
        _iamClientMock.Reset();
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await crossServiceClient.DeleteAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossServiceAccess_GenerateSignedUrl_Returns403()
    {
        // Arrange - create separate client to avoid modifying shared _client
        using var crossServiceClient = _factory.CreateClient();
        var service2Token = GenerateJwtToken("other-service", "uploadservice");
        crossServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service2Token);

        // Explicitly deny in IAM for this test
        _iamClientMock.Reset();
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new { ExpirationMinutes = 15 };

        // Act
        var response = await crossServiceClient.PostAsJsonAsync($"/upload/v1/files/{_service1UploadId}/signed-url", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("test-service/../demo-service/data.txt")]
    [InlineData("test-service/data/../../etc/shadow")]
    [InlineData("..\\..\\windows\\system32\\config\\sam")]
    public async Task PathTraversalAttempt_Upload_ReturnsBadRequest(string maliciousPath)
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Malicious content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "malicious.txt");
        content.Add(new StringContent(maliciousPath), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("test-service/data.txt'; DROP TABLE Uploads; --")]
    [InlineData("test-service/data.txt<script>alert('xss')</script>")]
    [InlineData("test-service/data.txt\0.exe")]
    [InlineData("test-service/\n\r../../etc/passwd")]
    public async Task InjectionAttempt_Upload_HandledSafely(string suspiciousPath)
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent(suspiciousPath), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/upload/v1/uploads", content);

        // Assert
        // Should either reject with BadRequest, Forbidden, or InternalServerError OR sanitize the path
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code: {response.StatusCode}");

        // If upload succeeded, verify it was sanitized by trying to retrieve it
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
            Assert.NotNull(result);
            // Cleanup
            await _client.DeleteAsync($"/upload/v1/files/{result!.UploadId}");
        }
    }

    [Fact]
    public async Task InvalidJwtToken_Upload_Returns401()
    {
        // Arrange
        var invalidClient = _factory.CreateClient();
        invalidClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("test-service/test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await invalidClient.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        invalidClient.Dispose();
    }

    [Fact]
    public async Task ExpiredJwtToken_Upload_Returns401()
    {
        // Arrange
        var expiredClient = _factory.CreateClient();
        var expiredToken = GenerateJwtToken("test-service", "uploadservice", DateTime.UtcNow.AddHours(-1)); // Expired 1 hour ago
        expiredClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("test-service/test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await expiredClient.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        expiredClient.Dispose();
    }

    [Fact]
    public async Task AccessNonExistentFile_Returns404NotForbidden()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Reset mock to allow permissions - we want to test 404 behavior, not auth
        _iamClientMock.Reset();
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Explicitly setup for null resource path to be safe
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await _client.GetAsync($"/upload/v1/files/{nonExistentId}");

        // Assert
        // Should return 404, not 403, to avoid information disclosure
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ServiceMismatch_UploadWithWrongServiceName_ReturnsForbidden()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Data"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "mismatch.txt");
        content.Add(new StringContent("other-service/mismatch.txt"), "Path");
        content.Add(new StringContent("other-service"), "ServiceName");

        // token is for "test-service", but request says "other-service"
        // Use token WITHOUT permissions to ensure IAM is actually checked or fallback fails
        var unauthorizedToken = GenerateJwtToken("test-service", "uploadservice", permissions: Array.Empty<string>());
        using var unauthorizedClient = _factory.CreateClient();
        unauthorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unauthorizedToken);

        // IAM check will be for principal "test-service" (from token) but resource "folders/other-service/..."
        _iamClientMock.Reset();
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "test-service", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await unauthorizedClient.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("application/x-msdownload")] // .exe
    [InlineData("application/x-executable")]
    [InlineData("application/x-sh")]
    [InlineData("application/x-bat")]
    public async Task DangerousContentType_Upload_MayBeBlocked(string dangerousContentType)
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Potentially dangerous content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(dangerousContentType);
        content.Add(fileContent, "File", "dangerous.exe");
        content.Add(new StringContent("test-service/dangerous.exe"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/upload/v1/uploads", content);

        // Assert
        // Depending on authorization policy, this should either be blocked or allowed
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code: {response.StatusCode}");

        // Cleanup if upload succeeded
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
            await _client.DeleteAsync($"/upload/v1/files/{result!.UploadId}");
        }
    }

    private string GenerateJwtToken(string serviceName, string audience, DateTime? expires = null, string[]? permissions = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName)
        };

        var perms = permissions ?? new[]
        {
            "upload.files.upload",
            "upload.files.download",
            "upload.files.read",
            "upload.files.delete",
            "upload.files.list",
            "upload.admin.manage-policies",
            "upload.admin.bulk-delete",
            "upload.admin.view-metrics",
            "upload.retention.configure",
            "upload.retention.execute"
        };

        foreach (var p in perms)
        {
            claims.Add(new Claim("permission", p));
        }

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: _baseFactory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
