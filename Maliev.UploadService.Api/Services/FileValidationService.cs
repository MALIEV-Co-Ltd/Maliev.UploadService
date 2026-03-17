namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Service for validating uploaded files.
/// </summary>
public class FileValidationService : IValidationService
{
    private readonly long _maxFileSizeBytes = 100 * 1024 * 1024; // 100MB default
    private readonly HashSet<string> _allowedContentTypes = new()
    {
        "text/plain",
        "text/csv",
        "application/json",
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/zip",
        "application/octet-stream"
    };

    /// <summary>
    /// Validates a file before upload.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="sizeBytes">The file size in bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<ValidationResult> ValidateFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate file size
        if (sizeBytes > _maxFileSizeBytes)
        {
            result.IsValid = false;
            result.Errors.Add($"File size {sizeBytes} bytes exceeds maximum allowed size of {_maxFileSizeBytes} bytes");
            return result;
        }

        // Validate content type
        if (!_allowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            result.IsValid = false;
            result.Errors.Add($"Content type '{contentType}' is not allowed");
            return result;
        }

        // Detect actual content type from file content (basic header-based detection)
        try
        {
            var position = fileStream.Position;
            var buffer = new byte[16];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            fileStream.Position = position; // Reset stream position

            // Basic file signature detection
            var detectedType = DetectContentTypeFromSignature(buffer, bytesRead);
            if (!string.IsNullOrEmpty(detectedType))
            {
                result.DetectedContentType = detectedType;
            }
        }
        catch (Exception ex)
        {
            // Content type detection failure is non-fatal
            result.Warnings.Add($"Content type detection warning: {ex.Message}");
        }

        return result;
    }

    private static string? DetectContentTypeFromSignature(byte[] buffer, int bytesRead)
    {
        if (bytesRead < 4) return null;

        // PNG: 89 50 4E 47
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return "image/png";

        // JPEG: FF D8 FF
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            return "image/jpeg";

        // GIF: 47 49 46 38
        if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
            return "image/gif";

        // PDF: 25 50 44 46
        if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
            return "application/pdf";

        // ZIP: 50 4B 03 04
        if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
            return "application/zip";

        // EXE (MZ header): 4D 5A
        if (buffer[0] == 0x4D && buffer[1] == 0x5A)
            return "application/x-msdownload";

        return null;
    }
}
