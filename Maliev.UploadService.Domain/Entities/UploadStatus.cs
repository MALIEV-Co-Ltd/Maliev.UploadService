namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Represents the lifecycle state of a file upload.
/// </summary>
public enum UploadStatus
{
    /// <summary>Upload session created but not yet started.</summary>
    Pending,

    /// <summary>Upload is currently in progress.</summary>
    InProgress,

    /// <summary>Upload completed and file is being validated.</summary>
    Validating,

    /// <summary>Upload completed and file is available.</summary>
    Completed,

    /// <summary>Upload failed due to an error.</summary>
    Failed
}
