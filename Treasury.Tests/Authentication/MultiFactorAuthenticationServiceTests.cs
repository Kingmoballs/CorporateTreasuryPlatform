using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Authentication;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Authentication;

public class MultiFactorAuthenticationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            21,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        CreateLoginChallenge_StoresOnlyTokenHash()
    {
        var setup = CreateSetup();
        MfaLoginChallenge? storedChallenge = null;

        setup.Repository
            .Setup(repository =>
                repository.TryCreateChallenge(
                    It.IsAny<MfaLoginChallenge>()))
            .Callback<MfaLoginChallenge>(
                challenge =>
                    storedChallenge = challenge)
            .ReturnsAsync(true);

        var response =
            await setup.Service
                .CreateLoginChallenge(
                    setup.User,
                    setup.Membership);

        Assert.True(response.MfaRequired);
        Assert.NotNull(
            response.MfaChallengeToken);
        Assert.NotNull(storedChallenge);
        Assert.Equal(
            Hash(response.MfaChallengeToken),
            storedChallenge.TokenHash);
        Assert.DoesNotContain(
            response.MfaChallengeToken,
            storedChallenge.TokenHash,
            StringComparison.Ordinal);
        Assert.Equal(
            Now.AddMinutes(5).UtcDateTime,
            response.MfaChallengeExpiresAtUtc);
    }

    [Fact]
    public async Task
        VerifyChallenge_ConsumesBeforeCreatingSession()
    {
        var setup = CreateSetup();
        var rawToken = "valid-mfa-challenge";
        var challenge =
            CreateChallenge(setup, rawToken);

        setup.Repository
            .Setup(repository =>
                repository.GetChallengeByHash(
                    Hash(rawToken)))
            .ReturnsAsync(challenge);

        setup.TotpService
            .Setup(service =>
                service.Verify(
                    "plain-secret",
                    "123456",
                    Now.UtcDateTime))
            .Returns(true);

        setup.Repository
            .Setup(repository =>
                repository.ConsumeChallenge(
                    challenge.Id,
                    setup.User.Id,
                    Now.UtcDateTime,
                    5))
            .ReturnsAsync(true);

        setup.SessionService
            .Setup(service =>
                service.Create(
                    setup.User,
                    setup.Membership,
                    AuthenticationMethods.Totp))
            .ReturnsAsync(
                new AuthenticationTokenPairDto
                {
                    AccessToken = "access",
                    RefreshToken = "refresh",
                    AccessTokenExpiresAtUtc =
                        Now.AddMinutes(15)
                            .UtcDateTime,
                    RefreshTokenExpiresAtUtc =
                        Now.AddDays(7).UtcDateTime
                });

        var response =
            await setup.Service.VerifyChallenge(
                new VerifyMfaChallengeDto
                {
                    ChallengeToken = rawToken,
                    Code = "123456"
                });

        Assert.False(response.MfaRequired);
        Assert.Equal(
            "access",
            response.AccessToken);
        Assert.Equal(
            "refresh",
            response.RefreshTokenForCookie);
    }

    [Fact]
    public async Task
        VerifyChallenge_InvalidCodeRecordsAttempt()
    {
        var setup = CreateSetup();
        var rawToken = "invalid-code-challenge";
        var challenge =
            CreateChallenge(setup, rawToken);

        setup.Repository
            .Setup(repository =>
                repository.GetChallengeByHash(
                    Hash(rawToken)))
            .ReturnsAsync(challenge);

        setup.TotpService
            .Setup(service =>
                service.Verify(
                    It.IsAny<string>(),
                    "000000",
                    Now.UtcDateTime))
            .Returns(false);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.VerifyChallenge(
                new VerifyMfaChallengeDto
                {
                    ChallengeToken = rawToken,
                    Code = "000000"
                }));

        setup.Repository.Verify(
            repository =>
                repository
                    .RecordFailedChallengeAttempt(
                        challenge.Id,
                        Now.UtcDateTime,
                        5),
            Times.Once);

        setup.SessionService.Verify(
            service =>
                service.Create(
                    It.IsAny<User>(),
                    It.IsAny<
                        OrganizationMembership>(),
                    It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task
        StartEnrollment_ProtectsSecretBeforeStorage()
    {
        var setup = CreateSetup(
            mfaEnabled: false);

        setup.TotpService
            .Setup(service =>
                service.GenerateSecret())
            .Returns("BASE32SECRET");

        setup.Repository
            .Setup(repository =>
                repository.SetPendingEnrollment(
                    setup.User.Id,
                    setup.User.SecurityStamp,
                    "protected::BASE32SECRET",
                    Now.UtcDateTime))
            .ReturnsAsync(true);

        var response =
            await setup.Service.StartEnrollment(
                new StartMfaEnrollmentDto
                {
                    CurrentPassword =
                        "SecurePassword123!"
                });

        Assert.Equal(
            "BASE32SECRET",
            response.ManualEntryKey);
        Assert.Equal(
            Now.AddMinutes(15).UtcDateTime,
            response.ExpiresAtUtc);
        Assert.DoesNotContain(
            "protected::",
            response.ManualEntryKey,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task
        ConfirmEnrollment_ReturnsOnlyRawRecoveryCodes()
    {
        var setup = CreateSetup(
            mfaEnabled: false);

        setup.User.ProtectedTotpSecret =
            "protected::plain-secret";

        setup.User.MfaEnrollmentStartedAtUtc =
            Now.AddMinutes(-1).UtcDateTime;

        setup.TotpService
            .Setup(service =>
                service.Verify(
                    "plain-secret",
                    "123456",
                    Now.UtcDateTime))
            .Returns(true);

        IReadOnlyCollection<MfaRecoveryCode>?
            storedCodes = null;

        setup.Repository
            .Setup(repository =>
                repository.Enable(
                    setup.User.Id,
                    setup.User.SecurityStamp,
                    Now.UtcDateTime,
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyCollection<
                        MfaRecoveryCode>>()))
            .Callback<
                Guid,
                Guid,
                DateTime,
                Guid,
                IReadOnlyCollection<
                    MfaRecoveryCode>>(
                (_, _, _, _, codes) =>
                    storedCodes = codes)
            .ReturnsAsync(true);

        var response =
            await setup.Service.ConfirmEnrollment(
                new ConfirmMfaEnrollmentDto
                {
                    Code = "123456"
                });

        Assert.Equal(
            10,
            response.RecoveryCodes.Count);
        Assert.NotNull(storedCodes);
        Assert.Equal(10, storedCodes.Count);

        foreach (var code in
                 response.RecoveryCodes)
        {
            Assert.Matches(
                "^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$",
                code);

            Assert.Contains(
                storedCodes,
                stored =>
                    stored.CodeHash ==
                    Hash(
                        code.Replace(
                            "-",
                            string.Empty)));

            Assert.DoesNotContain(
                storedCodes,
                stored =>
                    stored.CodeHash == code);
        }
    }

    [Fact]
    public async Task
        Disable_RotatesSecurityState()
    {
        var setup = CreateSetup();

        setup.TotpService
            .Setup(service =>
                service.Verify(
                    "plain-secret",
                    "123456",
                    Now.UtcDateTime))
            .Returns(true);

        Guid replacementStamp = Guid.Empty;

        setup.Repository
            .Setup(repository =>
                repository.Disable(
                    setup.User.Id,
                    setup.User.SecurityStamp,
                    Now.UtcDateTime,
                    It.IsAny<Guid>()))
            .Callback<Guid, Guid, DateTime, Guid>(
                (_, _, _, stamp) =>
                    replacementStamp = stamp)
            .ReturnsAsync(true);

        await setup.Service.Disable(
            new DisableMfaDto
            {
                CurrentPassword =
                    "SecurePassword123!",
                Code = "123456"
            });

        Assert.NotEqual(
            Guid.Empty,
            replacementStamp);
        Assert.NotEqual(
            setup.User.SecurityStamp,
            replacementStamp);
    }

    private static ServiceSetup CreateSetup(
        bool mfaEnabled = true)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.FinanceManager
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
            ProtectedTotpSecret =
                mfaEnabled
                    ? "protected::plain-secret"
                    : null,
            MfaEnabledAtUtc =
                mfaEnabled
                    ? Now.AddDays(-1).UtcDateTime
                    : null,
            IsActive = true,
            RoleId = role.Id,
            Role = role,
            SecurityStamp = Guid.NewGuid()
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

        var repository =
            new Mock<IMultiFactorRepository>();

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(item =>
                item.GetById(user.Id))
            .ReturnsAsync(user);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .SetupGet(service => service.UserId)
            .Returns(user.Id);

        var sessionService =
            new Mock<
                IAuthenticationSessionService>();

        var totpService =
            new Mock<ITotpService>();

        totpService
            .Setup(service =>
                service.BuildProvisioningUri(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Returns(
                "otpauth://totp/test");

        var secretProtector =
            new Mock<IMfaSecretProtector>();

        secretProtector
            .Setup(protector =>
                protector.Protect(
                    It.IsAny<string>()))
            .Returns<string>(secret =>
                "protected::" + secret);

        secretProtector
            .Setup(protector =>
                protector.Unprotect(
                    It.IsAny<string>()))
            .Returns<string>(protectedSecret =>
                protectedSecret[
                    "protected::".Length..]);

        var service =
            new MultiFactorAuthenticationService(
                repository.Object,
                userRepository.Object,
                currentUserService.Object,
                sessionService.Object,
                totpService.Object,
                secretProtector.Object,
                Options.Create(
                    new MultiFactorAuthenticationOptions
                    {
                        Issuer =
                            "Corporate Treasury Platform",
                        EnrollmentMinutes = 15,
                        ChallengeMinutes = 5,
                        MaximumChallengeAttempts = 5,
                        RecoveryCodeCount = 10
                    }),
                new FixedTimeProvider(Now),
                Mock.Of<
                    IAuthenticationSecurityEventService>());

        return new ServiceSetup(
            service,
            repository,
            userRepository,
            sessionService,
            totpService,
            user,
            membership);
    }

    private static MfaLoginChallenge
        CreateChallenge(
            ServiceSetup setup,
            string rawToken)
    {
        return new MfaLoginChallenge
        {
            Id = Guid.NewGuid(),
            UserId = setup.User.Id,
            User = setup.User,
            OrganizationId =
                setup.Membership.OrganizationId,
            Organization =
                setup.Membership.Organization,
            OrganizationMembershipId =
                setup.Membership.Id,
            OrganizationMembership =
                setup.Membership,
            TokenHash = Hash(rawToken),
            SecurityStamp =
                setup.User.SecurityStamp,
            CreatedAtUtc =
                Now.AddMinutes(-1).UtcDateTime,
            ExpiresAtUtc =
                Now.AddMinutes(4).UtcDateTime
        };
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
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
        MultiFactorAuthenticationService Service,
        Mock<IMultiFactorRepository> Repository,
        Mock<IUserRepository> UserRepository,
        Mock<IAuthenticationSessionService>
            SessionService,
        Mock<ITotpService> TotpService,
        User User,
        OrganizationMembership Membership);
}
