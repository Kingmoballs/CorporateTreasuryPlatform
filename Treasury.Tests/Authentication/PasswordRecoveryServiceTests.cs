using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Authentication;

namespace Treasury.Tests.Authentication;

public class PasswordRecoveryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(
            2026,
            7,
            23,
            19,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        RequestReset_StoresHashAndEmailsRawToken()
    {
        var setup = CreateSetup();
        PasswordResetToken? storedToken = null;
        string? emailedUrl = null;

        setup.TokenRepository
            .Setup(repository =>
                repository.TryCreate(
                    It.IsAny<PasswordResetToken>(),
                    Now.AddMinutes(-5).UtcDateTime))
            .Callback<
                PasswordResetToken,
                DateTime>(
                (token, _) =>
                    storedToken = token)
            .ReturnsAsync(true);

        setup.EmailSender
            .Setup(sender =>
                sender.SendPasswordReset(
                    setup.User.Email,
                    "Ada Okafor",
                    It.IsAny<string>(),
                    Now.AddMinutes(30).UtcDateTime))
            .Callback<
                string,
                string,
                string,
                DateTime>(
                (_, _, url, _) =>
                    emailedUrl = url)
            .Returns(Task.CompletedTask);

        var response =
            await setup.Service.RequestReset(
                new ForgotPasswordDto
                {
                    Email =
                        " ADA@EXAMPLE.COM "
                });

        Assert.NotNull(storedToken);
        Assert.NotNull(emailedUrl);

        var rawToken =
            GetTokenFromUrl(emailedUrl);

        Assert.Equal(
            HashToken(rawToken),
            storedToken.TokenHash);
        Assert.DoesNotContain(
            rawToken,
            storedToken.TokenHash,
            StringComparison.Ordinal);
        Assert.Equal(
            setup.User.Id,
            storedToken.UserId);
        Assert.Equal(
            Now.AddMinutes(30).UtcDateTime,
            storedToken.ExpiresAtUtc);
        Assert.Contains(
            "If the account is eligible",
            response.Message,
            StringComparison.Ordinal);

        setup.EmailSender.Verify(
            sender => sender.EnsureConfigured(),
            Times.Once);
    }

    [Fact]
    public async Task
        RequestReset_UnknownAccountReturnsGenericResponse()
    {
        var setup = CreateSetup(
            userExists: false);

        var response =
            await setup.Service.RequestReset(
                new ForgotPasswordDto
                {
                    Email =
                        "unknown@example.com"
                });

        Assert.Contains(
            "If the account is eligible",
            response.Message,
            StringComparison.Ordinal);

        setup.TokenRepository.Verify(
            repository =>
                repository.TryCreate(
                    It.IsAny<PasswordResetToken>(),
                    It.IsAny<DateTime>()),
            Times.Never);

        setup.EmailSender.Verify(
            sender =>
                sender.SendPasswordReset(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task
        RequestReset_EmailConfigurationFailureIsGeneric()
    {
        var setup = CreateSetup();

        setup.EmailSender
            .Setup(sender =>
                sender.EnsureConfigured())
            .Throws(
                new InvalidOperationException(
                    "Email is not configured"));

        var response =
            await setup.Service.RequestReset(
                new ForgotPasswordDto
                {
                    Email = setup.User.Email
                });

        Assert.Contains(
            "If the account is eligible",
            response.Message,
            StringComparison.Ordinal);

        setup.UserRepository.Verify(
            repository =>
                repository.GetByEmail(
                    It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task
        RequestReset_CooldownDoesNotSendAnotherEmail()
    {
        var setup = CreateSetup();

        setup.TokenRepository
            .Setup(repository =>
                repository.TryCreate(
                    It.IsAny<PasswordResetToken>(),
                    It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        await setup.Service.RequestReset(
            new ForgotPasswordDto
            {
                Email = setup.User.Email
            });

        setup.EmailSender.Verify(
            sender =>
                sender.SendPasswordReset(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task
        RequestReset_EmailFailureRevokesToken()
    {
        var setup = CreateSetup();
        PasswordResetToken? storedToken = null;

        setup.TokenRepository
            .Setup(repository =>
                repository.TryCreate(
                    It.IsAny<PasswordResetToken>(),
                    It.IsAny<DateTime>()))
            .Callback<
                PasswordResetToken,
                DateTime>(
                (token, _) =>
                    storedToken = token)
            .ReturnsAsync(true);

        setup.EmailSender
            .Setup(sender =>
                sender.SendPasswordReset(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "SMTP unavailable"));

        var response =
            await setup.Service.RequestReset(
                new ForgotPasswordDto
                {
                    Email = setup.User.Email
                });

        Assert.NotNull(storedToken);
        Assert.Contains(
            "If the account is eligible",
            response.Message,
            StringComparison.Ordinal);

        setup.TokenRepository.Verify(
            repository =>
                repository.Revoke(
                    storedToken.Id,
                    Now.UtcDateTime),
            Times.Once);
    }

    [Fact]
    public async Task
        ResetPassword_AtomicallyChangesCredential()
    {
        var setup = CreateSetup();
        var rawToken = "valid-password-reset-token";

        var token =
            CreateToken(
                setup.User,
                rawToken,
                Now.AddMinutes(10).UtcDateTime);

        setup.TokenRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        string? newPasswordHash = null;
        Guid newSecurityStamp = Guid.Empty;

        setup.TokenRepository
            .Setup(repository =>
                repository
                    .ConsumeAndChangePassword(
                        token.Id,
                        setup.User.Id,
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        Now.UtcDateTime))
            .Callback<
                Guid,
                Guid,
                string,
                Guid,
                DateTime>(
                (_, _, hash, stamp, _) =>
                {
                    newPasswordHash = hash;
                    newSecurityStamp = stamp;
                })
            .ReturnsAsync(true);

        await setup.Service.ResetPassword(
            new ResetPasswordDto
            {
                Token = rawToken,
                NewPassword =
                    "NewSecurePassword123!"
            });

        Assert.NotNull(newPasswordHash);
        Assert.True(
            BCrypt.Net.BCrypt.Verify(
                "NewSecurePassword123!",
                newPasswordHash));
        Assert.NotEqual(
            Guid.Empty,
            newSecurityStamp);
        Assert.NotEqual(
            setup.User.SecurityStamp,
            newSecurityStamp);
    }

    [Fact]
    public async Task
        ResetPassword_ExpiredTokenIsRejected()
    {
        var setup = CreateSetup();
        var rawToken = "expired-password-reset-token";

        setup.TokenRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(
                CreateToken(
                    setup.User,
                    rawToken,
                    Now.AddSeconds(-1).UtcDateTime));

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.ResetPassword(
                new ResetPasswordDto
                {
                    Token = rawToken,
                    NewPassword =
                        "NewSecurePassword123!"
                }));

        setup.TokenRepository.Verify(
            repository =>
                repository
                    .ConsumeAndChangePassword(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task
        ResetPassword_ConcurrentOrReplayedUseIsRejected()
    {
        var setup = CreateSetup();
        var rawToken = "already-consumed-reset-token";

        var token =
            CreateToken(
                setup.User,
                rawToken,
                Now.AddMinutes(10).UtcDateTime);

        setup.TokenRepository
            .Setup(repository =>
                repository.GetByTokenHash(
                    HashToken(rawToken)))
            .ReturnsAsync(token);

        setup.TokenRepository
            .Setup(repository =>
                repository
                    .ConsumeAndChangePassword(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => setup.Service.ResetPassword(
                new ResetPasswordDto
                {
                    Token = rawToken,
                    NewPassword =
                        "NewSecurePassword123!"
                }));
    }

    private static ServiceSetup CreateSetup(
        bool userExists = true)
    {
        var user =
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Okafor",
                Email = "ada@example.com",
                PasswordHash = "old-hash",
                EmailVerifiedAtUtc =
                    Now.AddDays(-30).UtcDateTime,
                IsActive = true,
                SecurityStamp =
                    Guid.NewGuid()
            };

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

        var tokenRepository =
            new Mock<
                IPasswordResetTokenRepository>();

        var emailSender =
            new Mock<IEmailSender>();

        var service =
            new PasswordRecoveryService(
                userRepository.Object,
                tokenRepository.Object,
                emailSender.Object,
                Options.Create(
                    new PasswordRecoveryOptions
                    {
                        TokenExpiryMinutes = 30,
                        RequestCooldownMinutes = 5,
                        ResetUrl =
                            "https://treasury.example/" +
                            "reset-password"
                    }),
                new FixedTimeProvider(Now),
                NullLogger<
                    PasswordRecoveryService>
                    .Instance,
                Mock.Of<
                    IAuthenticationSecurityEventService>());

        return new ServiceSetup(
            service,
            userRepository,
            tokenRepository,
            emailSender,
            user);
    }

    private static PasswordResetToken CreateToken(
        User user,
        string rawToken,
        DateTime expiresAtUtc)
    {
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc =
                Now.AddMinutes(-5).UtcDateTime,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static string GetTokenFromUrl(
        string resetUrl)
    {
        var uri = new Uri(resetUrl);

        var tokenParameter =
            uri.Query
                .TrimStart('?')
                .Split('&')
                .Single(parameter =>
                    parameter.StartsWith(
                        "token=",
                        StringComparison.Ordinal));

        return Uri.UnescapeDataString(
            tokenParameter["token=".Length..]);
    }

    private static string HashToken(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed record ServiceSetup(
        PasswordRecoveryService Service,
        Mock<IUserRepository> UserRepository,
        Mock<IPasswordResetTokenRepository>
            TokenRepository,
        Mock<IEmailSender> EmailSender,
        User User);
}
