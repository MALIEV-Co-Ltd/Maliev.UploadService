namespace Maliev.UploadService.Api.Models;

public class FileDownloadResponse
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class FileMetadataResponse
{
    public Guid FileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Subcategory { get; set; }

    // Cross-references
    public string? CustomerId { get; set; }
    public string? OrderId { get; set; }
    public string? QuotationId { get; set; }
    public string? InvoiceId { get; set; }
    public string? ReceiptId { get; set; }

    // Metadata
    public string[]? Tags { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string RetentionPolicy { get; set; } = string.Empty;
    public ProcessingStatus ProcessingStatus { get; set; }
    public string? ProcessingNotes { get; set; }
}

public class FileListQueryRequest
{
    public string? Category { get; set; }
    public string? EntityId { get; set; }
    public string? CustomerId { get; set; }
    public string? OrderId { get; set; }
    public AccessLevel? AccessLevel { get; set; }
    public ProcessingStatus? ProcessingStatus { get; set; }
    public DateTime? UploadedAfter { get; set; }
    public DateTime? UploadedBefore { get; set; }
    public string? ContentType { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "UploadedAt";
    public bool SortDescending { get; set; } = true;
}

public class FileListResponse
{
    public List<FileMetadataResponse> Files { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class SignedUrlResponse
{
    public Guid FileId { get; set; }
    public string SignedUrl { get; set; } = string.Empty;
    public string ObjectPath { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}