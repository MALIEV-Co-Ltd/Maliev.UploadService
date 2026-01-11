using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Mvc.Testing;
using Maliev.UploadService.Api.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
public class FilesControllerTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private HttpClient _client = null!;
    private string _authToken = null!;
    private string _uploadId = null!;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public FilesControllerTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ => _iamClientMock.Object);
            });
        });
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        // Allow all IAM checks for files management tests
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _authToken = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        // Upload a test file to use in retrieval tests
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file for retrieval"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test-retrieval.txt");
        content.Add(new StringContent("test-service/files/test-retrieval.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        _uploadId = uploadResult!.UploadId;

        // Verify the upload has the correct storage path
        Assert.NotEmpty(uploadResult.StoragePath);
    }

    public async Task DisposeAsync()
    {
        // Clean up the uploaded file
        if (_uploadId != null && _client != null)
        {
            try
            {
                await _client.DeleteAsync($"/upload/v1/files/{_uploadId}");
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetFileMetadata_ValidUploadId_ReturnsMetadata()
    {
        // Act
        var response = await _client.GetAsync($"/upload/v1/files/{_uploadId}");

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
        var response = await _client.GetAsync($"/upload/v1/files/{Guid.NewGuid()}");

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
        var response = await _client.PostAsJsonAsync($"/upload/v1/files/{_uploadId}/signed-url", request);

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
        var response = await _client.GetAsync("/upload/v1/files?pathPrefix=test-service/files/");

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

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var uploadIdToDelete = uploadResult!.UploadId;

        // Act
        var response = await _client.DeleteAsync($"/upload/v1/files/{uploadIdToDelete}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify file is gone
        var getResponse = await _client.GetAsync($"/upload/v1/files/{uploadIdToDelete}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
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
            signingCredentials: _baseFactory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
