namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for file query results with pagination.
/// </summary>
public class QueryFilesResponse
{
    /// <summary>
    /// Gets or sets the list of file metadata responses.
    /// </summary>
    public List<FileMetadataResponse> Files { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of files matching the query.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }
}
