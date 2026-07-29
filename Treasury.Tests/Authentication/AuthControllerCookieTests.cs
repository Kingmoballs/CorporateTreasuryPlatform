using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Treasury.Api.Controllers;
using Treasury.Api.Models;
using Treasury.Api.Security;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;

namespace Treasury.Tests.Authentication;

public class AuthControllerCookieTests
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
    public async Task Login_WritesRefreshCookie()
    {
        var setup = CreateSetup();
        var response = CreateAuthenticationResponse(
            "new-refresh-token");
        setup.AuthService
            .Setup(service =>
                service.Login(
                    It.IsAny<LoginDto>()))
            .ReturnsAsync(response);

        var result =
            await setup.Controller.Login(
                new LoginDto());

        Assert.IsType<OkObjectResult>(result);
        setup.CookieService.Verify(
            service =>
                service.Append(
                    It.IsAny<HttpResponse>(),
                    "new-refresh-token",
                    ExpiresAtUtc),
            Times.Once);
    }

    [Fact]
    public async Task
        LoginMfaChallenge_ClearsExistingCookie()
    {
        var setup = CreateSetup();
        setup.AuthService
            .Setup(service =>
                service.Login(
                    It.IsAny<LoginDto>()))
            .ReturnsAsync(
                new AuthResponseDto
                {
                    MfaRequired = true
                });

        var result =
            await setup.Controller.Login(
                new LoginDto());

        Assert.IsType<OkObjectResult>(result);
        setup.CookieService.Verify(
            service =>
                service.Delete(
                    It.IsAny<HttpResponse>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_RotatesCookie()
    {
        var setup = CreateSetup();
        setup.CookieService
            .Setup(service =>
                service.GetRequiredToken(
                    It.IsAny<HttpRequest>(),
                    RefreshTokenCookieService
                        .ClientRequestHeaderValue))
            .Returns("old-refresh-token");
        setup.AuthService
            .Setup(service =>
                service.Refresh(
                    "old-refresh-token"))
            .ReturnsAsync(
                CreateAuthenticationResponse(
                    "rotated-refresh-token"));

        var result =
            await setup.Controller.Refresh(
                RefreshTokenCookieService
                    .ClientRequestHeaderValue);

        Assert.IsType<OkObjectResult>(result);
        setup.CookieService.Verify(
            service =>
                service.Append(
                    It.IsAny<HttpResponse>(),
                    "rotated-refresh-token",
                    ExpiresAtUtc),
            Times.Once);
    }

    [Fact]
    public async Task
        InvalidRefresh_ClearsCookieAndReturnsStandardError()
    {
        var setup = CreateSetup();
        setup.CookieService
            .Setup(service =>
                service.GetRequiredToken(
                    It.IsAny<HttpRequest>(),
                    It.IsAny<string?>()))
            .Throws(
                new UnauthorizedAccessException(
                    "Invalid refresh session."));

        var result =
            await setup.Controller.Refresh(
                RefreshTokenCookieService
                    .ClientRequestHeaderValue);

        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);
        var error =
            Assert.IsType<ApiErrorResponse>(
                unauthorized.Value);

        Assert.Equal(
            "authentication_failed",
            error.Code);
        setup.CookieService.Verify(
            service =>
                service.Delete(
                    It.IsAny<HttpResponse>()),
            Times.Once);
    }

    [Fact]
    public async Task Logout_ClearsRefreshCookie()
    {
        var setup = CreateSetup();
        setup.AuthService
            .Setup(service => service.Logout())
            .Returns(Task.CompletedTask);

        var result =
            await setup.Controller.Logout();

        Assert.IsType<NoContentResult>(result);
        setup.CookieService.Verify(
            service =>
                service.Delete(
                    It.IsAny<HttpResponse>()),
            Times.Once);
    }

    private static ControllerSetup CreateSetup()
    {
        var authService =
            new Mock<IAuthService>();
        var cookieService =
            new Mock<IRefreshTokenCookieService>();
        var controller =
            new AuthController(
                authService.Object,
                cookieService.Object)
            {
                ControllerContext =
                    new ControllerContext
                    {
                        HttpContext =
                            new DefaultHttpContext()
                    }
            };

        return new ControllerSetup(
            controller,
            authService,
            cookieService);
    }

    private static AuthResponseDto
        CreateAuthenticationResponse(
            string refreshToken)
    {
        return new AuthResponseDto
        {
            AccessToken = "access-token",
            RefreshTokenForCookie = refreshToken,
            AccessTokenExpiresAtUtc =
                ExpiresAtUtc.AddMinutes(-5),
            RefreshTokenExpiresAtUtc =
                ExpiresAtUtc
        };
    }

    private sealed record ControllerSetup(
        AuthController Controller,
        Mock<IAuthService> AuthService,
        Mock<IRefreshTokenCookieService>
            CookieService);
}
