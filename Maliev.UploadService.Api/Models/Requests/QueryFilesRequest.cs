using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

public class QueryFilesRequest
{
    /// <summary>
    /// Path prefix to filter files (e.g., "service-name/folder/")
    /// </summary>
    public string? PathPrefix { get; set; }

    /// <summary>
    /// Page number (1-indexed)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size (max: 100)
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

