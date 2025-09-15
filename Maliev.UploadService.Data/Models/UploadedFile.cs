using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Models;

public class UploadedFile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Subcategory { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string ObjectName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    [MaxLength(100)]
    public string Bucket { get; set; } = string.Empty;

    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string UploadedBy { get; set; } = string.Empty;

    // Cross-reference fields for business entities
    [MaxLength(100)]
    public string? CustomerId { get; set; }

    [MaxLength(100)]
    public string? OrderId { get; set; }

    [MaxLength(100)]
    public string? QuotationId { get; set; }

    [MaxLength(100)]
    public string? InvoiceId { get; set; }

    [MaxLength(100)]
    public string? ReceiptId { get; set; }

    // Metadata fields
    public string Tags { get; set; } = string.Empty; // JSON array of tags

    [Required]
    [MaxLength(20)]
    public AccessLevel AccessLevel { get; set; } = AccessLevel.Internal;

    [Required]
    [MaxLength(50)]
    public string RetentionPolicy { get; set; } = string.Empty;

    // File processing status
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Completed;

    [MaxLength(1000)]
    public string? ProcessingNotes { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    [MaxLength(100)]
    public string? DeletedBy { get; set; }

    // Checksums for integrity
    [MaxLength(64)]
    public string? Md5Hash { get; set; }

    [MaxLength(128)]
    public string? Sha256Hash { get; set; }
}

public enum AccessLevel
{
    Public,
    Internal,
    Restricted,
    Confidential
}

public enum ProcessingStatus
{
    Uploading,
    Processing,
    Completed,
    Failed,
    Quarantine
}