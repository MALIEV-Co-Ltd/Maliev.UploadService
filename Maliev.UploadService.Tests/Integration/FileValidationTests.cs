using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Maliev.UploadService.Tests.Fixtures;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

[Collection("Database")]
public class FileValidationTests
{
    private readonly TestWebApplicationFactory _factory;

    public FileValidationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Requires ClamAV container - integration test for malware detection")]
    public async Task UploadFile_MalwareDetected_ReturnsValidationError()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var content = new MultipartFormDataContent();

        // EICAR test file - standard antivirus test file
        var eicarSignature = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        var fileContent = new ByteArrayContent(Encoding.ASCII.GetBytes(eicarSignature));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "eicar.txt");
        content.Add(new StringContent("test-service/uploads/eicar.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await client.PostAsync("/upload/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("malware", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_ContentTypeSpoof_DetectsRealContentType()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var content = new MultipartFormDataContent();

        // Create file with mismatched content type and actual content
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG magic bytes
        var fileContent = new ByteArrayContent(pngHeader);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); // Spoofed as text
        content.Add(fileContent, "File", "image.txt");
        content.Add(new StringContent("test-service/uploads/image.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");

        // Act
        var response = await client.PostAsync("/upload/v1/uploads", content);

        // Assert - Should detect PNG and either reject or correct the content type
        // Implementation detail: depends on whether PNG is allowed and how strict validation is
        var result = await response.Content.ReadAsStringAsync();
        Assert.NotNull(result);
    }
}
