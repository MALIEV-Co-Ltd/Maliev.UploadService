using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    public int ExpirationInMinutes { get; set; } = 60;
}