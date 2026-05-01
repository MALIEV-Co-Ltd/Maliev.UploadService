using Maliev.Aspire.ServiceDefaults.IAM;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Maliev.UploadService.Api.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// End-to-end integration tests verifying complete workflows
/// </summary>
[Collection("Database")]
public class EndToEndTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestWebApplicationFactory _baseFactory;
    private HttpClient _client = null!;
    private readonly Mock<IIamServiceClient> _iamClientMock = new();

    public EndToEndTests(TestWebApplicationFactory factory)
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

        // Allow all IAM checks for E2E tests
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var token = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task FullWorkflow_UploadRetrieveDelete_CompletesSuccessfully()
    {
        // ==================== PHASE 1: UPLOAD ====================
        var fileName = $"e2e-test-{Guid.NewGuid()}.txt";
        var fileContent = "This is an end-to-end test file with important data.";
        var filePath = $"test-service/e2e/{fileName}";

        var uploadContent = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var fileContentPart = new ByteArrayContent(fileBytes);
        fileContentPart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        uploadContent.Add(fileContentPart, "File", fileName);
        uploadContent.Add(new StringContent(filePath), "Path");
        uploadContent.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await _client.PostAsync("/upload/v1/uploads", uploadContent);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        Assert.NotNull(uploadResult.UploadId);
        Assert.Equal(filePath, uploadResult.StoragePath);
        Assert.Equal("text/plain", uploadResult.ContentType);
        Assert.Equal(fileBytes.Length, uploadResult.FileSize);

        var uploadId = uploadResult.UploadId;

        // ==================== PHASE 2: RETRIEVE METADATA ====================
        var metadataResponse = await _client.GetAsync($"/upload/v1/files/{uploadId}");

        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<FileMetadataResponse>();
        Assert.NotNull(metadata);
        Assert.Equal(uploadId, metadata.UploadId);
        Assert.Equal(filePath, metadata.StoragePath);
        Assert.Equal("text/plain", metadata.ContentType);
        Assert.Equal(fileBytes.Length, metadata.FileSize);

        // ==================== PHASE 3: GENERATE SIGNED URL ====================
        var signedUrlRequest = new GenerateSignedUrlRequest
        {
            ExpirationMinutes = 15
        };

        var signedUrlResponse = await _client.PostAsJsonAsync($"/upload/v1/files/{uploadId}/signed-url", signedUrlRequest);

        Assert.Equal(HttpStatusCode.OK, signedUrlResponse.StatusCode);
        var signedUrlResult = await signedUrlResponse.Content.ReadFromJsonAsync<SignedUrlResponse>();
        Assert.NotNull(signedUrlResult);
        Assert.NotNull(signedUrlResult.SignedUrl);
        Assert.NotEmpty(signedUrlResult.SignedUrl);
        Assert.True(signedUrlResult.ExpiresAt > DateTime.UtcNow);
        Assert.True(signedUrlResult.ExpiresAt <= DateTime.UtcNow.AddMinutes(16)); // Allow 1 min buffer

        // ==================== PHASE 4: QUERY FILES BY PATH ====================
        var queryResponse = await _client.GetAsync("/upload/v1/files?pathPrefix=test-service/e2e/&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        var queryResult = await queryResponse.Content.ReadFromJsonAsync<QueryFilesResponse>();
        Assert.NotNull(queryResult);
        Assert.True(queryResult.TotalCount > 0);
        Assert.Contains(queryResult.Files, f => f.UploadId == uploadId);

        // ==================== PHASE 5: DELETE FILE ====================
        var deleteResponse = await _client.DeleteAsync($"/upload/v1/files/{uploadId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // ==================== PHASE 6: VERIFY DELETION ====================
        // Try to retrieve metadata after deletion - should return 404
        var deletedMetadataResponse = await _client.GetAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.NotFound, deletedMetadataResponse.StatusCode);

        // Try to generate signed URL after deletion - should return 404
        var deletedSignedUrlResponse = await _client.PostAsJsonAsync($"/upload/v1/files/{uploadId}/signed-url", signedUrlRequest);
        Assert.Equal(HttpStatusCode.NotFound, deletedSignedUrlResponse.StatusCode);

        // Verify file no longer appears in query results
        var queryAfterDeleteResponse = await _client.GetAsync($"/upload/v1/files?pathPrefix=test-service/e2e/{fileName}&page=1&pageSize=10");
        var queryAfterDeleteResult = await queryAfterDeleteResponse.Content.ReadFromJsonAsync<QueryFilesResponse>();
        Assert.NotNull(queryAfterDeleteResult);
        Assert.DoesNotContain(queryAfterDeleteResult.Files, f => f.UploadId == uploadId);
    }

    [Fact]
    public async Task ResumableUploadWorkflow_LargeFile_CompletesSuccessfully()
    {
        // Create a client that doesn't follow redirects (for 308 status)
        var nonRedirectClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        nonRedirectClient.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;

        var filePath = $"test-service/e2e/resumable-{Guid.NewGuid()}.bin";
        var totalSize = 5 * 1024 * 1024; // 5 MB

        // ==================== PHASE 1: INITIATE RESUMABLE UPLOAD ====================
        var initiateRequest = new
        {
            Path = filePath,
            FileName = "upload.bin",
            ServiceName = "test-service",
            ContentType = "application/octet-stream",
            TotalSize = totalSize
        };

        var initiateResponse = await nonRedirectClient.PostAsJsonAsync("/upload/v1/uploads/resumable", initiateRequest);
        Assert.Equal(HttpStatusCode.OK, initiateResponse.StatusCode);

        var initiateResult = await initiateResponse.Content.ReadFromJsonAsync<InitiateResumableUploadResponse>();
        Assert.NotNull(initiateResult);
        Assert.NotNull(initiateResult.UploadId);
        var uploadId = initiateResult.UploadId;

        // ==================== PHASE 2: UPLOAD CHUNKS ====================
        var chunkSize = 1024 * 1024; // 1 MB chunks
        var totalChunks = (totalSize + chunkSize - 1) / chunkSize;

        for (int i = 0; i < totalChunks; i++)
        {
            var startByte = i * chunkSize;
            var endByte = Math.Min(startByte + chunkSize - 1, totalSize - 1);
            var currentChunkSize = endByte - startByte + 1;

            var chunkData = new byte[currentChunkSize];
            new Random(i).NextBytes(chunkData); // Consistent random data per chunk

            var chunkContent = new ByteArrayContent(chunkData);
            chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            chunkContent.Headers.ContentRange = new ContentRangeHeaderValue(startByte, endByte, totalSize);

            var chunkResponse = await nonRedirectClient.PutAsync($"/upload/v1/uploads/resumable/{uploadId}", chunkContent);

            if (i < totalChunks - 1)
            {
                // Intermediate chunks should return 308 (Resume Incomplete) or 200 (if GCS accepts it)
                Assert.True(
                    chunkResponse.StatusCode == HttpStatusCode.OK ||
                    chunkResponse.StatusCode == (HttpStatusCode)308,
                    $"Expected OK or 308 for chunk {i}, got {chunkResponse.StatusCode}");
            }
            else
            {
                // Final chunk should return 200 OK
                Assert.True(
                    chunkResponse.StatusCode == HttpStatusCode.OK,
                    $"Expected OK for final chunk, got {chunkResponse.StatusCode}");
            }
        }

        // ==================== PHASE 3: VERIFY UPLOAD COMPLETED ====================
        var metadataResponse = await nonRedirectClient.GetAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);

        var metadata = await metadataResponse.Content.ReadFromJsonAsync<FileMetadataResponse>();
        Assert.NotNull(metadata);
        Assert.Equal(uploadId, metadata.UploadId);
        Assert.Equal("application/octet-stream", metadata.ContentType);

        // ==================== PHASE 4: CLEANUP ====================
        var deleteResponse = await nonRedirectClient.DeleteAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task MultiServiceUpload_CrossServiceIsolation_EnforcesAuthorization()
    {
        // Create tokens for two different services
        var service1Token = GenerateJwtToken("service-a", "uploadservice");
        var service2Token = GenerateJwtToken("service-b", "uploadservice");

        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service1Token);

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service2Token);

        // ==================== SERVICE A UPLOADS FILE ====================
        var fileContent = "Service A confidential data";
        var uploadContent1 = new MultipartFormDataContent();
        var fileContentPart1 = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent));
        fileContentPart1.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        uploadContent1.Add(fileContentPart1, "File", "confidential.txt");
        uploadContent1.Add(new StringContent("service-a/confidential/data.txt"), "Path");
        uploadContent1.Add(new StringContent("service-a"), "ServiceName");

        var uploadResponse1 = await client1.PostAsync("/upload/v1/uploads", uploadContent1);
        Assert.Equal(HttpStatusCode.OK, uploadResponse1.StatusCode);
        var uploadResult1 = await uploadResponse1.Content.ReadFromJsonAsync<UploadResponse>();
        var uploadId = uploadResult1!.UploadId;

        // ==================== SERVICE B TRIES TO ACCESS SERVICE A'S FILE ====================
        // Should be blocked by authorization

        // Mock IAM to deny service-b access to service-a's folders
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "service-b", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var unauthorizedAccess = await client2.GetAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedAccess.StatusCode);

        // ==================== SERVICE B TRIES TO DELETE SERVICE A'S FILE ====================
        var unauthorizedDelete = await client2.DeleteAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedDelete.StatusCode);

        // ==================== SERVICE A CAN STILL ACCESS ITS OWN FILE ====================
        // Mock IAM to allow service-a back
        _iamClientMock.Setup(x => x.CheckPermissionAsync(
            "service-a", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var authorizedAccess = await client1.GetAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.OK, authorizedAccess.StatusCode);

        // ==================== CLEANUP ====================
        var deleteResponse = await client1.DeleteAsync($"/upload/v1/files/{uploadId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
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
