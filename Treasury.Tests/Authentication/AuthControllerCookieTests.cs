using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Treasury.Api.Controllers;
using Treasury.Api.Models;
using Treasury.Api.Security;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

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

    [Fact]
    public async Task Me_ReturnsProfileAndMfaState()
    {
        var setup = CreateSetup();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var mfaEnabledAtUtc =
            new DateTime(
                2026,
                8,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);
        var currentUserService =
            new Mock<ICurrentUserService>();
        var userRepository =
            new Mock<IUserRepository>();

        currentUserService
            .SetupGet(service => service.UserId)
            .Returns(userId);
        currentUserService
            .SetupGet(service => service.Email)
            .Returns("ada@example.com");
        currentUserService
            .SetupGet(service => service.Role)
            .Returns("CFO");
        currentUserService
            .SetupGet(service =>
                service.OrganizationId)
            .Returns(organizationId);
        currentUserService
            .SetupGet(service =>
                service.OrganizationMembershipId)
            .Returns(membershipId);
        currentUserService
            .SetupGet(service =>
                service.AuthenticationSessionId)
            .Returns(sessionId);
        currentUserService
            .SetupGet(service =>
                service.OrganizationCode)
            .Returns("MOBALLS");
        userRepository
            .Setup(repository =>
                repository.GetById(userId))
            .ReturnsAsync(
                new User
                {
                    Id = userId,
                    FirstName = "Ada",
                    LastName = "Okafor",
                    Email = "ada@example.com",
                    MfaEnabledAtUtc =
                        mfaEnabledAtUtc
                });

        var result = await setup.Controller.Me(
            currentUserService.Object,
            userRepository.Object);

        var ok = Assert.IsType<OkObjectResult>(
            result);
        var response =
            Assert.IsType<CurrentUserDto>(
                ok.Value);

        Assert.Equal(userId, response.Id);
        Assert.Equal("Ada", response.FirstName);
        Assert.Equal("Okafor", response.LastName);
        Assert.Equal("CFO", response.Role);
        Assert.Equal(
            membershipId,
            response.OrganizationMembershipId);
        Assert.Equal(
            sessionId,
            response.AuthenticationSessionId);
        Assert.True(response.MfaEnabled);
        Assert.Equal(
            mfaEnabledAtUtc,
            response.MfaEnabledAtUtc);
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
