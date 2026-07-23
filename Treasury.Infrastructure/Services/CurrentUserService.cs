using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Treasury.Application.Interfaces;
using Treasury.Shared.Constants;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId =>
        Guid.Parse(
            _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier)!);

    public string Email =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(
                ClaimTypes.Email)
        ?? string.Empty;

    public string Role =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(
                ClaimTypes.Role)
        ?? string.Empty;

    public Guid? OrganizationId =>
        ParseGuidClaim(
            CustomClaimTypes.OrganizationId);

    public Guid? OrganizationMembershipId =>
        ParseGuidClaim(
            CustomClaimTypes
                .OrganizationMembershipId);

    public string OrganizationCode =>
        _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(
                CustomClaimTypes.OrganizationCode)
        ?? string.Empty;

    private Guid? ParseGuidClaim(
        string claimType)
    {
        var value =
            _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(claimType);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }
}
