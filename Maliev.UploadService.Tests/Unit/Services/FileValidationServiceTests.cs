using Maliev.UploadService.Api.Services;
using Moq;
using nClam;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

public class FileValidationServiceTests
{
    [Fact]
    public async Task ValidateFileAsync_ValidTextFile_ReturnsSuccess()
    {
        // Arrange
        var mockClamClient = new Mock<IClamClient>();
        // Clean scan result - raw result indicates no virus found
        var cleanScanResult = new ClamScanResult("stream: OK");
        mockClamClient
            .Setup(x => x.SendAndScanFileAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleanScanResult);

        var service = new FileValidationService(mockClamClient.Object);
        var fileContent = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
        using var stream = new MemoryStream(fileContent);

        // Act
        var result = await service.ValidateFileAsync(stream, "test.txt", "text/plain", fileContent.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateFileAsync_ExecutableFile_ReturnsError()
    {
        // Arrange
        var mockClamClient = new Mock<IClamClient>();
        var service = new FileValidationService(mockClamClient.Object);

        // MZ header indicates executable
        var exeContent = new byte[] { 0x4D, 0x5A, 0x90, 0x00 };
        using var stream = new MemoryStream(exeContent);

        // Act
        var result = await service.ValidateFileAsync(
            stream,
            "malicious.exe",
            "application/x-msdownload",
            exeContent.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("content type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateFileAsync_FileTooLarge_ReturnsError()
    {
        // Arrange
        var mockClamClient = new Mock<IClamClient>();
        var service = new FileValidationService(mockClamClient.Object);
        using var stream = new MemoryStream();

        // Simulate 200MB file (exceeds typical limit)
        var largeFileSize = 200L * 1024 * 1024;

        // Act
        var result = await service.ValidateFileAsync(
            stream,
            "large.bin",
            "application/octet-stream",
            largeFileSize);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateFileAsync_MalwareDetected_ReturnsError()
    {
        // Arrange
        var mockClamClient = new Mock<IClamClient>();
        // Infected scan result - raw result indicates virus found
        var infectedScanResult = new ClamScanResult("stream: EICAR-Test-File FOUND");
        mockClamClient
            .Setup(x => x.SendAndScanFileAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(infectedScanResult);

        var service = new FileValidationService(mockClamClient.Object);
        var fileContent = System.Text.Encoding.UTF8.GetBytes("test content");
        using var stream = new MemoryStream(fileContent);

        // Act
        var result = await service.ValidateFileAsync(stream, "test.txt", "text/plain", fileContent.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("malware", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateFileAsync_ContentTypeMismatch_ReturnsDetectedType()
    {
        // Arrange
        var mockClamClient = new Mock<IClamClient>();
        // Clean scan result
        var cleanScanResult = new ClamScanResult("stream: OK");
        mockClamClient
            .Setup(x => x.SendAndScanFileAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cleanScanResult);

        var service = new FileValidationService(mockClamClient.Object);

        // PNG file header
        var pngContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(pngContent);

        // Act - claim it's text/plain but it's actually PNG
        var result = await service.ValidateFileAsync(
            stream,
            "fake.txt",
            "text/plain",
            pngContent.Length);

        // Assert - should detect actual content type
        Assert.NotNull(result.DetectedContentType);
        Assert.Contains("image", result.DetectedContentType, StringComparison.OrdinalIgnoreCase);
    }
}
