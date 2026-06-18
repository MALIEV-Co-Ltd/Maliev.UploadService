using Maliev.UploadService.Api.Services;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

public class FileValidationServiceTests
{
    [Fact]
    public async Task ValidateFileAsync_ValidTextFile_ReturnsSuccess()
    {
        // Arrange
        var service = new FileValidationService();
        var fileContent = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
        using var stream = new MemoryStream(fileContent);

        // Act
        var result = await service.ValidateFileAsync(stream, "test.txt", "text/plain", fileContent.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("hero-3d-compressed.glb", "model/gltf-binary")]
    [InlineData("scene.gltf", "model/gltf+json")]
    [InlineData("assembly.fbx", "application/x-fbx")]
    public async Task ValidateFileAsync_ModelContentType_ReturnsSuccess(string fileName, string contentType)
    {
        // Arrange
        var service = new FileValidationService();
        using var stream = new MemoryStream(new byte[] { 0x67, 0x6C, 0x54, 0x46 });

        // Act
        var result = await service.ValidateFileAsync(stream, fileName, contentType, stream.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateFileAsync_ExecutableFile_ReturnsError()
    {
        // Arrange
        var service = new FileValidationService();

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
        var service = new FileValidationService();
        using var stream = new MemoryStream();

        // Simulate a file above the 10GB service limit without allocating it.
        var largeFileSize = 11L * 1024 * 1024 * 1024;

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

    [Theory]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png")]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData("image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38 }, "image/gif")]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf")]
    [InlineData("application/zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "application/zip")]
    public async Task ValidateFileAsync_DetectsCorrectContentType(string declaredType, byte[] signature, string expectedDetectedType)
    {
        // Arrange
        var service = new FileValidationService();

        // Create file with proper signature
        var fileContent = new byte[1024];
        Array.Copy(signature, fileContent, signature.Length);
        using var stream = new MemoryStream(fileContent);

        // Act
        var result = await service.ValidateFileAsync(stream, "test.file", declaredType, fileContent.Length);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(expectedDetectedType, result.DetectedContentType);
    }

    [Fact]
    public async Task ValidateFileAsync_EmptyFile_PassesValidation()
    {
        // Arrange
        var service = new FileValidationService();
        using var stream = new MemoryStream();

        // Act
        var result = await service.ValidateFileAsync(stream, "empty.txt", "text/plain", 0);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateFileAsync_DisallowedContentType_ReturnsError()
    {
        // Arrange
        var service = new FileValidationService();
        using var stream = new MemoryStream();

        // Act
        var result = await service.ValidateFileAsync(stream, "script.js", "application/javascript", 100);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not allowed"));
    }

    [Fact]
    public async Task ValidateFileAsync_ContentTypeMismatch_ReturnsDetectedType()
    {
        // Arrange
        var service = new FileValidationService();

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

    [Fact]
    public async Task ValidateFileAsync_ExecutableSignatureWithAllowedContentType_ReturnsError()
    {
        // Arrange
        var service = new FileValidationService();
        var exeContent = new byte[] { 0x4D, 0x5A, 0x90, 0x00 };
        using var stream = new MemoryStream(exeContent);

        // Act
        var result = await service.ValidateFileAsync(
            stream,
            "fake.step",
            "application/octet-stream",
            exeContent.Length);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("application/x-msdownload", result.DetectedContentType);
        Assert.Contains(result.Errors, e => e.Contains("signature", StringComparison.OrdinalIgnoreCase));
    }
}
