using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Treasury.Api.Configuration;
using Treasury.Api.Security;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Tests.Authentication;

public class RefreshTokenCookieTests
{
    private static readonly DateTime ExpiresAtUtc =
        new(
            2026,
            8,
            4,
            12,
            0,
            0,
            DateTimeKind.Utc);

    [Fact]
    public void Append_UsesRestrictedSecurityAttributes()
    {
        var context = new DefaultHttpContext();
        var service = CreateService();

        service.Append(
            context.Response,
            "rotated-token",
            ExpiresAtUtc);

        var setCookie =
            context.Response.Headers.SetCookie
                .ToString();

        Assert.Contains(
            "Treasury.RefreshToken=rotated-token",
            setCookie);
        Assert.Contains(
            "path=/api/v1/auth",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "secure",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "httponly",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "samesite=strict",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void
        GetRequiredToken_RequiresCookieAndClientHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            "Treasury.RefreshToken=rotating-token";
        var service = CreateService();

        var result = service.GetRequiredToken(
            context.Request,
            RefreshTokenCookieService
                .ClientRequestHeaderValue);

        Assert.Equal("rotating-token", result);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("wrong-client", true)]
    [InlineData("web", false)]
    public void GetRequiredToken_RejectsInvalidRequest(
        string? header,
        bool includeCookie)
    {
        var context = new DefaultHttpContext();

        if (includeCookie)
        {
            context.Request.Headers.Cookie =
                "Treasury.RefreshToken=rotating-token";
        }

        var service = CreateService();

        Assert.Throws<UnauthorizedAccessException>(
            () =>
                service.GetRequiredToken(
                    context.Request,
                    header));
    }

    [Fact]
    public void Delete_UsesOriginalCookieScope()
    {
        var context = new DefaultHttpContext();
        var service = CreateService();

        service.Delete(context.Response);

        var setCookie =
            context.Response.Headers.SetCookie
                .ToString();

        Assert.Contains(
            "Treasury.RefreshToken=",
            setCookie);
        Assert.Contains(
            "path=/api/v1/auth",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "httponly",
            setCookie,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthenticationResponses_DoNotSerializeSecrets()
    {
        var authenticationResponse =
            JsonSerializer.Serialize(
                new AuthResponseDto
                {
                    AccessToken = "access-token",
                    RefreshTokenForCookie =
                        "response-refresh-secret"
                });

        var tokenPair =
            JsonSerializer.Serialize(
                new AuthenticationTokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken =
                        "pair-refresh-secret"
                });
        using var authenticationDocument =
            JsonDocument.Parse(
                authenticationResponse);
        using var tokenPairDocument =
            JsonDocument.Parse(tokenPair);

        Assert.DoesNotContain(
            "response-refresh-secret",
            authenticationResponse);
        Assert.False(
            authenticationDocument.RootElement
                .TryGetProperty(
                    nameof(
                        AuthResponseDto
                            .RefreshTokenForCookie),
                    out _));
        Assert.DoesNotContain(
            "pair-refresh-secret",
            tokenPair);
        Assert.False(
            tokenPairDocument.RootElement
                .TryGetProperty(
                    nameof(
                        AuthenticationTokenPairDto
                            .RefreshToken),
                    out _));
    }

    private static RefreshTokenCookieService
        CreateService()
    {
        return new RefreshTokenCookieService(
            Options.Create(
                new RefreshTokenCookieOptions()));
    }
}
