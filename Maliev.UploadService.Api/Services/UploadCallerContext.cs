using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Maliev.UploadService.Api.Services;

/// <summary>Describes the authenticated identity used for upload authorization and ownership.</summary>
/// <param name="PrincipalId">Authoritative JWT subject or name identifier.</param>
/// <param name="TrustedServiceName">Logical service name taken only from a signed service token.</param>
/// <param name="IsService">Whether the caller is a service identity.</param>
/// <param name="IsManagedService">Whether the service identity is backed by an IAM GUID principal.</param>
public sealed record UploadCaller(
    string PrincipalId,
    string? TrustedServiceName,
    bool IsService,
    bool IsManagedService)
{
    /// <summary>Gets the trusted policy key for reviewed legacy service fallback.</summary>
    public string? LegacyPolicyServiceId => IsService && !IsManagedService ? TrustedServiceName : null;
}

/// <summary>Resolves the authenticated upload caller from the current request.</summary>
public sealed class UploadCallerContext(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Gets the authenticated caller or fails closed when no authoritative subject exists.</summary>
    /// <returns>The authenticated upload caller.</returns>
    /// <exception cref="InvalidOperationException">The request has no authenticated subject.</exception>
    public UploadCaller GetRequired()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var principalId = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(principalId))
        {
            throw new InvalidOperationException("An authenticated JWT subject is required for upload operations.");
        }

        var isService = string.Equals(
            user.FindFirst("user_type")?.Value,
            "service",
            StringComparison.OrdinalIgnoreCase);
        var trustedServiceName = isService ? user.FindFirst("service_name")?.Value?.Trim() : null;
        if (string.IsNullOrWhiteSpace(trustedServiceName))
        {
            trustedServiceName = null;
        }

        return new UploadCaller(
            principalId.Trim(),
            trustedServiceName,
            isService,
            isService && Guid.TryParse(principalId, out _));
    }
}
