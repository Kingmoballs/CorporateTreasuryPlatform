using Microsoft.Extensions.Options;
using Treasury.Api.Configuration;

namespace Treasury.Api.Security;

public class RefreshTokenCookieService
    : IRefreshTokenCookieService
{
    public const string ClientRequestHeaderName =
        "X-Treasury-Client";

    public const string ClientRequestHeaderValue =
        "web";

    private const int MaximumTokenLength = 512;

    private readonly RefreshTokenCookieOptions
        _options;

    public RefreshTokenCookieService(
        IOptions<RefreshTokenCookieOptions> options)
    {
        _options = options.Value;
    }

    public string GetRequiredToken(
        HttpRequest request,
        string? clientRequestHeader)
    {
        /*
         * This non-simple header forces cross-origin browser
         * callers through the configured CORS preflight before
         * the refresh cookie can cause a state-changing request.
         */
        if (!string.Equals(
                clientRequestHeader,
                ClientRequestHeaderValue,
                StringComparison.Ordinal))
        {
            throw InvalidRefreshRequest();
        }

        if (!request.Cookies.TryGetValue(
                _options.Name,
                out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken) ||
            refreshToken.Length > MaximumTokenLength)
        {
            throw InvalidRefreshRequest();
        }

        return refreshToken;
    }

    public void Append(
        HttpResponse response,
        string refreshToken,
        DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) ||
            refreshToken.Length > MaximumTokenLength)
        {
            throw new ArgumentException(
                "A valid refresh token is required.",
                nameof(refreshToken));
        }

        response.Cookies.Append(
            _options.Name,
            refreshToken,
            CreateCookieOptions(expiresAtUtc));
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(
            _options.Name,
            CreateCookieOptions(null));
    }

    private CookieOptions CreateCookieOptions(
        DateTime? expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _options.Secure,
            SameSite = _options.SameSite,
            Path = _options.Path,
            IsEssential = true,
            Expires = expiresAtUtc.HasValue
                ? new DateTimeOffset(
                    DateTime.SpecifyKind(
                        expiresAtUtc.Value,
                        DateTimeKind.Utc))
                : null
        };
    }

    private static UnauthorizedAccessException
        InvalidRefreshRequest()
    {
        return new UnauthorizedAccessException(
            "The refresh session is invalid or has " +
            "expired.");
    }
}
