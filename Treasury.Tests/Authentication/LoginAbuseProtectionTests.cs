using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Api.Controllers;
using Treasury.Api.Security;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Application.Services;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Authentication;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class LoginAbuseProtectionTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            20,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        Login_WrongPasswordRecordsFailure()
    {
        var setup = CreateSetup();

        var exception =
            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => setup.Service.Login(
                    new LoginDto
                    {
                        Email = setup.User.Email,
                        Password = "WrongPassword123!"
                    }));

        Assert.Equal(
            "Invalid credentials.",
            exception.Message);

        setup.LoginAttemptService.Verify(
            service =>
                service.RecordFailure(
                    setup.User.Id),
            Times.Once);

        setup.SessionService.Verify(
            service =>
                service.Create(
                    It.IsAny<User>(),
                    It.IsAny<
                        OrganizationMembership>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Login_LockedAccountReturnsGenericFailure()
    {
        var setup = CreateSetup();

        setup.LoginAttemptService
            .Setup(service =>
                service.CompleteSuccessfulAttempt(
                    setup.User.Id))
            .ReturnsAsync(false);

        var exception =
            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => setup.Service.Login(
                    new LoginDto
                    {
                        Email = setup.User.Email,
                        Password =
                            "SecurePassword123!"
                    }));

        Assert.Equal(
            "Invalid credentials.",
            exception.Message);

        setup.SessionService.Verify(
            service =>
                service.Create(
                    It.IsAny<User>(),
                    It.IsAny<
                        OrganizationMembership>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Login_UnknownAccountReturnsGenericFailure()
    {
        var setup = CreateSetup(
            userExists: false);

        var exception =
            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => setup.Service.Login(
                    new LoginDto
                    {
                        Email = "unknown@example.com",
                        Password =
                            "SecurePassword123!"
                    }));

        Assert.Equal(
            "Invalid credentials.",
            exception.Message);

        setup.LoginAttemptService.Verify(
            service =>
                service.RecordFailure(
                    It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Login_MfaEnabledCreatesChallengeWithoutSession()
    {
        var setup = CreateSetup();

        setup.User.MfaEnabledAtUtc =
            Now.AddDays(-1).UtcDateTime;

        setup.MultiFactorService
            .Setup(service =>
                service.CreateLoginChallenge(
                    setup.User,
                    It.IsAny<
                        OrganizationMembership>()))
            .ReturnsAsync(
                new AuthResponseDto
                {
                    MfaRequired = true,
                    MfaChallengeToken =
                        "challenge-token",
                    MfaChallengeExpiresAtUtc =
                        Now.AddMinutes(5)
                            .UtcDateTime
                });

        var response =
            await setup.Service.Login(
                new LoginDto
                {
                    Email = setup.User.Email,
                    Password =
                        "SecurePassword123!"
                });

        Assert.True(response.MfaRequired);
        Assert.Equal(
            "challenge-token",
            response.MfaChallengeToken);

        setup.SessionService.Verify(
            service =>
                service.Create(
                    It.IsAny<User>(),
                    It.IsAny<
                        OrganizationMembership>()),
            Times.Never);
    }

    [Fact]
    public async Task
        LoginAttemptService_UsesConfiguredThresholds()
    {
        var repository =
            new Mock<IUserRepository>();

        var userId = Guid.NewGuid();

        var service =
            new LoginAttemptService(
                repository.Object,
                Options.Create(
                    new AuthenticationSecurityOptions
                    {
                        MaximumFailedLoginAttempts = 5,
                        LoginFailureWindowMinutes = 15,
                        LoginLockoutMinutes = 20
                    }),
                new FixedTimeProvider(Now));

        await service.RecordFailure(userId);

        repository.Verify(
            item =>
                item.RecordFailedLogin(
                    userId,
                    Now.UtcDateTime,
                    Now.AddMinutes(-15).UtcDateTime,
                    5,
                    Now.AddMinutes(20).UtcDateTime),
            Times.Once);
    }

    [Theory]
    [InlineData(
        nameof(AuthController.Login),
        AuthenticationRateLimitPolicies.Login)]
    [InlineData(
        nameof(AuthController.Refresh),
        AuthenticationRateLimitPolicies.Refresh)]
    [InlineData(
        nameof(AuthController.ForgotPassword),
        AuthenticationRateLimitPolicies
            .PasswordRecovery)]
    [InlineData(
        nameof(AuthController.ResetPassword),
        AuthenticationRateLimitPolicies
            .PasswordRecovery)]
    [InlineData(
        nameof(AuthController.VerifyMfaChallenge),
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    [InlineData(
        nameof(AuthController.UseMfaRecoveryCode),
        AuthenticationRateLimitPolicies
            .MultiFactorAuthentication)]
    public void AuthenticationEndpoint_HasRateLimit(
        string methodName,
        string expectedPolicy)
    {
        var method =
            typeof(AuthController)
                .GetMethod(methodName);

        var attribute =
            method?.GetCustomAttribute<
                EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(
            expectedPolicy,
            attribute.PolicyName);
    }

    private static ServiceSetup CreateSetup(
        bool userExists = true)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "MOBALLS",
            Name = "Moballs Limited",
            Slug = "moballs"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Okafor",
            Email = "ada@example.com",
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    "SecurePassword123!"),
            EmailVerifiedAtUtc =
                Now.AddDays(-30).UtcDateTime,
            IsActive = true,
            RoleId = role.Id,
            Role = role
        };

        var membership =
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organization.Id,
                Organization = organization,
                UserId = user.Id,
                User = user,
                RoleId = role.Id,
                Role = role,
                IsActive = true,
                IsDefault = true
            };

        user.OrganizationMemberships.Add(
            membership);

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(repository =>
                repository.GetByEmail(
                    It.IsAny<string>()))
            .ReturnsAsync(
                userExists
                    ? user
                    : null);

        var loginAttemptService =
            new Mock<ILoginAttemptService>();

        loginAttemptService
            .Setup(service =>
                service.CompleteSuccessfulAttempt(
                    user.Id))
            .ReturnsAsync(true);

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var multiFactorService =
            new Mock<
                IMultiFactorAuthenticationService>();

        var service =
            new AuthService(
                userRepository.Object,
                loginAttemptService.Object,
                multiFactorService.Object,
                sessionService.Object,
                Mock.Of<ICurrentUserService>(),
                Mock.Of<
                    IAuthenticationSecurityEventService>());

        return new ServiceSetup(
            service,
            userRepository,
            loginAttemptService,
            multiFactorService,
            sessionService,
            user);
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(
            DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _now;
        }
    }

    private sealed record ServiceSetup(
        AuthService Service,
        Mock<IUserRepository> UserRepository,
        Mock<ILoginAttemptService>
            LoginAttemptService,
        Mock<IMultiFactorAuthenticationService>
            MultiFactorService,
        Mock<IAuthenticationSessionService>
            SessionService,
        User User);
}
