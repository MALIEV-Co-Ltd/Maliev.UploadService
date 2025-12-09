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

public class UploadsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _authToken = null!;

    public UploadsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _authToken = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UploadFile_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("test-service/uploads/test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Expected OK but got {response.StatusCode}. Error: {errorContent}");
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, Guid.Parse(result!.UploadId));
        Assert.Equal("test-service/uploads/test.txt", result.StoragePath);
        Assert.Equal("text/plain", result.ContentType);
        Assert.True(result.FileSize > 0);
    }

    [Fact]
    public async Task UploadFile_FileSizeExceedsLimit_ReturnsBadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        // Create a 100MB+ file (exceeds typical limit)
        var largeFile = new byte[100 * 1024 * 1024 + 1];
        var fileContent = new ByteArrayContent(largeFile);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "large.bin");
        content.Add(new StringContent("test-service/uploads/large.bin"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("size", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        // Upload executable file which should be blocked
        var fileContent = new ByteArrayContent(new byte[] { 0x4D, 0x5A }); // MZ header (EXE)
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fileContent, "File", "malicious.exe");
        content.Add(new StringContent("test-service/uploads/file.exe"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("content type", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_MissingAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var clientWithoutAuth = _factory.CreateClient();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "test.txt");
        content.Add(new StringContent("test-service/uploads/test.txt"), "Path");

        // Act
        var response = await clientWithoutAuth.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private string GenerateJwtToken(string serviceName, string audience)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim("service_name", serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

    // T102: Test path placeholder resolution (FR-007, FR-009)
    [Fact]
    public async Task UploadFile_WithPathPlaceholders_ResolvesPlaceholders()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "document.txt");
        // Path with {timestamp} and {id} placeholders
        content.Add(new StringContent("test-service/uploads/{timestamp}/document-{id}.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);

        // Verify path placeholders were resolved
        Assert.DoesNotContain("{timestamp}", result!.StoragePath);
        Assert.DoesNotContain("{id}", result.StoragePath);
        Assert.StartsWith("test-service/uploads/", result.StoragePath);
        Assert.EndsWith(".txt", result.StoragePath);
    }

    // T103: Test path collision detection (FR-010)
    [Fact]
    public async Task UploadFile_ToExistingPath_WithoutOverwrite_ReturnsConflict()
    {
        // Arrange - First upload
        var content1 = new MultipartFormDataContent();
        var fileContent1 = new ByteArrayContent(Encoding.UTF8.GetBytes("First file"));
        fileContent1.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content1.Add(fileContent1, "File", "test.txt");
        content1.Add(new StringContent("test-service/collision/test.txt"), "Path");
        content1.Add(new StringContent("test-service"), "ServiceName");
        content1.Add(new StringContent("false"), "AllowOverwrite"); // Explicitly disable overwrite

        var response1 = await _client.PostAsync("/api/v1/uploads", content1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Arrange - Second upload to same path
        var content2 = new MultipartFormDataContent();
        var fileContent2 = new ByteArrayContent(Encoding.UTF8.GetBytes("Second file"));
        fileContent2.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content2.Add(fileContent2, "File", "test.txt");
        content2.Add(new StringContent("test-service/collision/test.txt"), "Path");
        content2.Add(new StringContent("test-service"), "ServiceName");
        content2.Add(new StringContent("false"), "AllowOverwrite");

        // Act
        var response2 = await _client.PostAsync("/api/v1/uploads", content2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        var errorMessage = await response2.Content.ReadAsStringAsync();
        Assert.Contains("already exists", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // T104: Test path traversal attack prevention (FR-008)
    [Theory]
    [InlineData("test-service/../../../etc/passwd")]
    [InlineData("test-service/uploads/../../secret.txt")]
    [InlineData("test-service/uploads/..\\..\\windows\\system32\\config")]
    [InlineData("test-service/uploads/.//../sensitive.txt")]
    public async Task UploadFile_WithPathTraversal_ReturnsBadRequest(string maliciousPath)
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Malicious content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "file.txt");
        content.Add(new StringContent(maliciousPath), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("path", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // T138: Test large file streaming upload (FR-001, FR-023)
    [Fact]
    public async Task UploadFile_LargeFileStreaming_UploadsSuccessfully()
    {
        // Arrange - Create a 50MB file for streaming test
        var largeFileSize = 50 * 1024 * 1024; // 50MB
        var largeFile = new byte[largeFileSize];
        new Random().NextBytes(largeFile); // Fill with random data

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(largeFile);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "File", "largefile.bin");
        content.Add(new StringContent("test-service/large/streaming-test.bin"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);
        Assert.Equal(largeFileSize, result!.FileSize);
        Assert.Equal("application/octet-stream", result.ContentType);
    }

    // T139: Test resumable upload initiation (FR-022)
    [Fact]
    public async Task InitiateResumableUpload_ValidRequest_ReturnsSessionUri()
    {
        // Arrange
        var request = new
        {
            Path = "test-service/resumable/large-file.bin",
            ServiceName = "test-service",
            ContentType = "application/octet-stream",
            TotalSize = 100 * 1024 * 1024 // 100MB
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/uploads/resumable", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(result);

        // Should return uploadId and sessionUri
        var uploadId = result?.GetProperty("uploadId").GetString();
        var sessionUri = result?.GetProperty("sessionUri").GetString();

        Assert.NotNull(uploadId);
        Assert.NotNull(sessionUri);
        Assert.NotEqual(Guid.Empty, Guid.Parse(uploadId!));
    }

    // T140: Test resumable upload continuation (FR-022)
    [Fact]
    public async Task ResumeUpload_WithValidSession_UploadsChunk()
    {
        // Arrange - First initiate a resumable upload
        var initiateRequest = new
        {
            Path = "test-service/resumable/resume-test.bin",
            ServiceName = "test-service",
            ContentType = "application/octet-stream",
            TotalSize = 10 * 1024 * 1024 // 10MB
        };

        var initiateResponse = await _client.PostAsJsonAsync("/api/v1/uploads/resumable", initiateRequest);
        Assert.Equal(HttpStatusCode.OK, initiateResponse.StatusCode);

        var initiateResult = await initiateResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var uploadId = initiateResult.GetProperty("uploadId").GetString();

        // Prepare chunk data (1MB)
        var chunkSize = 1024 * 1024;
        var chunkData = new byte[chunkSize];
        new Random().NextBytes(chunkData);

        // Act - Resume upload with first chunk
        var chunkContent = new ByteArrayContent(chunkData);
        chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        chunkContent.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, chunkSize - 1, 10 * 1024 * 1024);

        var resumeResponse = await _client.PutAsync($"/api/v1/uploads/resumable/{uploadId}", chunkContent);

        // Assert
        // Should return 200 OK for completed or 308 Resume Incomplete for partial upload
        Assert.True(
            resumeResponse.StatusCode == HttpStatusCode.OK ||
            resumeResponse.StatusCode == (HttpStatusCode)308,
            $"Expected OK or 308, got {resumeResponse.StatusCode}"
        );
    }
}
