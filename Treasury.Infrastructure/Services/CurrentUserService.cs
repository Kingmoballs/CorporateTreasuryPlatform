using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Treasury.Application.Interfaces;

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
}