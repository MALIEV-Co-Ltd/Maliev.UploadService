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

public class IAMAdminTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public IAMAdminTests(TestWebApplicationFactory factory)
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

    [Fact]
    public async Task GetMetrics_WithAuthorizedIAM_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var adminId = "admin-user";

        var token = GenerateJwtToken(adminId, "uploadservice", isAdmin: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            adminId,
            UploadPermissions.AdminViewMetrics,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await client.GetAsync("/upload/v1/admin/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_WithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = "standard-user";

        var token = GenerateJwtToken(userId, "uploadservice", isAdmin: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            userId,
            UploadPermissions.AdminBulkDelete,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await client.PostAsJsonAsync("/upload/v1/admin/bulk-delete", new { ServiceId = "test-service" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
            claims.Add(new Claim("role", "Admin"));
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





