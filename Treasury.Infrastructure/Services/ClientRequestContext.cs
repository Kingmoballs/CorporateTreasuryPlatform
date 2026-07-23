using Microsoft.AspNetCore.Http;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Services;

public class ClientRequestContext
    : IClientRequestContext
{
    private readonly IHttpContextAccessor
        _httpContextAccessor;

    public ClientRequestContext(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor =
            httpContextAccessor;
    }

    public string? IpAddress =>
        Truncate(
            _httpContextAccessor
                .HttpContext?
                .Connection
                .RemoteIpAddress?
                .ToString(),
            64);

    public string? UserAgent =>
        Truncate(
            _httpContextAccessor
                .HttpContext?
                .Request
                .Headers
                .UserAgent
                .ToString(),
            512);

    private static string? Truncate(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maximumLength
            ? trimmed
            : trimmed[..maximumLength];
    }
}
