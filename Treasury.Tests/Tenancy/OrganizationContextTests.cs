using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Tenancy;

public class OrganizationContextTests
{
    [Fact]
    public void
        PlatformRoleOutsideReservedOrganizationDoesNotReceiveSystemScope()
    {
        var accessor =
            CreateAccessor(
                Roles.PlatformAdmin,
                OrganizationDefaults
                    .OrganizationCode);

        var context =
            new OrganizationContext(accessor);

        Assert.False(context.IsSystemScope);
    }

    [Fact]
    public void
        PlatformRoleInReservedOrganizationReceivesSystemScope()
    {
        var accessor =
            CreateAccessor(
                Roles.PlatformAdmin,
                PlatformDefaults.OrganizationCode);

        var context =
            new OrganizationContext(accessor);

        Assert.True(context.IsSystemScope);
    }

    private static IHttpContextAccessor
        CreateAccessor(
            string role,
            string organizationCode)
    {
        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.Role,
                        role),
                    new Claim(
                        CustomClaimTypes
                            .OrganizationCode,
                        organizationCode)
                },
                authenticationType: "Test");

        return new HttpContextAccessor
        {
            HttpContext =
                new DefaultHttpContext
                {
                    User =
                        new ClaimsPrincipal(
                            identity)
                }
        };
    }
}
