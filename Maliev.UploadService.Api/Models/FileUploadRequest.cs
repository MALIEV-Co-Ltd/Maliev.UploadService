using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models;

/// <summary>
/// Clean, path-based file upload request model
/// No legacy business category support - only object paths
/// </summary>
public class FileUploadRequest
{
    /// <summary>
    /// The file to upload
    /// </summary>
    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Full object path where the file should be stored
    /// Example: "quotations/QUO-001/documents/contract.pdf"
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string ObjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional storage configuration
    /// </summary>
    public StorageOptions? StorageOptions { get; set; }

    /// <summary>
    /// Optional metadata for the file
    /// </summary>
    public Dictionary<string, string>? ServiceMetadata { get; set; }

    /// <summary>
    /// Access level for the file
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = AccessLevel.Internal;

    /// <summary>
    /// Validate that the request is properly formed
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ObjectPath) && File != null;
    }
}