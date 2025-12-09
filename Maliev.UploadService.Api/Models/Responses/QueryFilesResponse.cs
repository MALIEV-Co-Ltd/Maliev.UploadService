namespace Maliev.UploadService.Api.Models.Responses;

public class QueryFilesResponse
{
    public List<FileMetadataResponse> Files { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
