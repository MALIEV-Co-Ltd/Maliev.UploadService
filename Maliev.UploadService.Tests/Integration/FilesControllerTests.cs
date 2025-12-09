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

public class FilesControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _authToken = null!;
    private string _uploadId = null!;

    public FilesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _authToken = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        // Upload a test file to use in retrieval tests
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file for retrieval"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test-retrieval.txt");
        content.Add(new StringContent("test-service/files/test-retrieval.txt"), "Path");
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
    public async Task GetFileMetadata_ValidUploadId_ReturnsMetadata()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/files/{_uploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FileMetadataResponse>();
        Assert.NotNull(result);
        Assert.Equal(_uploadId, result!.UploadId);
        Assert.Equal("test-service/files/test-retrieval.txt", result.StoragePath);
        Assert.Equal("text/plain", result.ContentType);
        Assert.True(result.FileSize > 0);
    }

    [Fact]
    public async Task GetFileMetadata_NonExistentUploadId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/files/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrl_ValidUploadId_ReturnsSignedUrl()
    {
        // Arrange
        var request = new
        {
            ExpirationMinutes = 60
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/files/{_uploadId}/signed-url", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SignedUrlResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.SignedUrl);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task QueryFiles_ByPathPrefix_ReturnsMatchingFiles()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/files?pathPrefix=test-service/files/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<QueryFilesResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Files);
        Assert.All(result.Files, f => Assert.StartsWith("test-service/files/", f.StoragePath));
    }

    [Fact]
    public async Task DeleteFile_ValidUploadId_ReturnsNoContent()
    {
        // Arrange - upload a file to delete
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("File to delete"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "delete-me.txt");
        content.Add(new StringContent("test-service/files/delete-me.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/api/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var uploadIdToDelete = uploadResult!.UploadId;

        // Act
        var response = await _client.DeleteAsync($"/api/v1/files/{uploadIdToDelete}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify file is gone
        var getResponse = await _client.GetAsync($"/api/v1/files/{uploadIdToDelete}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
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
