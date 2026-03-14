using Maliev.Aspire.ServiceDefaults.IAM;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

[Collection("Database")]
public class IAMUserTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public IAMUserTests(TestWebApplicationFactory factory)
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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetFile_OwnResource_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = "user-123";
        var requestedPath = "users/user-123/file-abc.pdf";
        var resourcePath = $"folders/{requestedPath}";

        var token = GenerateJwtToken(userId, "uploadservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Seed DB with this file
        using var scope = _baseFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        var uploadId = Guid.NewGuid().ToString();

        dbContext.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = userId,
            FileName = "file-abc.pdf",
            StoragePath = requestedPath,
            ContentType = "application/pdf",
            FileSize = 1024,
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });

        dbContext.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = userId,
            StoragePath = requestedPath,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024,
            VersionETag = "etag",
            Checksum = "mock-checksum"
        });
        await dbContext.SaveChangesAsync();

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            userId,
            UploadPermissions.FilesRead,
            resourcePath,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await client.GetAsync($"/upload/v1/files/{uploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetFile_OtherUserResource_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = "user-123";
        var requestedPath = "users/other-user/secret.pdf";
        var resourcePath = $"folders/{requestedPath}";

        var token = GenerateJwtToken(userId, "uploadservice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Seed DB with this file
        using var scope = _baseFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        var uploadId = Guid.NewGuid().ToString();

        dbContext.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "other-user",
            FileName = "secret.pdf",
            StoragePath = requestedPath,
            ContentType = "application/pdf",
            FileSize = 1024,
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });

        dbContext.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "other-user",
            StoragePath = requestedPath,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024,
            VersionETag = "etag",
            Checksum = "mock-checksum"
        });
        await dbContext.SaveChangesAsync();

        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            userId,
            UploadPermissions.FilesRead,
            resourcePath,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await client.GetAsync($"/upload/v1/files/{uploadId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private string GenerateJwtToken(string serviceName, string audience)
    {
        var claimsList = new List<Claim>
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
            claims: claimsList,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _baseFactory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
