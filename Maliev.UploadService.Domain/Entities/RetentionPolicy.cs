using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Defines rules for how long files are retained and how their storage class transitions over time.
/// </summary>
public class RetentionPolicy
{
    /// <summary>Gets or sets the unique policy identifier.</summary>
    [Key]
    [Required]
    public required string PolicyId { get; set; }

    /// <summary>Gets or sets the human-readable policy name.</summary>
    [Required]
    public required string PolicyName { get; set; }

    /// <summary>Gets or sets the service this policy applies to (null = all services).</summary>
    public string? ServiceId { get; set; }

    /// <summary>Gets or sets the number of days to retain files (0 = indefinite).</summary>
    [Required]
    [Range(0, int.MaxValue)]
    public required int RetentionDays { get; set; }

    /// <summary>Gets or sets the list of storage class transitions ordered by age in days.</summary>
    public List<StorageClassTransition>? StorageClassTransitions { get; set; }

    /// <summary>Gets or sets the path prefix this policy applies to (null = all paths).</summary>
    public string? ApplyToPathPrefix { get; set; }

    /// <summary>Gets or sets whether this policy is currently active.</summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets when this policy was created.</summary>
    [Required]
    public required DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when this policy was last updated.</summary>
    [Required]
    public required DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Defines a storage class transition rule — the file moves to <see cref="StorageClass"/> after <see cref="Days"/> days.
/// </summary>
public class StorageClassTransition
{
    /// <summary>Gets or sets the age in days after which the transition occurs.</summary>
    [Required]
    public required int Days { get; set; }

    /// <summary>Gets or sets the target GCS storage class (NEARLINE, COLDLINE, ARCHIVE).</summary>
    [Required]
    public required string StorageClass { get; set; }
}
