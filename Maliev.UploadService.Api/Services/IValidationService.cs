namespace Maliev.UploadService.Api.Services;

public interface IValidationService
{
    Task<ValidationResult> ValidateFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? DetectedContentType { get; set; }
}
