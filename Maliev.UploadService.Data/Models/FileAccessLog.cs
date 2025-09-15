using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Models;

public class FileAccessLog
{
    [Key]
    public long Id { get; set; }

    [Required]
    public Guid FileId { get; set; }

    public UploadedFile File { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public AccessType AccessType { get; set; }

    [Required]
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string AccessedBy { get; set; } = string.Empty;

    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(1000)]
    public string? AdditionalInfo { get; set; }

    public bool IsSuccess { get; set; } = true;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

public enum AccessType
{
    Upload,
    Download,
    View,
    Delete,
    Update,
    List
}