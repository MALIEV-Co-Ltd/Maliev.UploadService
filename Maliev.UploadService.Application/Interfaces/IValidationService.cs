namespace Maliev.UploadService.Application.Interfaces;

/// <summary>
/// Validates files before they are persisted to cloud storage.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates a file stream against size, content type, and file signature rules.
    /// </summary>
    /// <param name="fileStream">The file stream to validate.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The declared MIME content type.</param>
    /// <param name="sizeBytes">The declared file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValidationResult"/> describing validation outcome.</returns>
    Task<ValidationResult> ValidateFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Encapsulates the result of a file validation check.
/// </summary>
public class ValidationResult
{
    /// <summary>Gets or sets whether the file passed all validation rules.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets the list of validation error messages.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Gets or sets the list of non-fatal validation warnings.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Gets or sets the detected MIME content type from file signature analysis.</summary>
    public string? DetectedContentType { get; set; }
}
