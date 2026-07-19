using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Maliev.UploadService.Tests.Unit.Services;

public sealed class UploadCallerContextTests
{
    [Fact]
    public void GetRequired_ManagedService_UsesSubjectAsPrincipalAndClaimAsLogicalService()
    {
        var principalId = Guid.NewGuid().ToString("D");
        var context = CreateContext(
            new Claim(JwtRegisteredClaimNames.Sub, principalId),
            new Claim("service_name", "ContactService"),
            new Claim("user_type", "service"));

        var caller = context.GetRequired();

        Assert.Equal(principalId, caller.PrincipalId);
        Assert.Equal("ContactService", caller.TrustedServiceName);
        Assert.True(caller.IsService);
        Assert.True(caller.IsManagedService);
        Assert.Null(caller.LegacyPolicyServiceId);
    }

    [Fact]
    public void GetRequired_LegacyService_ExposesOnlySignedServiceNameForLegacyFallback()
    {
        var context = CreateContext(
            new Claim(JwtRegisteredClaimNames.Sub, "system:service:legacy"),
            new Claim("service_name", "LegacyService"),
            new Claim("user_type", "service"));

        var caller = context.GetRequired();

        Assert.Equal("system:service:legacy", caller.PrincipalId);
        Assert.Equal("LegacyService", caller.LegacyPolicyServiceId);
        Assert.False(caller.IsManagedService);
    }

    [Fact]
    public void GetRequired_Employee_IgnoresServiceNameClaimForLegacyFallback()
    {
        var principalId = Guid.NewGuid().ToString("D");
        var context = CreateContext(
            new Claim(JwtRegisteredClaimNames.Sub, principalId),
            new Claim("service_name", "SpoofedService"),
            new Claim("user_type", "employee"));

        var caller = context.GetRequired();

        Assert.Equal(principalId, caller.PrincipalId);
        Assert.Null(caller.TrustedServiceName);
        Assert.Null(caller.LegacyPolicyServiceId);
        Assert.False(caller.IsService);
    }

    private static UploadCallerContext CreateContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        return new UploadCallerContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
