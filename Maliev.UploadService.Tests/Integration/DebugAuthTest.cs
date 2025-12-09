using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.UploadService.Tests.Integration;

public class DebugAuthTest : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public DebugAuthTest(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task DebugAuthorizationFlow()
    {
        var client = _factory.CreateClient();
        var testServiceToken = GenerateJwtToken("test-service", "uploadservice");
        var otherServiceToken = GenerateJwtToken("other-service", "uploadservice");

        // Upload a file as test-service
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", testServiceToken);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Private file"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "private.txt");
        content.Add(new StringContent("test-service/private/private.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        var uploadResponse = await client.PostAsync("/api/v1/uploads", content);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        var uploadId = uploadResult!.UploadId;

        _output.WriteLine($"Upload ID: {uploadId}");
        _output.WriteLine($"Storage Path: {uploadResult.StoragePath}");

        // Get file metadata using the upload service's database context
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Api.Data.UploadServiceDbContext>();
        var fileMetadata = await dbContext.FileMetadata.FindAsync(uploadId);

        _output.WriteLine($"FileMetadata UploadId: {fileMetadata?.UploadId}");
        _output.WriteLine($"FileMetadata StoragePath: {fileMetadata?.StoragePath}");
        _output.WriteLine($"FileMetadata ServiceId: {fileMetadata?.ServiceId}");

        // Try to access as other-service
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherServiceToken);
        var response = await client.GetAsync($"/api/v1/files/{uploadId}");

        _output.WriteLine($"Response Status: {response.StatusCode}");
        var responseBody = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Response Body: {responseBody}");
    }

    private string GenerateJwtToken(string serviceId, string audience)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceId),
            new Claim(ClaimTypes.NameIdentifier, serviceId),
            new Claim("sub", serviceId),
        };

        var credentials = _factory.SigningCredentials;
        var token = new JwtSecurityToken(
            issuer: "https://test.maliev.com",
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
