using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for generating a signed URL for file download.
/// </summary>
public class GenerateSignedUrlRequest
{
    /// <summary>
    /// Gets or sets the expiration time in minutes (default: 60, max: 10080 = 7 days).
    /// </summary>
    [Range(1, 10080)]
    public int ExpirationMinutes { get; set; } = 60;
}
