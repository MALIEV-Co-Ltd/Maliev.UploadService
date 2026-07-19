namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Interface for file validation service.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates a file before upload.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="sizeBytes">The file size in bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of file validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the file is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the detected content type.
    /// </summary>
    public string? DetectedContentType { get; set; }
}
