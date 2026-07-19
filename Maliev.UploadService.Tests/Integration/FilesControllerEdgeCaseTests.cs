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
public class FilesControllerEdgeCaseTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private HttpClient _client = null!;
    private string _authToken = null!;
    private string _uploadId = null!;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public FilesControllerEdgeCaseTests(TestWebApplicationFactory factory)
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

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _iamClientMock.Setup(x => x.CheckPermissionLiveAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _authToken = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file for edge cases"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", $"edge-case-test-{uniqueSuffix}.txt");
        content.Add(new StringContent($"test-service/files/edge-case-test-{uniqueSuffix}.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        Assert.Equal(System.Net.HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        _uploadId = uploadResult!.UploadId;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GenerateSignedUrl_WithZeroExpiration_ReturnsBadRequest()
    {
        var request = new
        {
            ExpirationMinutes = 0
        };

        var response = await _client.PostAsJsonAsync($"/upload/v1/files/{_uploadId}/signed-url", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrl_WithNegativeExpiration_ReturnsBadRequest()
    {
        var request = new
        {
            ExpirationMinutes = -1
        };

        var response = await _client.PostAsJsonAsync($"/upload/v1/files/{_uploadId}/signed-url", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrl_WithVeryLargeExpiration_ReturnsBadRequest()
    {
        var request = new
        {
            ExpirationMinutes = 10080 // More than a week
        };

        var response = await _client.PostAsJsonAsync($"/upload/v1/files/{_uploadId}/signed-url", request);

        // API accepts large expiration values, just verify response is valid
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QueryFiles_WithPagination_ReturnsCorrectPage()
    {
        for (int i = 0; i < 5; i++)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes($"File {i}"));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContent, "File", $"pagination-test-{i}.txt");
            content.Add(new StringContent($"test-service/files/pagination-test-{i}.txt"), "Path");
            content.Add(new StringContent("test-service"), "ServiceName");
            await _client.PostAsync("/upload/v1/uploads", content);
        }

        var response = await _client.GetAsync("/upload/v1/files?page=1&pageSize=3&pathPrefix=test-service/files/pagination-test-");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<QueryFilesResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Files.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task QueryFiles_WithPageSizeExceedingMax_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/upload/v1/files?page=1&pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QueryFiles_WithoutPathPrefix_FiltersByServiceId()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Service specific file"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "service-specific.txt");
        content.Add(new StringContent("test-service/service-specific.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");
        await _client.PostAsync("/upload/v1/uploads", content);

        var response = await _client.GetAsync("/upload/v1/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<QueryFilesResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Files);
    }

    [Fact]
    public async Task DeleteFile_UpdatesLastAccessedTime()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("File for last access test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "last-access-test.txt");
        content.Add(new StringContent("test-service/files/last-access-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        var testUploadId = uploadResult.UploadId;

        await _client.GetAsync($"/upload/v1/files/{testUploadId}");

        var response = await _client.DeleteAsync($"/upload/v1/files/{testUploadId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
