using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class OrganizationContext
    : IOrganizationContext
{
    private readonly IHttpContextAccessor
        _httpContextAccessor;

    public OrganizationContext(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor =
            httpContextAccessor;
    }

    public Guid? OrganizationId
    {
        get
        {
            var value =
                _httpContextAccessor.HttpContext?
                    .User
                    .FindFirstValue(
                        CustomClaimTypes
                            .OrganizationId);

            return Guid.TryParse(
                value,
                out var organizationId)
                    ? organizationId
                    : null;
        }
    }

    /*
     * Background work without an HTTP request and an
     * authenticated PlatformAdmin may operate across
     * organization boundaries.
     */
    public bool IsSystemScope
    {
        get
        {
            var httpContext =
                _httpContextAccessor.HttpContext;

            return httpContext is null ||
                   (httpContext.User.Identity?
                        .IsAuthenticated == true &&
                    httpContext.User.IsInRole(
                        Roles.PlatformAdmin) &&
                    string.Equals(
                        httpContext.User
                            .FindFirstValue(
                                CustomClaimTypes
                                    .OrganizationCode),
                        PlatformDefaults
                            .OrganizationCode,
                        StringComparison.Ordinal));
        }
    }
}
