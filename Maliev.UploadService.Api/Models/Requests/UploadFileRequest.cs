using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

public class UploadFileRequest
{
    [Required]
    public required IFormFile File { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Path { get; set; }

    [MaxLength(100)]
    public string? ServiceName { get; set; }

    public bool Overwrite { get; set; } = false;

    [MaxLength(1000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// Optional retention policy ID to apply to uploaded file
    /// </summary>
    [MaxLength(50)]
    public string? RetentionPolicyId { get; set; }
}

