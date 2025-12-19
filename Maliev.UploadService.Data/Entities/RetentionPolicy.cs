using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Entities;

public class RetentionPolicy
{
    [Key]
    [Required]
    public required string PolicyId { get; set; }

    [Required]
    public required string PolicyName { get; set; }

    public string? ServiceId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public required int RetentionDays { get; set; }

    public List<StorageClassTransition>? StorageClassTransitions { get; set; }

    public string? ApplyToPathPrefix { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public required DateTime CreatedAt { get; set; }

    [Required]
    public required DateTime UpdatedAt { get; set; }
}

public class StorageClassTransition
{
    [Required]
    public required int Days { get; set; }

    [Required]
    public required string StorageClass { get; set; }
}
