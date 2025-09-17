using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public required string Issuer { get; set; }

    [Required]
    public required string Audience { get; set; }

    [Required]
    public required string SecurityKey { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int ExpirationMinutes { get; set; } = 30;
}