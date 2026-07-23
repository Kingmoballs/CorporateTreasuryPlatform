using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Authentication;

public class PasswordRecoveryService
    : IPasswordRecoveryService
{
    private const int TokenByteCount = 32;

    private readonly IUserRepository _userRepository;

    private readonly IPasswordResetTokenRepository
        _tokenRepository;

    private readonly IEmailSender _emailSender;

    private readonly PasswordRecoveryOptions _options;

    private readonly TimeProvider _timeProvider;

    private readonly ILogger<PasswordRecoveryService>
        _logger;

    public PasswordRecoveryService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailSender emailSender,
        IOptions<PasswordRecoveryOptions> options,
        TimeProvider timeProvider,
        ILogger<PasswordRecoveryService> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailSender = emailSender;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ForgotPasswordResponseDto>
        RequestReset(ForgotPasswordDto dto)
    {
        var response =
            new ForgotPasswordResponseDto();

        /*
         * Validate delivery before the account lookup so
         * infrastructure failures cannot reveal whether an
         * email address is registered.
         */
        try
        {
            _emailSender.EnsureConfigured();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Password recovery email delivery is " +
                "not configured.");

            return response;
        }

        var user =
            await _userRepository.GetByEmail(
                dto.Email);

        if (user is null ||
            !user.IsActive ||
            !user.EmailVerifiedAtUtc.HasValue)
        {
            return response;
        }

        var now = GetUtcNow();
        var rawToken = GenerateToken();

        var token =
            new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = HashToken(rawToken),
                CreatedAtUtc = now,
                ExpiresAtUtc =
                    now.AddMinutes(
                        _options
                            .TokenExpiryMinutes)
            };

        var created =
            await _tokenRepository.TryCreate(
                token,
                now.AddMinutes(
                    -_options
                        .RequestCooldownMinutes));

        if (!created)
        {
            return response;
        }

        try
        {
            await _emailSender.SendPasswordReset(
                user.Email,
                GetDisplayName(user),
                BuildResetUrl(rawToken),
                token.ExpiresAtUtc);
        }
        catch (Exception exception)
        {
            /*
             * Do not leave an undelivered credential active
             * or make a transient SMTP failure trigger the
             * resend cooldown.
             */
            await _tokenRepository.Revoke(
                token.Id,
                GetUtcNow());

            _logger.LogError(
                exception,
                "Failed to deliver password recovery " +
                "email for user {UserId}.",
                user.Id);

            return response;
        }

        return response;
    }

    public async Task ResetPassword(
        ResetPasswordDto dto)
    {
        var token =
            await _tokenRepository
                .GetByTokenHash(
                    HashToken(dto.Token));

        var now = GetUtcNow();

        if (token is null ||
            token.ConsumedAtUtc.HasValue ||
            token.RevokedAtUtc.HasValue ||
            token.ExpiresAtUtc <= now ||
            !token.User.IsActive ||
            !token.User.EmailVerifiedAtUtc.HasValue)
        {
            throw InvalidResetToken();
        }

        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        var changed =
            await _tokenRepository
                .ConsumeAndChangePassword(
                    token.Id,
                    token.UserId,
                    passwordHash,
                    Guid.NewGuid(),
                    now);

        if (!changed)
        {
            throw InvalidResetToken();
        }
    }

    private string BuildResetUrl(string rawToken)
    {
        var builder =
            new UriBuilder(_options.ResetUrl);

        var existingQuery =
            builder.Query.TrimStart('?');

        var tokenParameter =
            "token=" +
            Uri.EscapeDataString(rawToken);

        builder.Query =
            string.IsNullOrWhiteSpace(existingQuery)
                ? tokenParameter
                : existingQuery +
                    "&" +
                    tokenParameter;

        return builder.Uri.AbsoluteUri;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider
            .GetUtcNow()
            .UtcDateTime;
    }

    private static string GetDisplayName(User user)
    {
        var name =
            $"{user.FirstName} {user.LastName}"
                .Trim();

        return string.IsNullOrWhiteSpace(name)
            ? user.Email
            : name;
    }

    private static string GenerateToken()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(
                TokenByteCount);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(
        string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return string.Empty;
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawToken)));
    }

    private static UnauthorizedAccessException
        InvalidResetToken()
    {
        return new UnauthorizedAccessException(
            "The password reset token is invalid or " +
            "has expired.");
    }
}
