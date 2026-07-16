using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Maliev.UploadService.Tests.Integration;

[Collection("Database")]
public sealed class ManagedWorkloadAuthorizationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _baseFactory;
    private readonly Mock<IIamServiceClient> _iamClient = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public ManagedWorkloadAuthorizationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddScoped(_ => _iamClient.Object));
        });
    }

    public Task InitializeAsync()
    {
        _iamClient
            .Setup(client => client.CheckPermissionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task InitiateContactUpload_ExactScopedLiveCheck_PersistsAuthenticatedOwner()
    {
        var principalId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/model.step";
        AllowLive(principalId, "upload.files.upload", $"folders/{path}");
        using var client = CreateServiceClient(principalId, "ContactService");

        var response = await client.PostAsJsonAsync("/upload/v1/uploads/resumable", new
        {
            Path = path,
            FileName = "model.step",
            ServiceName = "ContactService",
            ContentType = "application/step",
            TotalSize = 128
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InitiateResumableUploadResponse>();
        Assert.NotNull(result);
        await using var db = _baseFactory.CreateDbContext();
        var upload = await db.Uploads.SingleAsync(item => item.UploadId == result.UploadId);
        Assert.Equal(principalId, upload.UserId);
        Assert.Equal("ContactService", upload.ServiceId);
        _iamClient.Verify(client => client.CheckPermissionLiveAsync(
            principalId,
            "upload.files.upload",
            $"folders/{path}",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("orders/secret.step")]
    [InlineData("contacts-private/secret.step")]
    public async Task InitiateContactUpload_OutsideContactScope_ReturnsForbidden(string path)
    {
        var principalId = Guid.NewGuid().ToString("D");
        DenyLive(principalId, "upload.files.upload", $"folders/{path}");
        using var client = CreateServiceClient(principalId, "ContactService");

        var response = await client.PostAsJsonAsync("/upload/v1/uploads/resumable", new
        {
            Path = path,
            FileName = "secret.step",
            ServiceName = "ContactService",
            ContentType = "application/step",
            TotalSize = 128
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = _baseFactory.CreateDbContext();
        Assert.False(await db.Uploads.AnyAsync(item => item.StoragePath == path));
    }

    [Fact]
    public async Task InitiateContactUpload_SpoofedServiceName_ReturnsForbiddenBeforeAuthorizationOrMutation()
    {
        var principalId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/spoof.step";
        using var client = CreateServiceClient(principalId, "ContactService");

        var response = await client.PostAsJsonAsync("/upload/v1/uploads/resumable", new
        {
            Path = path,
            FileName = "spoof.step",
            ServiceName = "OrderService",
            ContentType = "application/step",
            TotalSize = 128
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _iamClient.Verify(client => client.CheckPermissionLiveAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        await using var db = _baseFactory.CreateDbContext();
        Assert.False(await db.Uploads.AnyAsync(item => item.StoragePath == path));
    }

    [Fact]
    public async Task ManagedUpload_OtherPrincipalCannotCompleteSignedUrlOrDelete_OwnerCan()
    {
        var ownerId = Guid.NewGuid().ToString("D");
        var otherId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/owned.txt";
        AllowAllLiveForPath(path);
        using var owner = CreateServiceClient(ownerId, "ContactService");
        using var other = CreateServiceClient(otherId, "ContactService");

        var uploadResponse = await owner.PostAsync(
            "/upload/v1/uploads",
            CreateMultipart(path, "ContactService", overwrite: false));
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploaded);

        var signedDenied = await other.PostAsJsonAsync(
            $"/upload/v1/files/{uploaded.UploadId}/signed-url",
            new { ExpirationMinutes = 5 });
        var deleteDenied = await other.DeleteAsync($"/upload/v1/files/{uploaded.UploadId}");

        Assert.Equal(HttpStatusCode.Forbidden, signedDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteDenied.StatusCode);

        var signedOwner = await owner.PostAsJsonAsync(
            $"/upload/v1/files/{uploaded.UploadId}/signed-url",
            new { ExpirationMinutes = 5 });
        Assert.Equal(HttpStatusCode.OK, signedOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/upload/v1/files/{uploaded.UploadId}")).StatusCode);
    }

    [Fact]
    public async Task ManagedResumableUpload_OtherPrincipalCannotResumeOrComplete()
    {
        var ownerId = Guid.NewGuid().ToString("D");
        var otherId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/resumable.step";
        AllowAllLiveForPath(path);
        using var owner = CreateServiceClient(ownerId, "ContactService");
        using var other = CreateServiceClient(otherId, "ContactService");

        var initiation = await owner.PostAsJsonAsync("/upload/v1/uploads/resumable", new
        {
            Path = path,
            FileName = "resumable.step",
            ServiceName = "ContactService",
            ContentType = "application/step",
            TotalSize = 128
        });
        var initiated = await initiation.Content.ReadFromJsonAsync<InitiateResumableUploadResponse>();
        Assert.NotNull(initiated);

        var resumeDenied = await other.PutAsync(
            $"/upload/v1/uploads/resumable/{initiated.UploadId}",
            new ByteArrayContent([1]));
        var completeDenied = await other.PostAsJsonAsync(
            $"/upload/v1/uploads/resumable/{initiated.UploadId}/complete",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, resumeDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, completeDenied.StatusCode);
    }

    [Fact]
    public async Task ManagedUpload_OtherPrincipalCannotOverwriteOwnedPath()
    {
        var ownerId = Guid.NewGuid().ToString("D");
        var otherId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/overwrite.txt";
        AllowAllLiveForPath(path);
        using var owner = CreateServiceClient(ownerId, "ContactService");
        using var other = CreateServiceClient(otherId, "ContactService");

        Assert.Equal(
            HttpStatusCode.OK,
            (await owner.PostAsync("/upload/v1/uploads", CreateMultipart(path, "ContactService", false))).StatusCode);
        var overwrite = await other.PostAsync(
            "/upload/v1/uploads",
            CreateMultipart(path, "ContactService", true));

        Assert.Equal(HttpStatusCode.Forbidden, overwrite.StatusCode);
        await using var db = _baseFactory.CreateDbContext();
        var upload = await db.Uploads.SingleAsync(item => item.StoragePath == path);
        Assert.Equal(ownerId, upload.UserId);
    }

    [Fact]
    public async Task LegacyNullOwner_ManagedCallerWithExactScopeCanGenerateSignedUrl()
    {
        var principalId = Guid.NewGuid().ToString("D");
        var uploadId = Guid.NewGuid().ToString("D");
        var path = $"contacts/{Guid.NewGuid():N}/legacy.txt";
        await using (var db = _baseFactory.CreateDbContext())
        {
            db.Uploads.Add(new Upload
            {
                UploadId = uploadId,
                ServiceId = "ContactService",
                UserId = null,
                FileName = "legacy.txt",
                ContentType = "text/plain",
                FileSize = 6,
                StoragePath = path,
                BytesUploaded = 6,
                Status = UploadStatus.Completed,
                UploadedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
            db.FileMetadata.Add(new FileMetadata
            {
                FileId = Guid.NewGuid().ToString("D"),
                UploadId = uploadId,
                ServiceId = "ContactService",
                StoragePath = path,
                VersionETag = "legacy-etag",
                FileSize = 6,
                ContentType = "text/plain",
                Checksum = "legacy",
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        AllowLive(principalId, "upload.files.download", $"folders/{path}");
        using var client = CreateServiceClient(principalId, "ContactService");

        var response = await client.PostAsJsonAsync(
            $"/upload/v1/files/{uploadId}/signed-url",
            new { ExpirationMinutes = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _iamClient.Verify(client => client.CheckPermissionLiveAsync(
            principalId,
            "upload.files.download",
            $"folders/{path}",
            It.IsAny<CancellationToken>()), Times.Once);
        _iamClient.Verify(client => client.CheckPermissionLiveAsync(
            principalId,
            "upload.files.download",
            "global",
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmployeeCaller_IgnoresServiceNameClaimAndUsesSubjectForExactAuthorization()
    {
        var principalId = $"employee-{Guid.NewGuid():N}";
        var path = $"test-service/{Guid.NewGuid():N}/employee.txt";
        AllowLive(principalId, "upload.files.upload", $"folders/{path}");
        using var client = CreateUserClient(principalId, "SpoofedService");

        var response = await client.PostAsync(
            "/upload/v1/uploads",
            CreateMultipart(path, "test-service", false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = _baseFactory.CreateDbContext();
        var upload = await db.Uploads.SingleAsync(item => item.StoragePath == path);
        Assert.Equal(principalId, upload.UserId);
        Assert.Equal("test-service", upload.ServiceId);
    }

    private HttpClient CreateServiceClient(string principalId, string serviceName)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(
                principalId,
                new Claim("service_name", serviceName),
                new Claim("user_type", "service")));
        return client;
    }

    private HttpClient CreateUserClient(string principalId, string untrustedServiceName)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(principalId, new Claim("service_name", untrustedServiceName)));
        return client;
    }

    private string CreateToken(string principalId, params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, principalId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("permission", "upload.files.upload"),
            new("permission", "upload.files.download"),
            new("permission", "upload.files.delete")
        };
        claims.AddRange(additionalClaims);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: _baseFactory.SigningCredentials));
    }

    private static MultipartFormDataContent CreateMultipart(string path, string serviceName, bool overwrite)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("contact attachment"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "File", "owned.txt");
        content.Add(new StringContent(path), "Path");
        content.Add(new StringContent(serviceName), "ServiceName");
        content.Add(new StringContent(overwrite.ToString()), "Overwrite");
        return content;
    }

    private void AllowLive(string principalId, string permission, string resourcePath) =>
        _iamClient.Setup(client => client.CheckPermissionLiveAsync(
                principalId,
                permission,
                resourcePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private void DenyLive(string principalId, string permission, string resourcePath) =>
        _iamClient.Setup(client => client.CheckPermissionLiveAsync(
                principalId,
                permission,
                resourcePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    private void AllowAllLiveForPath(string path) =>
        _iamClient.Setup(client => client.CheckPermissionLiveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string?>(resource => resource == "global" || resource == $"folders/{path}"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
}
