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
     * Only work that genuinely has no HTTP request receives
     * system-wide access. An HTTP request with a missing or
     * invalid organization claim sees no tenant data.
     */
    public bool IsSystemScope =>
        _httpContextAccessor.HttpContext is null;
}
