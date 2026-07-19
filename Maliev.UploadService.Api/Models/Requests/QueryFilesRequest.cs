using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for querying files with pagination and filtering.
/// </summary>
public class QueryFilesRequest
{
    /// <summary>
    /// Gets or sets the path prefix to filter files (e.g., "service-name/folder/").
    /// </summary>
    public string? PathPrefix { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-indexed).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size (max: 100).
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
