using Maliev.UploadService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.UploadService.Infrastructure.Services;

/// <summary>
/// Validates files against size, content type, and file signature rules.
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

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        if (sizeBytes > _maxFileSizeBytes)
        {
            result.IsValid = false;
            result.Errors.Add($"File size {sizeBytes} bytes exceeds maximum allowed size of {_maxFileSizeBytes} bytes");
            return result;
        }

        if (!_allowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            result.IsValid = false;
            result.Errors.Add($"Content type '{contentType}' is not allowed");
            return result;
        }

        try
        {
            var position = fileStream.Position;
            var buffer = new byte[16];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            fileStream.Position = position;

            var detectedType = DetectContentTypeFromSignature(buffer, bytesRead);
            if (!string.IsNullOrEmpty(detectedType))
            {
                result.DetectedContentType = detectedType;
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Content type detection warning: {ex.Message}");
        }

        return result;
    }

    private static string? DetectContentTypeFromSignature(byte[] buffer, int bytesRead)
    {
        if (bytesRead < 4) return null;

        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return "image/png";

        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            return "image/jpeg";

        if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
            return "image/gif";

        if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
            return "application/pdf";

        if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
            return "application/zip";

        if (buffer[0] == 0x4D && buffer[1] == 0x5A)
            return "application/x-msdownload";

        return null;
    }
}
