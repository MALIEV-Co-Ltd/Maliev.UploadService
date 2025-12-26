using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for bulk delete operation (FR-032)
/// </summary>
public class BulkDeleteRequest
{
    [Required]
    [MaxLength(100)]
    public required string ServiceId { get; set; }

    [MaxLength(500)]
    public string? PathPrefix { get; set; }

    public List<string>? UploadIds { get; set; }

    public DateTime? DeleteFilesOlderThan { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

