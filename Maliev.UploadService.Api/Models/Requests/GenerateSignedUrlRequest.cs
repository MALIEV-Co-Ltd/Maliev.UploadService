using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

public class GenerateSignedUrlRequest
{
    /// <summary>
    /// Expiration time in minutes (default: 60, max: 10080 = 7 days)
    /// </summary>
    [Range(1, 10080)]
    public int ExpirationMinutes { get; set; } = 60;
}
