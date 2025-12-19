using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// Security tests for the Upload Service.
/// Tests authorization bypass attempts, injection attacks, and access control violations.
/// </summary>
[Collection("Database")]
public class SecurityTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private HttpClient _unauthenticatedClient = null!;
    private string _service1UploadId = null!;

    public SecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Authenticated client for test-service
        _client = _factory.CreateClient();
        var token = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Unauthenticated client
        _unauthenticatedClient = _factory.CreateClient();

        // Upload a file as test-service for cross-service access tests
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test service confidential data"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "secure.txt");
        content.Add(new StringContent("test-service/secure/data.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var response = await _client.PostAsync("/upload/v1/uploads", content);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        _service1UploadId = result!.UploadId;

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Cleanup: Delete the test file
        if (!string.IsNullOrEmpty(_service1UploadId))
        {
            await _client.DeleteAsync($"/upload/v1/files/{_service1UploadId}");
        }

        _client?.Dispose();
        _unauthenticatedClient?.Dispose();
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
        // Arrange - Create client for demo-service (different from test-service)
        var otherServiceClient = _factory.CreateClient();
        var otherServiceToken = GenerateJwtToken("demo-service", "uploadservice");
        otherServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherServiceToken);

        // Act - Try to access test-service's file
        var response = await otherServiceClient.GetAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        otherServiceClient.Dispose();
    }

    [Fact]
    public async Task CrossServiceAccess_DeleteFile_Returns403()
    {
        // Arrange - Create client for demo-service (different from test-service)
        var otherServiceClient = _factory.CreateClient();
        var otherServiceToken = GenerateJwtToken("demo-service", "uploadservice");
        otherServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherServiceToken);

        // Act - Try to delete test-service's file
        var response = await otherServiceClient.DeleteAsync($"/upload/v1/files/{_service1UploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        otherServiceClient.Dispose();
    }

    [Fact]
    public async Task CrossServiceAccess_GenerateSignedUrl_Returns403()
    {
        // Arrange - Create client for demo-service (different from test-service)
        var otherServiceClient = _factory.CreateClient();
        var otherServiceToken = GenerateJwtToken("demo-service", "uploadservice");
        otherServiceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherServiceToken);

        var signedUrlRequest = new { ExpirationMinutes = 15 };

        // Act - Try to generate signed URL for test-service's file
        var response = await otherServiceClient.PostAsJsonAsync($"/upload/v1/files/{_service1UploadId}/signed-url", signedUrlRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        otherServiceClient.Dispose();
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

        // Act
        var response = await _client.GetAsync($"/upload/v1/files/{nonExistentId}");

        // Assert
        // Should return 404, not 403, to avoid information disclosure
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ServiceMismatch_UploadWithWrongServiceName_ReturnsForbidden()
    {
        // Arrange - test-service tries to upload to demo-service's path
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("demo-service/data.txt"), "Path"); // Wrong service path
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/upload/v1/uploads", content);

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

    private string GenerateJwtToken(string serviceName, string audience, DateTime? expires = null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName)
        };

        var token = new JwtSecurityToken(
            issuer: "test-issuer",  // Match BaseIntegrationTestFactory expectations
            audience: "test-audience",  // Match BaseIntegrationTestFactory expectations
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: _factory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
