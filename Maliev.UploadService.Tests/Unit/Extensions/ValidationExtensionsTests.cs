using Maliev.UploadService.Api.Extensions;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for ValidationExtensions (T105)
/// Tests path sanitization and security validation (FR-008, FR-027)
/// </summary>
public class ValidationExtensionsTests
{
    [Theory]
    [InlineData("service/path/file.txt", true)] // Valid path
    [InlineData("service/uploads/2024/file.pdf", true)] // Valid with subdirectories
    [InlineData("service-name/files/document_v2.txt", true)] // Valid with underscores and hyphens
    [InlineData("", false)] // Empty path
    [InlineData("  ", false)] // Whitespace only
    [InlineData("../../../etc/passwd", false)] // Path traversal with ../
    [InlineData("service/../../secret", false)] // Path traversal in middle
    [InlineData("service\\..\\windows\\file", false)] // Windows-style path traversal
    [InlineData("service//file.txt", false)] // Double slashes
    [InlineData("service/file<script>.txt", false)] // Special characters <
    [InlineData("service/file>output.txt", false)] // Special characters >
    [InlineData("service/file|pipe.txt", false)] // Pipe character
    [InlineData("service/file:colon.txt", false)] // Colon (Windows reserved)
    [InlineData("service/file*wildcard.txt", false)] // Asterisk
    [InlineData("service/file?query.txt", false)] // Question mark
    [InlineData("service/file\"quote.txt", false)] // Double quote
    [InlineData(".hidden/file.txt", false)] // Hidden file prefix
    [InlineData("C:/windows/file.txt", false)] // Windows absolute path
    public void IsValidPath_ValidatesPathCorrectly(string path, bool expectedValid)
    {
        // Act
        var result = path.IsValidPath();

        // Assert
        Assert.Equal(expectedValid, result);
    }

    [Fact]
    public void SanitizePath_ValidPath_ReturnsCleanPath()
    {
        // Arrange
        var path = "service/uploads/file.txt";

        // Act
        var result = path.SanitizePath();

        // Assert
        Assert.Equal("service/uploads/file.txt", result);
    }

    [Fact]
    public void SanitizePath_PathWithLeadingSlash_RemovesIt()
    {
        // Arrange
        var path = "/service/uploads/file.txt";

        // Act
        var result = path.SanitizePath();

        // Assert
        Assert.Equal("service/uploads/file.txt", result);
    }

    [Fact]
    public void SanitizePath_PathWithTrailingSlash_RemovesIt()
    {
        // Arrange
        var path = "service/uploads/";

        // Act
        var result = path.SanitizePath();

        // Assert
        Assert.Equal("service/uploads", result);
    }

    [Fact]
    public void SanitizePath_PathWithBackslashes_ConvertToForwardSlashes()
    {
        // Arrange
        var path = "service\\uploads\\file.txt";

        // Act
        var result = path.SanitizePath();

        // Assert
        Assert.Equal("service/uploads/file.txt", result);
    }

    [Fact]
    public void SanitizePath_PathWithMultipleSlashes_ThrowsArgumentException()
    {
        // Arrange
        var path = "service///uploads//file.txt";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.SanitizePath());
        Assert.Contains("dangerous patterns", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizePath_EmptyPath_ThrowsArgumentException(string path)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.SanitizePath());
        Assert.Contains("Path cannot be null or whitespace", ex.Message);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("service/../../secret")]
    [InlineData("service/../other")]
    public void SanitizePath_PathTraversal_ThrowsArgumentException(string path)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.SanitizePath());
        Assert.Contains("dangerous patterns", ex.Message);
    }


    [Theory]
    [InlineData("service/file<.txt")]
    [InlineData("service/file>.txt")]
    [InlineData("service/file|.txt")]
    [InlineData("service/file:.txt")]
    [InlineData("service/file*.txt")]
    [InlineData("service/file?.txt")]
    [InlineData("service/file\".txt")]
    public void SanitizePath_InvalidCharacters_ThrowsArgumentException(string path)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.SanitizePath());
        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public void SanitizePath_DotPrefix_ThrowsArgumentException()
    {
        // Arrange
        var path = ".hidden/file.txt";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.SanitizePath());
        Assert.Contains("cannot start with", ex.Message);
    }

    [Fact]
    public void ResolvePlaceholders_ValidPlaceholders_ReplacesCorrectly()
    {
        // Arrange
        var path = "service/{userId}/uploads/{timestamp}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "userId", "12345" },
            { "timestamp", "2024-01-01" }
        };

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal("service/12345/uploads/2024-01-01/file.txt", result);
    }

    [Fact]
    public void ResolvePlaceholders_PlaceholderWithSlash_SanitizesToUnderscore()
    {
        // Arrange
        var path = "service/{category}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "category", "path/with/slashes" }
        };

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal("service/path_with_slashes/file.txt", result);
    }

    [Fact]
    public void ResolvePlaceholders_PlaceholderWithBackslash_SanitizesToUnderscore()
    {
        // Arrange
        var path = "service/{category}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "category", "path\\with\\backslashes" }
        };

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal("service/path_with_backslashes/file.txt", result);
    }

    [Fact]
    public void ResolvePlaceholders_EmptyDictionary_ReturnsOriginalPath()
    {
        // Arrange
        var path = "service/uploads/file.txt";
        var placeholders = new Dictionary<string, string>();

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal(path, result);
    }

    [Fact]
    public void ResolvePlaceholders_NullDictionary_ReturnsOriginalPath()
    {
        // Arrange
        var path = "service/uploads/file.txt";

        // Act
        var result = path.ResolvePlaceholders(null!);

        // Assert
        Assert.Equal(path, result);
    }

    [Fact]
    public void ResolvePlaceholders_UnresolvedPlaceholder_ThrowsArgumentException()
    {
        // Arrange
        var path = "service/{userId}/uploads/{timestamp}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "userId", "12345" }
            // Missing timestamp placeholder
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => path.ResolvePlaceholders(placeholders));
        Assert.Contains("unresolved placeholders", ex.Message);
    }

    [Fact]
    public void ResolvePlaceholders_CaseInsensitive_ReplacesCorrectly()
    {
        // Arrange
        var path = "service/{USERID}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "userid", "12345" }
        };

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal("service/12345/file.txt", result);
    }

    [Fact]
    public void ResolvePlaceholders_MultipleSamePlaceholder_ReplacesAll()
    {
        // Arrange
        var path = "service/{id}/folder/{id}/file.txt";
        var placeholders = new Dictionary<string, string>
        {
            { "id", "ABC123" }
        };

        // Act
        var result = path.ResolvePlaceholders(placeholders);

        // Assert
        Assert.Equal("service/ABC123/folder/ABC123/file.txt", result);
    }
}
