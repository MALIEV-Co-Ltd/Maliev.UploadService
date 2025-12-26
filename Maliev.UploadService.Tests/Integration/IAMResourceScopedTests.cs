using Maliev.Aspire.ServiceDefaults.IAM;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

public class IAMResourceScopedTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public IAMResourceScopedTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real IAM client with mock for tests
                services.AddScoped(_ => _iamClientMock.Object);
            });
        });
    }

    [Fact]
    public async Task Upload_WithAuthorizedIAMResource_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var serviceName = "InvoiceService";
        var requestedPath = "invoices/2025/inv1.pdf";
        var resourcePath = $"folders/{requestedPath}";

        var token = GenerateJwtToken(serviceName, "uploadservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            serviceName,
            UploadPermissions.FilesUpload,
            resourcePath,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(serviceName), "ServiceName");
        content.Add(new StringContent(requestedPath), "Path");
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", "inv1.pdf");

        // Act
        var response = await client.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithUnauthorizedIAMResource_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var serviceName = "InvoiceService";
        var requestedPath = "orders/2025/ord1.pdf";
        var resourcePath = $"folders/{requestedPath}";

        var token = GenerateJwtToken(serviceName, "uploadservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            serviceName,
            UploadPermissions.FilesUpload,
            resourcePath,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Also ensure legacy doesn't allow it (mocking empty DB/policies)

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(serviceName), "ServiceName");
        content.Add(new StringContent(requestedPath), "Path");
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", "ord1.pdf");

        // Act
        var response = await client.PostAsync("/upload/v1/uploads", content);

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
            new Claim("service_id", serviceName),
            new Claim("permission", "upload.files.upload"),
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





