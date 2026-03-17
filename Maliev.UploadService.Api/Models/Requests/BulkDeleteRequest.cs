using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for bulk delete operation (FR-032)
/// </summary>
public class BulkDeleteRequest
{
    /// <summary>
    /// Gets or sets the service identifier.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ServiceId { get; set; }

    /// <summary>
    /// Gets or sets the path prefix to filter files for deletion.
    /// </summary>
    [MaxLength(500)]
    public string? PathPrefix { get; set; }

    /// <summary>
    /// Gets or sets the list of specific upload IDs to delete.
    /// </summary>
    public List<string>? UploadIds { get; set; }

    /// <summary>
    /// Gets or sets the date threshold for deleting files older than this date.
    /// </summary>
    public DateTime? DeleteFilesOlderThan { get; set; }

    /// <summary>
    /// Gets or sets the reason for the bulk delete operation.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
