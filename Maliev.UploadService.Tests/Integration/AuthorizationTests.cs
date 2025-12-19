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

[Collection("Database")]
public class AuthorizationTests : IAsyncLifetime
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

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        _uploadId = uploadResult!.UploadId;
    }

    public Task DisposeAsync()
    {
        // Don't delete the file here - all tests in this class use the same upload
        // The file will be cleaned up when the test database is torn down
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetFileMetadata_UnauthorizedService_ReturnsForbidden()
    {
        // Upload a fresh file for this test
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testServiceToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("File for metadata test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "metadata-test.txt");
        content.Add(new StringContent("test-service/auth-tests/metadata-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var testUploadId = uploadResult!.UploadId;

        // Arrange - switch to other-service token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);

        // Act
        var response = await _client.GetAsync($"/upload/v1/files/{testUploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrl_UnauthorizedService_ReturnsForbidden()
    {
        // Upload a fresh file for this test
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testServiceToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("File for signed URL test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "signed-url-test.txt");
        content.Add(new StringContent("test-service/auth-tests/signed-url-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var testUploadId = uploadResult!.UploadId;

        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);
        var request = new { ExpirationMinutes = 60 };

        // Act
        var response = await _client.PostAsJsonAsync($"/upload/v1/files/{testUploadId}/signed-url", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_UnauthorizedService_ReturnsForbidden()
    {
        // Upload a fresh file for this test
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testServiceToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("File for delete test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "delete-test.txt");
        content.Add(new StringContent("test-service/auth-tests/delete-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var testUploadId = uploadResult!.UploadId;

        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _otherServiceToken);

        // Act
        var response = await _client.DeleteAsync($"/upload/v1/files/{testUploadId}");

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
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _factory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
