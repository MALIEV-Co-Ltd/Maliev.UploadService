using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

public class AuthorizationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _testServiceToken = null!;
    private string _otherServiceToken = null!;
    private string _uploadId = null!;

    public AuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _testServiceToken = GenerateJwtToken("test-service", "uploadservice");
        _otherServiceToken = GenerateJwtToken("other-service", "uploadservice");

        // Upload a file as test-service
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testServiceToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Private file"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "private.txt");
        content.Add(new StringContent("test-service/private/private.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/api/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        _uploadId = uploadResult!.UploadId;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetFileMetadata_UnauthorizedService_ReturnsForbidden()
    {
        // Arrange - switch to other-service token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);

        // Act
        var response = await _client.GetAsync($"/api/v1/files/{_uploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrl_UnauthorizedService_ReturnsForbidden()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);
        var request = new { ExpirationMinutes = 60 };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/files/{_uploadId}/signed-url", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_UnauthorizedService_ReturnsForbidden()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/files/{_uploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private string GenerateJwtToken(string serviceName, string audience)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName)
        };

        var token = new JwtSecurityToken(
            issuer: "https://test.maliev.com",
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _factory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
